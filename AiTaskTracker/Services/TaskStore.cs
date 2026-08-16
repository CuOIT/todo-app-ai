using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiTaskTracker.Domain;

namespace AiTaskTracker.Services;

public sealed class TaskStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy(),
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly JsonSerializerOptions _legacyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TaskStore(string? dataDirectory = null)
    {
        var configuredDirectory = string.IsNullOrWhiteSpace(dataDirectory)
            ? Environment.GetEnvironmentVariable("AITASKTRACKER_DATA_DIR")
            : dataDirectory;
        DataDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AiTaskTracker")
            : configuredDirectory;
        SnapshotPath = Path.Combine(DataDirectory, "snapshot.json");
        OperationsPath = Path.Combine(DataDirectory, "operations.jsonl");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
    public string SnapshotPath { get; }
    public string OperationsPath { get; }

    public TaskSnapshot Load()
    {
        if (!File.Exists(SnapshotPath))
        {
            var seeded = CreateSeedSnapshot();
            Save(seeded);
            return seeded;
        }

        var json = File.ReadAllText(SnapshotPath);
        var snapshot = TryReadSnapshot(json) ?? MigrateLegacyTasks(json) ?? CreateSeedSnapshot();
        Normalize(snapshot);
        if (snapshot.SchemaVersion < 2)
        {
            snapshot.SchemaVersion = 2;
            Save(snapshot);
        }
        return snapshot;
    }

    public void Save(TaskSnapshot snapshot)
    {
        Normalize(snapshot);
        snapshot.SavedAt = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        File.WriteAllText(SnapshotPath, json);
    }

    public TaskItem CreateTask(
        TaskSnapshot snapshot,
        string title,
        string actorName,
        string actorType = "user",
        string priority = "med",
        string status = TaskStatuses.Backlog,
        int progressPercent = 0,
        string notes = "",
        string projectId = "default",
        string listId = "main")
    {
        var task = new TaskItem
        {
            Title = title.Trim(),
            Notes = notes.Trim(),
            Priority = priority,
            Status = status,
            ProgressPercent = progressPercent,
            ProjectId = projectId,
            ListId = listId,
            CreatedBy = actorName,
            UpdatedBy = actorName
        };
        task.Logs.Insert(0, new TaskLog(actorType, actorName, "Task created."));
        snapshot.Tasks.Insert(0, task);
        Save(snapshot);
        AppendAudit(new AuditOperation
        {
            ActorType = actorType,
            ActorName = actorName,
            Action = "create_task",
            TaskId = task.Id,
            ChangedFields = new List<string> { "title", "status", "priority", "progress_percent" },
            After = ToAuditDictionary(task)
        });
        return task;
    }

    public bool SoftDeleteTask(TaskSnapshot snapshot, TaskItem task, string actorName, string actorType = "user")
    {
        var before = ToAuditDictionary(task);
        task.IsDeleted = true;
        task.Touch(actorName);
        Save(snapshot);
        AppendAudit(new AuditOperation
        {
            ActorType = actorType,
            ActorName = actorName,
            Action = "delete_task",
            TaskId = task.Id,
            ChangedFields = new List<string> { "is_deleted" },
            Before = before,
            After = ToAuditDictionary(task)
        });
        return true;
    }

    public void AddLog(TaskSnapshot snapshot, TaskItem task, string message, string actorName, string actorType = "user")
    {
        var before = ToAuditDictionary(task);
        task.Logs.Insert(0, new TaskLog(actorType, actorName, message.Trim()));
        task.Touch(actorName);
        Save(snapshot);
        AppendAudit(new AuditOperation
        {
            ActorType = actorType,
            ActorName = actorName,
            Action = "add_task_log",
            TaskId = task.Id,
            ChangedFields = new List<string> { "logs", "updated_at", "updated_by" },
            Before = before,
            After = ToAuditDictionary(task)
        });
    }

    public SubtaskItem AddSubtask(TaskSnapshot snapshot, TaskItem task, string title, string actorName, string actorType = "user")
    {
        var before = ToAuditDictionary(task);
        var subtask = new SubtaskItem
        {
            Title = title.Trim(),
            Status = TaskStatuses.Backlog
        };
        task.Subtasks.Insert(0, subtask);
        task.Touch(actorName);
        Save(snapshot);
        AppendAudit(new AuditOperation
        {
            ActorType = actorType,
            ActorName = actorName,
            Action = "add_subtask",
            TaskId = task.Id,
            ChangedFields = new List<string> { "subtasks", "updated_at", "updated_by" },
            Before = before,
            After = ToAuditDictionary(task)
        });
        return subtask;
    }

    public TaskAttachment AddAttachment(
        TaskSnapshot snapshot,
        TaskItem task,
        string target,
        string title,
        string note,
        string actorName,
        string actorType = "user")
    {
        var before = ToAuditDictionary(task);
        var attachment = new TaskAttachment
        {
            Target = target.Trim(),
            Title = title.Trim(),
            Note = note.Trim(),
            Type = Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile ? "url" : "file"
        };
        task.Attachments.Insert(0, attachment);
        task.Touch(actorName);
        Save(snapshot);
        AppendAudit(new AuditOperation
        {
            ActorType = actorType,
            ActorName = actorName,
            Action = "add_attachment",
            TaskId = task.Id,
            ChangedFields = new List<string> { "attachments", "updated_at", "updated_by" },
            Before = before,
            After = ToAuditDictionary(task)
        });
        return attachment;
    }

    public void UpdateTask(
        TaskSnapshot snapshot,
        TaskItem task,
        string actorName,
        string actorType,
        IEnumerable<string> changedFields,
        Dictionary<string, object?> before)
    {
        var fields = changedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (fields.Count == 0)
        {
            return;
        }

        task.Touch(actorName);
        Save(snapshot);
        AppendAudit(new AuditOperation
        {
            ActorType = actorType,
            ActorName = actorName,
            Action = "update_task",
            TaskId = task.Id,
            ChangedFields = fields,
            Before = before,
            After = ToAuditDictionary(task)
        });
    }

    public List<TaskItem> ActiveTasks(TaskSnapshot snapshot)
    {
        return snapshot.Tasks
            .Where(task => !task.IsDeleted)
            .OrderByDescending(task => task.IsPinned)
            .ThenBy(task => StatusRank(task.Status))
            .ThenByDescending(task => task.UpdatedAt)
            .ToList();
    }

    public FocusBoard BuildFocus(TaskSnapshot snapshot)
    {
        var tasks = ActiveTasks(snapshot);
        var today = DateTimeOffset.Now.Date;
        return new FocusBoard
        {
            Now = tasks
                .Where(task => task.IsPinned || task.Status == TaskStatuses.InProgress || task.Status == TaskStatuses.Review)
                .OrderByDescending(task => task.IsPinned)
                .ThenByDescending(task => task.UpdatedAt)
                .Take(12)
                .ToList(),
            Blocked = tasks
                .Where(task => task.Status == TaskStatuses.Blocked || task.BlockedByTaskIds.Count > 0)
                .OrderByDescending(task => task.UpdatedAt)
                .Take(12)
                .ToList(),
            Due = tasks
                .Where(task => task.DueDate is not null && task.DueDate.Value.Date <= today.AddDays(2))
                .OrderBy(task => task.DueDate)
                .Take(12)
                .ToList(),
            Recent = tasks
                .OrderByDescending(task => task.UpdatedAt)
                .Take(12)
                .ToList()
        };
    }

    public static Dictionary<string, object?> ToAuditDictionary(TaskItem task)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = task.Id,
            ["title"] = task.Title,
            ["notes"] = task.Notes,
            ["status"] = task.Status,
            ["priority"] = task.Priority,
            ["progress_percent"] = task.ProgressPercent,
            ["project_id"] = task.ProjectId,
            ["list_id"] = task.ListId,
            ["assignee"] = task.Assignee,
            ["tags"] = task.Tags.ToArray(),
            ["estimate"] = task.Estimate,
            ["start_date"] = task.StartDate,
            ["due_date"] = task.DueDate,
            ["blocked_by_task_ids"] = task.BlockedByTaskIds.ToArray(),
            ["attachments_count"] = task.Attachments.Count,
            ["subtasks_count"] = task.Subtasks.Count,
            ["logs_count"] = task.Logs.Count,
            ["updated_at"] = task.UpdatedAt,
            ["updated_by"] = task.UpdatedBy,
            ["is_deleted"] = task.IsDeleted
        };
    }

    public void AppendAudit(AuditOperation operation)
    {
        var line = JsonSerializer.Serialize(operation, _jsonOptions);
        File.AppendAllText(OperationsPath, line + Environment.NewLine);
    }

    private TaskSnapshot? TryReadSnapshot(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TaskSnapshot>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private TaskSnapshot? MigrateLegacyTasks(string json)
    {
        try
        {
            var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json, _legacyJsonOptions);
            if (tasks is null)
            {
                return null;
            }

            return new TaskSnapshot
            {
                SchemaVersion = 1,
                Tasks = tasks
            };
        }
        catch
        {
            return null;
        }
    }

    private TaskSnapshot CreateSeedSnapshot()
    {
        var snapshot = new TaskSnapshot();
        Normalize(snapshot);
        return snapshot;
    }

    private static void Normalize(TaskSnapshot snapshot)
    {
        snapshot.Projects ??= new List<ProjectInfo>();
        snapshot.Lists ??= new List<TaskListInfo>();
        snapshot.Tasks ??= new List<TaskItem>();

        if (snapshot.Projects.All(project => project.Id != "default"))
        {
            snapshot.Projects.Insert(0, new ProjectInfo("default", "Personal"));
        }

        if (snapshot.Lists.All(list => list.Id != "main"))
        {
            snapshot.Lists.Insert(0, new TaskListInfo("main", "default", "Main"));
        }

        foreach (var task in snapshot.Tasks)
        {
            task.NormalizeCollections();
        }
    }

    private static int StatusRank(string status)
    {
        return status switch
        {
            TaskStatuses.InProgress => 0,
            TaskStatuses.Blocked => 1,
            TaskStatuses.Review => 2,
            TaskStatuses.Ready => 3,
            TaskStatuses.Backlog => 4,
            TaskStatuses.Done => 5,
            TaskStatuses.Cancelled => 6,
            _ => 9
        };
    }
}

public sealed class FocusBoard
{
    public List<TaskItem> Now { get; set; } = new();
    public List<TaskItem> Blocked { get; set; } = new();
    public List<TaskItem> Due { get; set; } = new();
    public List<TaskItem> Recent { get; set; } = new();
}
