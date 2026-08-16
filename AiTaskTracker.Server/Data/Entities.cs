namespace AiTaskTracker.Server.Data;

public sealed class UserEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<WorkspaceMemberEntity> Memberships { get; set; } = new();
    public List<SessionEntity> Sessions { get; set; } = new();
}

public sealed class WorkspaceEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<WorkspaceMemberEntity> Members { get; set; } = new();
    public List<TaskEntity> Tasks { get; set; } = new();
}

public sealed class WorkspaceMemberEntity
{
    public string WorkspaceId { get; set; } = "";
    public WorkspaceEntity? Workspace { get; set; }
    public string UserId { get; set; } = "";
    public UserEntity? User { get; set; }
    public string Role { get; set; } = "member";
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SessionEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TokenHash { get; set; } = "";
    public string UserId { get; set; } = "";
    public UserEntity? User { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class TaskEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string WorkspaceId { get; set; } = "";
    public WorkspaceEntity? Workspace { get; set; }
    public string ShortId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = TaskValues.Backlog;
    public string Priority { get; set; } = TaskValues.Medium;
    public int ProgressPercent { get; set; }
    public string ProjectId { get; set; } = "default";
    public string ListId { get; set; } = "main";
    public string Assignee { get; set; } = "";
    public string TagsJson { get; set; } = "[]";
    public string Estimate { get; set; } = "";
    public DateTime? StartDateUtc { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public string BlockedByTaskIdsJson { get; set; } = "[]";
    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "User";
    public string UpdatedBy { get; set; } = "User";
    public string CreatedByUserId { get; set; } = "";
    public string UpdatedByUserId { get; set; } = "";
    public List<SubtaskEntity> Subtasks { get; set; } = new();
    public List<TaskLogEntity> Logs { get; set; } = new();
    public List<AttachmentEntity> Attachments { get; set; } = new();
    public List<AuditEntity> Audits { get; set; } = new();
}

public sealed class SubtaskEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = "";
    public TaskEntity? Task { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = TaskValues.Backlog;
    public int ProgressPercent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TaskLogEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = "";
    public TaskEntity? Task { get; set; }
    public string ActorType { get; set; } = "user";
    public string ActorName { get; set; } = "User";
    public string Message { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AttachmentEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = "";
    public TaskEntity? Task { get; set; }
    public string Type { get; set; } = "url";
    public string Title { get; set; } = "";
    public string Target { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditEntity
{
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = "";
    public TaskEntity? Task { get; set; }
    public string ActorType { get; set; } = "user";
    public string ActorName { get; set; } = "User";
    public string Source { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ChangedFieldsJson { get; set; } = "[]";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
}

public static class TaskValues
{
    public const string Backlog = "backlog";
    public const string Ready = "ready";
    public const string InProgress = "in_progress";
    public const string Blocked = "blocked";
    public const string Review = "review";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public const string Emergency = "emergen";
    public const string High = "high";
    public const string Medium = "med";
    public const string Low = "low";

    public static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        Backlog, Ready, InProgress, Blocked, Review, Done, Cancelled
    };

    public static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        Emergency, High, Medium, Low
    };

    public static string NormalizeStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? Backlog;
        return Statuses.Contains(normalized) ? normalized : Backlog;
    }

    public static string NormalizePriority(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "emergency" or "emergent" or "emergen" or "urgent" => Emergency,
            "hi" or "high" => High,
            "medium" or "med" => Medium,
            "lo" or "low" => Low,
            _ => Medium
        };
    }
}
