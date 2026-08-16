namespace AiTaskTracker.Server.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string WorkspaceName = "My Team");

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserDto User,
    IReadOnlyList<WorkspaceDto> Workspaces);

public sealed record UserDto(string Id, string Email, string DisplayName);

public sealed record WorkspaceDto(
    string Id,
    string Name,
    string Role,
    string InviteCode,
    DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequest(string Name);

public sealed record JoinWorkspaceRequest(string InviteCode);

public sealed record ActorDto(
    string ActorName = "User",
    string ActorType = "user",
    string Source = "");

public sealed record CreateTaskRequest(
    string Title,
    string Notes = "",
    string Status = "backlog",
    string Priority = "med",
    int ProgressPercent = 0,
    string ProjectId = "default",
    string ListId = "main",
    string Assignee = "",
    IReadOnlyList<string>? Tags = null,
    string Estimate = "",
    DateTimeOffset? StartDate = null,
    DateTimeOffset? DueDate = null,
    IReadOnlyList<string>? BlockedByTaskIds = null,
    bool IsPinned = false,
    ActorDto? Actor = null);

public sealed record UpdateTaskRequest(
    string? Title = null,
    string? Notes = null,
    string? Status = null,
    string? Priority = null,
    int? ProgressPercent = null,
    string? ProjectId = null,
    string? ListId = null,
    string? Assignee = null,
    IReadOnlyList<string>? Tags = null,
    string? Estimate = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? DueDate = null,
    bool ClearStartDate = false,
    bool ClearDueDate = false,
    IReadOnlyList<string>? BlockedByTaskIds = null,
    bool? IsPinned = null,
    ActorDto? Actor = null);

public sealed record AddTaskLogRequest(string Message, ActorDto? Actor = null);

public sealed record AddSubtaskRequest(
    string Title,
    string Status = "backlog",
    int ProgressPercent = 0,
    ActorDto? Actor = null);

public sealed record AddAttachmentRequest(
    string Type,
    string Title,
    string Target,
    string Note = "",
    ActorDto? Actor = null);

public sealed record UpdateSubtaskRequest(
    string? Title = null,
    string? Status = null,
    int? ProgressPercent = null,
    ActorDto? Actor = null);

public sealed record TaskDto(
    string Id,
    string WorkspaceId,
    string ShortId,
    string Title,
    string Notes,
    string Status,
    string Priority,
    int ProgressPercent,
    string ProjectId,
    string ListId,
    string Assignee,
    IReadOnlyList<string> Tags,
    string Estimate,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    IReadOnlyList<string> BlockedByTaskIds,
    bool IsPinned,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CreatedBy,
    string UpdatedBy,
    IReadOnlyList<SubtaskDto> Subtasks,
    IReadOnlyList<TaskLogDto> Logs,
    IReadOnlyList<AttachmentDto> Attachments);

public sealed record SubtaskDto(
    string Id,
    string Title,
    string Status,
    int ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaskLogDto(
    string Id,
    string ActorType,
    string ActorName,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record AttachmentDto(
    string Id,
    string Type,
    string Title,
    string Target,
    string Note,
    DateTimeOffset CreatedAt);

public sealed record AuditDto(
    string OperationId,
    string ActorType,
    string ActorName,
    string Source,
    string Action,
    string TaskId,
    DateTimeOffset Timestamp,
    IReadOnlyList<string> ChangedFields,
    object Before,
    object After);

public sealed record FocusDto(
    IReadOnlyList<TaskDto> Now,
    IReadOnlyList<TaskDto> Blocked,
    IReadOnlyList<TaskDto> Due,
    IReadOnlyList<TaskDto> Recent);
