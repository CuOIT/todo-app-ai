using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AiTaskTracker.Domain;

public static class TaskStatuses
{
    public const string Backlog = "backlog";
    public const string Ready = "ready";
    public const string InProgress = "in_progress";
    public const string Blocked = "blocked";
    public const string Review = "review";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public static readonly string[] ClickUpLite =
    {
        Backlog,
        Ready,
        InProgress,
        Blocked,
        Review,
        Done,
        Cancelled
    };

    public static string ToKanbanGroup(string status)
    {
        return status switch
        {
            Ready or Backlog => "todo",
            InProgress or Review or Blocked => "in_progress",
            Done => "done",
            Cancelled => "close",
            _ => "todo"
        };
    }

    public static string FromKanbanGroup(string group)
    {
        return group switch
        {
            "todo" => Backlog,
            "in_progress" => InProgress,
            "done" => Done,
            "close" => Cancelled,
            _ => Backlog
        };
    }

    public static string ToDisplayName(string status)
    {
        return status switch
        {
            Backlog => "TO DO",
            Ready => "READY",
            InProgress => "IN PROGRESS",
            Blocked => "BLOCKED",
            Review => "REVIEW",
            Done => "DONE",
            Cancelled => "CLOSE",
            _ => status.ToUpperInvariant()
        };
    }
}

public sealed class TaskSnapshot
{
    public int SchemaVersion { get; set; } = 2;
    public string ClientId { get; set; } = Environment.MachineName;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ProjectInfo> Projects { get; set; } = new();
    public List<TaskListInfo> Lists { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
}

public sealed record ProjectInfo(string Id, string Name);

public sealed record TaskListInfo(string Id, string ProjectId, string Name);

public sealed class TaskItem : ObservableEntity
{
    private string _title = "";
    private string _notes = "";
    private string _status = TaskStatuses.Backlog;
    private string _priority = "med";
    private int _progressPercent;
    private string _projectId = "default";
    private string _listId = "main";
    private string _estimate = "";
    private string _assignee = "";
    private string _createdBy = "user";
    private string _updatedBy = "user";
    private bool _isPinned;
    private bool _isDeleted;
    private DateTimeOffset? _startDate;
    private DateTimeOffset? _dueDate;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ShortId { get; set; } = $"TASK-{Random.Shared.Next(1000, 9999)}";

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, NormalizeStatus(value)))
            {
                OnPropertyChanged(nameof(KanbanStatus));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(StatusOrder));
                OnPropertyChanged(nameof(IsDone));
                OnPropertyChanged(nameof(FocusBadges));
            }
        }
    }

    public string Priority
    {
        get => _priority;
        set => SetField(ref _priority, NormalizePriority(value));
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, Math.Clamp(value, 0, 100));
    }

    public string ProjectId
    {
        get => _projectId;
        set => SetField(ref _projectId, string.IsNullOrWhiteSpace(value) ? "default" : value.Trim());
    }

    public string ListId
    {
        get => _listId;
        set => SetField(ref _listId, string.IsNullOrWhiteSpace(value) ? "main" : value.Trim());
    }

    public ObservableCollection<string> Tags { get; set; } = new();

    public string Estimate
    {
        get => _estimate;
        set => SetField(ref _estimate, value.Trim());
    }

    public string Assignee
    {
        get => _assignee;
        set => SetField(ref _assignee, string.IsNullOrWhiteSpace(value) ? "" : value.Trim());
    }

    public DateTimeOffset? StartDate
    {
        get => _startDate;
        set => SetField(ref _startDate, value);
    }

    public DateTimeOffset? DueDate
    {
        get => _dueDate;
        set
        {
            if (SetField(ref _dueDate, value))
            {
                OnPropertyChanged(nameof(DueDateLocal));
                OnPropertyChanged(nameof(DueDateText));
                OnPropertyChanged(nameof(FocusBadges));
            }
        }
    }

    public ObservableCollection<string> BlockedByTaskIds { get; set; } = new();
    public ObservableCollection<TaskAttachment> Attachments { get; set; } = new();
    public ObservableCollection<SubtaskItem> Subtasks { get; set; } = new();
    public ObservableCollection<TaskLog> Logs { get; set; } = new();

    public string CreatedBy
    {
        get => _createdBy;
        set => SetField(ref _createdBy, string.IsNullOrWhiteSpace(value) ? "user" : value.Trim());
    }

    public string UpdatedBy
    {
        get => _updatedBy;
        set => SetField(ref _updatedBy, string.IsNullOrWhiteSpace(value) ? "user" : value.Trim());
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetField(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(FocusBadges));
            }
        }
    }

    public bool IsDeleted
    {
        get => _isDeleted;
        set => SetField(ref _isDeleted, value);
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (SetField(ref _updatedAt, value))
            {
                OnPropertyChanged(nameof(UpdatedAtLocal));
                OnPropertyChanged(nameof(FocusBadges));
            }
        }
    }

    [JsonIgnore]
    public bool IsDone
    {
        get => Status is TaskStatuses.Done or TaskStatuses.Cancelled;
        set
        {
            Status = value ? TaskStatuses.Done : TaskStatuses.InProgress;
            ProgressPercent = value ? 100 : Math.Min(ProgressPercent, 95);
        }
    }

    [JsonIgnore]
    public string KanbanStatus => TaskStatuses.ToKanbanGroup(Status);

    [JsonIgnore]
    public string StatusDisplay => TaskStatuses.ToDisplayName(Status);

    [JsonIgnore]
    public int StatusOrder => Status switch
    {
        TaskStatuses.InProgress => 0,
        TaskStatuses.Done => 1,
        TaskStatuses.Backlog => 2,
        TaskStatuses.Ready => 3,
        TaskStatuses.Review => 4,
        TaskStatuses.Blocked => 5,
        TaskStatuses.Cancelled => 6,
        _ => 9
    };

    [JsonIgnore]
    public string UpdatedAtLocal => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public string DueDateLocal => DueDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";

    [JsonIgnore]
    public string DueDateText
    {
        get => DueDateLocal;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                DueDate = null;
                return;
            }

            if (DateTimeOffset.TryParse(value.Trim(), out var parsed))
            {
                DueDate = parsed;
            }
        }
    }

    [JsonIgnore]
    public string TagsText
    {
        get => string.Join(", ", Tags);
        set
        {
            Tags.Clear();
            foreach (var tag in SplitCsv(value))
            {
                Tags.Add(tag);
            }
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string FocusBadges
    {
        get
        {
            var badges = new List<string>();
            if (IsPinned)
            {
                badges.Add("Pinned");
            }
            if (Status == TaskStatuses.Blocked || BlockedByTaskIds.Count > 0)
            {
                badges.Add("Blocked");
            }
            if (DueDate is not null && DueDate.Value.Date <= DateTimeOffset.Now.Date.AddDays(2))
            {
                badges.Add(DueDate.Value.Date < DateTimeOffset.Now.Date ? "Overdue" : "Due");
            }
            if (UpdatedAt >= DateTimeOffset.UtcNow.AddHours(-24))
            {
                badges.Add("Recent");
            }
            return string.Join(" | ", badges);
        }
    }

    public void Touch(string actor)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = actor;
    }

    public void NormalizeCollections()
    {
        Tags ??= new ObservableCollection<string>();
        BlockedByTaskIds ??= new ObservableCollection<string>();
        Attachments ??= new ObservableCollection<TaskAttachment>();
        Subtasks ??= new ObservableCollection<SubtaskItem>();
        Logs ??= new ObservableCollection<TaskLog>();
        Status = NormalizeStatus(Status);
        Priority = NormalizePriority(Priority);
    }

    private static string NormalizeStatus(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? TaskStatuses.Backlog : value.Trim().ToLowerInvariant();
        return TaskStatuses.ClickUpLite.Contains(normalized) ? normalized : TaskStatuses.Backlog;
    }

    private static string NormalizePriority(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "med"
            : value.Trim().ToLowerInvariant() switch
            {
                "emergency" or "emergent" or "emergen" or "urgent" => "emergen",
                "hi" or "high" => "high",
                "medium" or "med" => "med",
                "lo" or "low" => "low",
                _ => "med"
            };
    }

    private static IEnumerable<string> SplitCsv(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class SubtaskItem : ObservableEntity
{
    private string _title = "";
    private string _status = TaskStatuses.Backlog;
    private int _progressPercent;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, Math.Clamp(value, 0, 100));
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        set => SetField(ref _updatedAt, value);
    }

    [JsonIgnore]
    public bool IsDone
    {
        get => Status == TaskStatuses.Done;
        set
        {
            Status = value ? TaskStatuses.Done : TaskStatuses.InProgress;
            ProgressPercent = value ? 100 : Math.Min(ProgressPercent, 95);
        }
    }

    public override string ToString()
    {
        return $"{Status} | {ProgressPercent}% | {Title}";
    }
}

public sealed class TaskAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "url";
    public string Title { get; set; } = "";
    public string Target { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public override string ToString()
    {
        var label = string.IsNullOrWhiteSpace(Title) ? Target : Title;
        return $"{Type}: {label}";
    }
}

public sealed class TaskLog
{
    public TaskLog()
    {
    }

    public TaskLog(string actorType, string actorName, string message)
    {
        ActorType = actorType;
        ActorName = actorName;
        Message = message;
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ActorType { get; set; } = "user";
    public string ActorName { get; set; } = "User";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public override string ToString()
    {
        return $"{CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm} | {ActorName}: {Message}";
    }
}

public sealed class AuditOperation
{
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
    public string ActorType { get; set; } = "user";
    public string ActorName { get; set; } = "User";
    public string Action { get; set; } = "";
    public string TaskId { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public List<string> ChangedFields { get; set; } = new();
    public Dictionary<string, object?> Before { get; set; } = new();
    public Dictionary<string, object?> After { get; set; } = new();
}

public abstract class ObservableEntity : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
