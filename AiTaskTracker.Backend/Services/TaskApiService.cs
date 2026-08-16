using System.Text.Json;
using AiTaskTracker.Server.Contracts;
using AiTaskTracker.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTaskTracker.Server.Services;

public sealed class TaskApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TaskDbContext _db;

    public TaskApiService(TaskDbContext db)
    {
        _db = db;
    }

    public async Task<List<TaskDto>> ListAsync(
        string workspaceId,
        string? status,
        bool includeDeleted,
        DateTimeOffset? updatedSince,
        CancellationToken cancellationToken)
    {
        IQueryable<TaskEntity> query = FullTaskQuery(workspaceId);

        if (!includeDeleted)
        {
            query = query.Where(task => !task.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = TaskValues.NormalizeStatus(status);
            query = query.Where(task => task.Status == normalized);
        }

        if (updatedSince is not null)
        {
            var sinceUtc = updatedSince.Value.UtcDateTime;
            query = query.Where(task => task.UpdatedAtUtc >= sinceUtc);
        }

        var tasks = await query
            .OrderByDescending(task => task.IsPinned)
            .ThenByDescending(task => task.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return tasks.Select(ToDto).ToList();
    }

    public async Task<TaskDto?> GetAsync(
        string workspaceId,
        string id,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId)
            .FirstOrDefaultAsync(item => item.Id == id && (includeDeleted || !item.IsDeleted), cancellationToken);
        return task is null ? null : ToDto(task);
    }

    public async Task<TaskDto> CreateAsync(
        string workspaceId,
        UserEntity user,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var actor = NormalizeActor(request.Actor, user);
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var task = new TaskEntity
        {
            Id = id,
            WorkspaceId = workspaceId,
            ShortId = $"TASK-{id[..6].ToUpperInvariant()}",
            Title = request.Title.Trim(),
            Notes = request.Notes.Trim(),
            Status = TaskValues.NormalizeStatus(request.Status),
            Priority = TaskValues.NormalizePriority(request.Priority),
            ProgressPercent = Math.Clamp(request.ProgressPercent, 0, 100),
            ProjectId = NormalizeValue(request.ProjectId, "default"),
            ListId = NormalizeValue(request.ListId, "main"),
            Assignee = request.Assignee.Trim(),
            TagsJson = SerializeStrings(request.Tags),
            Estimate = request.Estimate.Trim(),
            StartDateUtc = request.StartDate?.UtcDateTime,
            DueDateUtc = request.DueDate?.UtcDateTime,
            BlockedByTaskIdsJson = SerializeStrings(request.BlockedByTaskIds),
            IsPinned = request.IsPinned,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedBy = actor.ActorName,
            UpdatedBy = actor.ActorName,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id
        };

        task.Logs.Add(new TaskLogEntity
        {
            ActorName = actor.ActorName,
            ActorType = actor.ActorType,
            Message = "Task created.",
            CreatedAtUtc = now
        });

        _db.Tasks.Add(task);
        AddAudit(task, actor, "create_task", CreateChangedFields(), null, Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task<TaskDto?> UpdateAsync(
        string workspaceId,
        string id,
        UserEntity user,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var before = Snapshot(task);
        var changed = ApplyUpdate(task, request);
        if (changed.Count == 0)
        {
            return ToDto(task);
        }

        var actor = NormalizeActor(request.Actor, user);
        task.UpdatedAtUtc = DateTime.UtcNow;
        task.UpdatedBy = actor.ActorName;
        task.UpdatedByUserId = user.Id;
        changed.Add("updated_at");
        changed.Add("updated_by");
        AddAudit(task, actor, "update_task", changed, before, Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(task);
    }

    public async Task<bool> SoftDeleteAsync(
        string workspaceId,
        string id,
        UserEntity user,
        ActorDto? requestActor,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is null)
        {
            return false;
        }

        if (task.IsDeleted)
        {
            return true;
        }

        var actor = NormalizeActor(requestActor, user);
        var before = Snapshot(task);
        task.IsDeleted = true;
        task.UpdatedAtUtc = DateTime.UtcNow;
        task.UpdatedBy = actor.ActorName;
        task.UpdatedByUserId = user.Id;
        AddAudit(
            task,
            actor,
            "delete_task",
            new[] { "is_deleted", "updated_at", "updated_by" },
            before,
            Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TaskLogDto?> AddLogAsync(
        string workspaceId,
        string taskId,
        UserEntity user,
        AddTaskLogRequest request,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId).FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null || task.IsDeleted)
        {
            return null;
        }

        var actor = NormalizeActor(request.Actor, user);
        var before = Snapshot(task);
        var now = DateTime.UtcNow;
        var log = new TaskLogEntity
        {
            TaskId = task.Id,
            ActorName = actor.ActorName,
            ActorType = actor.ActorType,
            Message = request.Message.Trim(),
            CreatedAtUtc = now
        };
        task.Logs.Add(log);
        task.UpdatedAtUtc = now;
        task.UpdatedBy = actor.ActorName;
        task.UpdatedByUserId = user.Id;
        AddAudit(
            task,
            actor,
            "add_task_log",
            new[] { "logs", "updated_at", "updated_by" },
            before,
            Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(log);
    }

    public async Task<SubtaskDto?> AddSubtaskAsync(
        string workspaceId,
        string taskId,
        UserEntity user,
        AddSubtaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId).FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null || task.IsDeleted)
        {
            return null;
        }

        var actor = NormalizeActor(request.Actor, user);
        var before = Snapshot(task);
        var now = DateTime.UtcNow;
        var subtask = new SubtaskEntity
        {
            TaskId = task.Id,
            Title = request.Title.Trim(),
            Status = TaskValues.NormalizeStatus(request.Status),
            ProgressPercent = Math.Clamp(request.ProgressPercent, 0, 100),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        task.Subtasks.Add(subtask);
        task.UpdatedAtUtc = now;
        task.UpdatedBy = actor.ActorName;
        task.UpdatedByUserId = user.Id;
        AddAudit(
            task,
            actor,
            "add_subtask",
            new[] { "subtasks", "updated_at", "updated_by" },
            before,
            Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(subtask);
    }

    public async Task<SubtaskDto?> UpdateSubtaskAsync(
        string workspaceId,
        string taskId,
        string subtaskId,
        UserEntity user,
        UpdateSubtaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId).FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        var subtask = task?.Subtasks.FirstOrDefault(item => item.Id == subtaskId);
        if (task is null || task.IsDeleted || subtask is null)
        {
            return null;
        }

        var before = Snapshot(task);
        var changed = new List<string>();
        SetIfChanged(request.Title, subtask.Title, value => subtask.Title = value.Trim(), "subtasks.title", changed);
        SetIfChanged(
            request.Status,
            subtask.Status,
            value => subtask.Status = TaskValues.NormalizeStatus(value),
            "subtasks.status",
            changed);
        if (request.ProgressPercent is not null)
        {
            var progress = Math.Clamp(request.ProgressPercent.Value, 0, 100);
            if (progress != subtask.ProgressPercent)
            {
                subtask.ProgressPercent = progress;
                changed.Add("subtasks.progress_percent");
            }
        }

        if (changed.Count == 0)
        {
            return ToDto(subtask);
        }

        var actor = NormalizeActor(request.Actor, user);
        var now = DateTime.UtcNow;
        subtask.UpdatedAtUtc = now;
        task.UpdatedAtUtc = now;
        task.UpdatedBy = actor.ActorName;
        task.UpdatedByUserId = user.Id;
        changed.AddRange(new[] { "updated_at", "updated_by" });
        AddAudit(task, actor, "update_subtask", changed, before, Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(subtask);
    }

    public async Task<AttachmentDto?> AddAttachmentAsync(
        string workspaceId,
        string taskId,
        UserEntity user,
        AddAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        var task = await FullTaskQuery(workspaceId).FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null || task.IsDeleted)
        {
            return null;
        }

        var actor = NormalizeActor(request.Actor, user);
        var before = Snapshot(task);
        var now = DateTime.UtcNow;
        var attachment = new AttachmentEntity
        {
            TaskId = task.Id,
            Type = NormalizeAttachmentType(request.Type),
            Title = NormalizeValue(request.Title, "Attachment"),
            Target = request.Target.Trim(),
            Note = request.Note.Trim(),
            CreatedAtUtc = now
        };
        task.Attachments.Add(attachment);
        task.UpdatedAtUtc = now;
        task.UpdatedBy = actor.ActorName;
        task.UpdatedByUserId = user.Id;
        AddAudit(
            task,
            actor,
            "add_attachment",
            new[] { "attachments", "updated_at", "updated_by" },
            before,
            Snapshot(task));
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(attachment);
    }

    public async Task<List<AuditDto>> GetAuditsAsync(
        string workspaceId,
        string taskId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Audits
            .AsNoTracking()
            .Where(audit => audit.TaskId == taskId && audit.Task!.WorkspaceId == workspaceId)
            .OrderByDescending(audit => audit.TimestampUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FocusDto> GetFocusAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var tasks = await FullTaskQuery(workspaceId)
            .Where(task => !task.IsDeleted)
            .OrderByDescending(task => task.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var dueThreshold = now.Date.AddDays(2);

        return new FocusDto(
            tasks.Where(task => task.IsPinned || task.Status is TaskValues.InProgress or TaskValues.Review)
                .OrderByDescending(task => task.IsPinned)
                .ThenByDescending(task => task.UpdatedAtUtc)
                .Take(12)
                .Select(ToDto)
                .ToList(),
            tasks.Where(task => task.Status == TaskValues.Blocked || DeserializeStrings(task.BlockedByTaskIdsJson).Count > 0)
                .Take(12)
                .Select(ToDto)
                .ToList(),
            tasks.Where(task => task.DueDateUtc is not null && task.DueDateUtc.Value.Date <= dueThreshold)
                .OrderBy(task => task.DueDateUtc)
                .Take(12)
                .Select(ToDto)
                .ToList(),
            tasks.Take(12).Select(ToDto).ToList());
    }

    private IQueryable<TaskEntity> FullTaskQuery(string workspaceId)
    {
        return _db.Tasks
            .Where(task => task.WorkspaceId == workspaceId)
            .AsSplitQuery()
            .Include(task => task.Subtasks)
            .Include(task => task.Logs)
            .Include(task => task.Attachments);
    }

    private static List<string> ApplyUpdate(TaskEntity task, UpdateTaskRequest request)
    {
        var changed = new List<string>();
        SetIfChanged(request.Title, task.Title, value => task.Title = value.Trim(), "title", changed);
        SetIfChanged(request.Notes, task.Notes, value => task.Notes = value.Trim(), "notes", changed);
        SetIfChanged(
            request.Status,
            task.Status,
            value => task.Status = TaskValues.NormalizeStatus(value),
            "status",
            changed);
        SetIfChanged(
            request.Priority,
            task.Priority,
            value => task.Priority = TaskValues.NormalizePriority(value),
            "priority",
            changed);

        if (request.ProgressPercent is not null)
        {
            var progress = Math.Clamp(request.ProgressPercent.Value, 0, 100);
            if (progress != task.ProgressPercent)
            {
                task.ProgressPercent = progress;
                changed.Add("progress_percent");
            }
        }

        SetIfChanged(
            request.ProjectId,
            task.ProjectId,
            value => task.ProjectId = NormalizeValue(value, "default"),
            "project_id",
            changed);
        SetIfChanged(
            request.ListId,
            task.ListId,
            value => task.ListId = NormalizeValue(value, "main"),
            "list_id",
            changed);
        SetIfChanged(request.Assignee, task.Assignee, value => task.Assignee = value.Trim(), "assignee", changed);
        SetIfChanged(request.Estimate, task.Estimate, value => task.Estimate = value.Trim(), "estimate", changed);

        if (request.Tags is not null)
        {
            SetJsonIfChanged(SerializeStrings(request.Tags), task.TagsJson, value => task.TagsJson = value, "tags", changed);
        }

        if (request.BlockedByTaskIds is not null)
        {
            SetJsonIfChanged(
                SerializeStrings(request.BlockedByTaskIds),
                task.BlockedByTaskIdsJson,
                value => task.BlockedByTaskIdsJson = value,
                "blocked_by_task_ids",
                changed);
        }

        SetDate(request.StartDate, request.ClearStartDate, task.StartDateUtc, value => task.StartDateUtc = value, "start_date", changed);
        SetDate(request.DueDate, request.ClearDueDate, task.DueDateUtc, value => task.DueDateUtc = value, "due_date", changed);

        if (request.IsPinned is not null && request.IsPinned.Value != task.IsPinned)
        {
            task.IsPinned = request.IsPinned.Value;
            changed.Add("is_pinned");
        }

        return changed;
    }

    private void AddAudit(
        TaskEntity task,
        ActorDto actor,
        string action,
        IEnumerable<string> changedFields,
        object? before,
        object after)
    {
        task.Audits.Add(new AuditEntity
        {
            TaskId = task.Id,
            ActorType = actor.ActorType,
            ActorName = actor.ActorName,
            Source = actor.Source,
            Action = action,
            TimestampUtc = DateTime.UtcNow,
            ChangedFieldsJson = JsonSerializer.Serialize(changedFields.Distinct().ToArray(), JsonOptions),
            BeforeJson = JsonSerializer.Serialize(before ?? new { }, JsonOptions),
            AfterJson = JsonSerializer.Serialize(after, JsonOptions)
        });
    }

    private static object Snapshot(TaskEntity task)
    {
        return new
        {
            task.Id,
            task.Title,
            task.Notes,
            task.Status,
            task.Priority,
            task.ProgressPercent,
            task.ProjectId,
            task.ListId,
            task.Assignee,
            Tags = DeserializeStrings(task.TagsJson),
            task.Estimate,
            StartDate = ToOffset(task.StartDateUtc),
            DueDate = ToOffset(task.DueDateUtc),
            BlockedByTaskIds = DeserializeStrings(task.BlockedByTaskIdsJson),
            task.IsPinned,
            task.IsDeleted,
            task.UpdatedAtUtc,
            task.UpdatedBy,
            SubtasksCount = task.Subtasks.Count,
            LogsCount = task.Logs.Count,
            AttachmentsCount = task.Attachments.Count
        };
    }

    private static TaskDto ToDto(TaskEntity task)
    {
        return new TaskDto(
            task.Id,
            task.WorkspaceId,
            task.ShortId,
            task.Title,
            task.Notes,
            task.Status,
            task.Priority,
            task.ProgressPercent,
            task.ProjectId,
            task.ListId,
            task.Assignee,
            DeserializeStrings(task.TagsJson),
            task.Estimate,
            ToOffset(task.StartDateUtc),
            ToOffset(task.DueDateUtc),
            DeserializeStrings(task.BlockedByTaskIdsJson),
            task.IsPinned,
            task.IsDeleted,
            ToOffset(task.CreatedAtUtc)!.Value,
            ToOffset(task.UpdatedAtUtc)!.Value,
            task.CreatedBy,
            task.UpdatedBy,
            task.Subtasks.OrderByDescending(item => item.UpdatedAtUtc).Select(ToDto).ToList(),
            task.Logs.OrderByDescending(item => item.CreatedAtUtc).Select(ToDto).ToList(),
            task.Attachments.OrderByDescending(item => item.CreatedAtUtc).Select(ToDto).ToList());
    }

    private static SubtaskDto ToDto(SubtaskEntity subtask)
    {
        return new SubtaskDto(
            subtask.Id,
            subtask.Title,
            subtask.Status,
            subtask.ProgressPercent,
            ToOffset(subtask.CreatedAtUtc)!.Value,
            ToOffset(subtask.UpdatedAtUtc)!.Value);
    }

    private static TaskLogDto ToDto(TaskLogEntity log)
    {
        return new TaskLogDto(
            log.Id,
            log.ActorType,
            log.ActorName,
            log.Message,
            ToOffset(log.CreatedAtUtc)!.Value);
    }

    private static AttachmentDto ToDto(AttachmentEntity attachment)
    {
        return new AttachmentDto(
            attachment.Id,
            attachment.Type,
            attachment.Title,
            attachment.Target,
            attachment.Note,
            ToOffset(attachment.CreatedAtUtc)!.Value);
    }

    private static AuditDto ToDto(AuditEntity audit)
    {
        return new AuditDto(
            audit.OperationId,
            audit.ActorType,
            audit.ActorName,
            audit.Source,
            audit.Action,
            audit.TaskId,
            ToOffset(audit.TimestampUtc)!.Value,
            DeserializeStrings(audit.ChangedFieldsJson),
            DeserializeObject(audit.BeforeJson),
            DeserializeObject(audit.AfterJson));
    }

    private static ActorDto NormalizeActor(ActorDto? actor, UserEntity user)
    {
        return new ActorDto(
            NormalizeValue(actor?.ActorName, user.DisplayName),
            NormalizeValue(actor?.ActorType, "user").ToLowerInvariant(),
            actor?.Source?.Trim() ?? "");
    }

    private static string NormalizeValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeAttachmentType(string? type)
    {
        return type?.Trim().ToLowerInvariant() == "file" ? "file" : "url";
    }

    private static string SerializeStrings(IEnumerable<string>? values)
    {
        var normalized = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static List<string> DeserializeStrings(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
    }

    private static object DeserializeObject(string json)
    {
        return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
    }

    private static DateTimeOffset? ToOffset(DateTime? utc)
    {
        return utc is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));
    }

    private static void SetIfChanged(
        string? requested,
        string current,
        Action<string> setter,
        string field,
        ICollection<string> changed)
    {
        if (requested is null)
        {
            return;
        }

        var before = current;
        setter(requested);
        if (!string.Equals(before, requested.Trim(), StringComparison.Ordinal))
        {
            changed.Add(field);
        }
    }

    private static void SetJsonIfChanged(
        string requested,
        string current,
        Action<string> setter,
        string field,
        ICollection<string> changed)
    {
        if (string.Equals(requested, current, StringComparison.Ordinal))
        {
            return;
        }

        setter(requested);
        changed.Add(field);
    }

    private static void SetDate(
        DateTimeOffset? requested,
        bool clear,
        DateTime? current,
        Action<DateTime?> setter,
        string field,
        ICollection<string> changed)
    {
        var next = clear ? null : requested?.UtcDateTime;
        if (!clear && requested is null)
        {
            return;
        }

        if (current == next)
        {
            return;
        }

        setter(next);
        changed.Add(field);
    }

    private static IReadOnlyList<string> CreateChangedFields()
    {
        return new[]
        {
            "title", "notes", "status", "priority", "progress_percent", "project_id", "list_id",
            "assignee", "tags", "estimate", "start_date", "due_date", "blocked_by_task_ids", "is_pinned"
        };
    }
}
