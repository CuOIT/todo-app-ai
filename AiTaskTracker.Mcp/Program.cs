using System.Text.Json;
using System.Text.Json.Nodes;
using AiTaskTracker.Domain;
using AiTaskTracker.Services;

var server = new McpServer();
await server.RunAsync();

internal sealed class McpServer
{
    private readonly TaskStore _store = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy(),
        WriteIndented = false
    };

    public async Task RunAsync()
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var request = JsonNode.Parse(line)?.AsObject();
                if (request is null)
                {
                    continue;
                }

                var response = Handle(request);
                if (response is not null)
                {
                    await Console.Out.WriteLineAsync(response.ToJsonString(_jsonOptions));
                    await Console.Out.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    error = new { code = -32000, message = ex.Message },
                    id = (string?)null
                }, _jsonOptions));
            }
        }
    }

    private JsonObject? Handle(JsonObject request)
    {
        var id = CloneNode(request["id"]);
        var method = request["method"]?.GetValue<string>() ?? "";
        return method switch
        {
            "initialize" => Result(id, new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "ai-task-tracker",
                    version = "0.1.0"
                }
            }),
            "notifications/initialized" => null,
            "tools/list" => Result(id, new { tools = ToolDefinitions() }),
            "tools/call" => Result(id, CallTool(request["params"]?.AsObject())),
            _ => Error(id, -32601, $"Unknown method: {method}")
        };
    }

    private object CallTool(JsonObject? parameters)
    {
        var name = parameters?["name"]?.GetValue<string>() ?? "";
        var arguments = parameters?["arguments"]?.AsObject() ?? new JsonObject();
        var result = name switch
        {
            "create_task" => CreateTask(arguments),
            "update_task" => UpdateTask(arguments),
            "delete_task" => DeleteTask(arguments),
            "list_tasks" => ListTasks(arguments),
            "get_task" => GetTask(arguments),
            "add_task_log" => AddTaskLog(arguments),
            "add_subtask" => AddSubtask(arguments),
            "update_subtask" => UpdateSubtask(arguments),
            "get_today_focus" => GetTodayFocus(),
            _ => throw new InvalidOperationException($"Unknown tool: {name}")
        };

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(result, _jsonOptions)
                }
            }
        };
    }

    private object CreateTask(JsonObject args)
    {
        var snapshot = _store.Load();
        var actor = Actor(args);
        var task = _store.CreateTask(
            snapshot,
            Required(args, "title"),
            actor.Name,
            actor.Type,
            Optional(args, "priority", "medium"),
            Optional(args, "status", TaskStatuses.Backlog),
            OptionalInt(args, "progress_percent", 0),
            Optional(args, "notes", ""),
            Optional(args, "project_id", "default"),
            Optional(args, "list_id", "main"));
        var assignee = Optional(args, "assignee", "");
        if (!string.IsNullOrWhiteSpace(assignee))
        {
            var before = TaskStore.ToAuditDictionary(task);
            task.Assignee = assignee;
            _store.UpdateTask(snapshot, task, actor.Name, actor.Type, new[] { "assignee" }, before);
        }

        if (args.TryGetPropertyValue("due_date", out var dueNode) && DateTimeOffset.TryParse(dueNode?.GetValue<string>(), out var dueDate))
        {
            var before = TaskStore.ToAuditDictionary(task);
            task.DueDate = dueDate;
            _store.UpdateTask(snapshot, task, actor.Name, actor.Type, new[] { "due_date" }, before);
        }

        return TaskDto(task);
    }

    private object UpdateTask(JsonObject args)
    {
        var snapshot = _store.Load();
        var actor = Actor(args);
        var task = FindTask(snapshot, Required(args, "task_id"));
        var before = TaskStore.ToAuditDictionary(task);
        var changed = new List<string>();

        SetString(args, "title", value => task.Title = value, changed);
        SetString(args, "notes", value => task.Notes = value, changed);
        SetString(args, "status", value => task.Status = value, changed);
        SetString(args, "priority", value => task.Priority = value, changed);
        SetString(args, "project_id", value => task.ProjectId = value, changed);
        SetString(args, "list_id", value => task.ListId = value, changed);
        SetString(args, "assignee", value => task.Assignee = value, changed);
        SetString(args, "estimate", value => task.Estimate = value, changed);
        SetInt(args, "progress_percent", value => task.ProgressPercent = value, changed);
        SetDate(args, "due_date", value => task.DueDate = value, changed);

        if (args.TryGetPropertyValue("tags", out var tagsNode) && tagsNode is JsonArray tags)
        {
            task.Tags.Clear();
            foreach (var tag in tags.Select(item => item?.GetValue<string>()).Where(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                task.Tags.Add(tag!);
            }
            changed.Add("tags");
        }

        if (args.TryGetPropertyValue("blocked_by_task_ids", out var blockedByNode) && blockedByNode is JsonArray blockers)
        {
            task.BlockedByTaskIds.Clear();
            foreach (var blocker in blockers.Select(item => item?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                task.BlockedByTaskIds.Add(blocker!);
            }
            changed.Add("blocked_by_task_ids");
        }

        _store.UpdateTask(snapshot, task, actor.Name, actor.Type, changed, before);
        return TaskDto(task);
    }

    private object DeleteTask(JsonObject args)
    {
        var snapshot = _store.Load();
        var actor = Actor(args);
        var task = FindTask(snapshot, Required(args, "task_id"));
        _store.SoftDeleteTask(snapshot, task, actor.Name, actor.Type);
        return new { deleted = true, task_id = task.Id, short_id = task.ShortId };
    }

    private object ListTasks(JsonObject args)
    {
        var snapshot = _store.Load();
        var status = Optional(args, "status", "");
        var query = Optional(args, "query", "");
        var tasks = _store.ActiveTasks(snapshot)
            .Where(task => string.IsNullOrWhiteSpace(status) || task.Status == status)
            .Where(task => string.IsNullOrWhiteSpace(query) || task.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || task.Notes.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(task => TaskDto(task))
            .ToList();
        return new { tasks };
    }

    private object GetTask(JsonObject args)
    {
        var snapshot = _store.Load();
        return TaskDto(FindTask(snapshot, Required(args, "task_id")), includeDetail: true);
    }

    private object AddTaskLog(JsonObject args)
    {
        var snapshot = _store.Load();
        var actor = Actor(args);
        var task = FindTask(snapshot, Required(args, "task_id"));
        _store.AddLog(snapshot, task, Required(args, "message"), actor.Name, actor.Type);
        return TaskDto(task, includeDetail: true);
    }

    private object AddSubtask(JsonObject args)
    {
        var snapshot = _store.Load();
        var actor = Actor(args);
        var task = FindTask(snapshot, Required(args, "task_id"));
        var subtask = _store.AddSubtask(snapshot, task, Required(args, "title"), actor.Name, actor.Type);
        return new { task = TaskDto(task), subtask };
    }

    private object UpdateSubtask(JsonObject args)
    {
        var snapshot = _store.Load();
        var actor = Actor(args);
        var task = FindTask(snapshot, Required(args, "task_id"));
        var subtaskId = Required(args, "subtask_id");
        var subtask = task.Subtasks.FirstOrDefault(item => item.Id == subtaskId)
            ?? throw new InvalidOperationException($"Subtask not found: {subtaskId}");

        var before = TaskStore.ToAuditDictionary(task);
        var changed = new List<string> { "subtasks" };
        SetString(args, "title", value => subtask.Title = value, changed);
        SetString(args, "status", value => subtask.Status = value, changed);
        SetInt(args, "progress_percent", value => subtask.ProgressPercent = value, changed);
        subtask.UpdatedAt = DateTimeOffset.UtcNow;
        _store.UpdateTask(snapshot, task, actor.Name, actor.Type, changed, before);
        return new { task = TaskDto(task), subtask };
    }

    private object GetTodayFocus()
    {
        var snapshot = _store.Load();
        var focus = _store.BuildFocus(snapshot);
        return new
        {
            now = focus.Now.Select(task => TaskDto(task)).ToList(),
            blocked = focus.Blocked.Select(task => TaskDto(task)).ToList(),
            due = focus.Due.Select(task => TaskDto(task)).ToList(),
            recent = focus.Recent.Select(task => TaskDto(task)).ToList()
        };
    }

    private TaskItem FindTask(TaskSnapshot snapshot, string taskId)
    {
        return snapshot.Tasks.FirstOrDefault(task =>
                !task.IsDeleted &&
                (task.Id.Equals(taskId, StringComparison.OrdinalIgnoreCase) ||
                 task.ShortId.Equals(taskId, StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidOperationException($"Task not found: {taskId}");
    }

    private static object TaskDto(TaskItem task, bool includeDetail = false)
    {
        var summary = new
        {
            id = task.Id,
            short_id = task.ShortId,
            title = task.Title,
            notes = task.Notes,
            status = task.Status,
            kanban_status = task.KanbanStatus,
            priority = task.Priority,
            progress_percent = task.ProgressPercent,
            project_id = task.ProjectId,
            list_id = task.ListId,
            assignee = task.Assignee,
            tags = task.Tags.ToArray(),
            estimate = task.Estimate,
            due_date = task.DueDate,
            blocked_by_task_ids = task.BlockedByTaskIds.ToArray(),
            focus_badges = task.FocusBadges,
            updated_at = task.UpdatedAt,
            updated_by = task.UpdatedBy
        };

        if (!includeDetail)
        {
            return summary;
        }

        return new
        {
            summary.id,
            summary.short_id,
            summary.title,
            summary.notes,
            summary.status,
            summary.kanban_status,
            summary.priority,
            summary.progress_percent,
            summary.project_id,
            summary.list_id,
            summary.assignee,
            summary.tags,
            summary.estimate,
            summary.due_date,
            summary.blocked_by_task_ids,
            summary.focus_badges,
            summary.updated_at,
            summary.updated_by,
            attachments = task.Attachments,
            subtasks = task.Subtasks,
            logs = task.Logs
        };
    }

    private static ActorInfo Actor(JsonObject args)
    {
        return new ActorInfo(Optional(args, "actor_name", "AI Agent"), Optional(args, "actor_type", "ai"));
    }

    private static string Required(JsonObject args, string name)
    {
        var value = Optional(args, name, "");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required argument: {name}");
        }
        return value;
    }

    private static string Optional(JsonObject args, string name, string fallback)
    {
        return args.TryGetPropertyValue(name, out var node) ? node?.GetValue<string>() ?? fallback : fallback;
    }

    private static int OptionalInt(JsonObject args, string name, int fallback)
    {
        return args.TryGetPropertyValue(name, out var node) && int.TryParse(node?.ToString(), out var parsed)
            ? Math.Clamp(parsed, 0, 100)
            : fallback;
    }

    private static void SetString(JsonObject args, string name, Action<string> apply, List<string> changed)
    {
        if (!args.TryGetPropertyValue(name, out var node))
        {
            return;
        }

        apply(node?.GetValue<string>() ?? "");
        changed.Add(name);
    }

    private static void SetInt(JsonObject args, string name, Action<int> apply, List<string> changed)
    {
        if (!args.TryGetPropertyValue(name, out var node) || !int.TryParse(node?.ToString(), out var parsed))
        {
            return;
        }

        apply(Math.Clamp(parsed, 0, 100));
        changed.Add(name);
    }

    private static void SetDate(JsonObject args, string name, Action<DateTimeOffset?> apply, List<string> changed)
    {
        if (!args.TryGetPropertyValue(name, out var node))
        {
            return;
        }

        apply(DateTimeOffset.TryParse(node?.GetValue<string>(), out var parsed) ? parsed : null);
        changed.Add(name);
    }

    private static object[] ToolDefinitions()
    {
        return new object[]
        {
            Tool("create_task", "Create a task.", new { title = "string", actor_name = "string", assignee = "string", priority = "string", status = "string", notes = "string", due_date = "yyyy-mm-dd" }),
            Tool("update_task", "Update task fields.", new { task_id = "string", actor_name = "string", title = "string", notes = "string", assignee = "string", status = "string", priority = "string", progress_percent = "number" }),
            Tool("delete_task", "Soft-delete a task.", new { task_id = "string", actor_name = "string" }),
            Tool("list_tasks", "List or search active tasks.", new { status = "string", query = "string" }),
            Tool("get_task", "Get full task detail.", new { task_id = "string" }),
            Tool("add_task_log", "Append a log entry to a task.", new { task_id = "string", actor_name = "string", message = "string" }),
            Tool("add_subtask", "Add a checklist subtask.", new { task_id = "string", actor_name = "string", title = "string" }),
            Tool("update_subtask", "Update a checklist subtask.", new { task_id = "string", subtask_id = "string", actor_name = "string", status = "string", progress_percent = "number" }),
            Tool("get_today_focus", "Read Today Focus sections.", new { })
        };
    }

    private static object Tool(string name, string description, object properties)
    {
        return new
        {
            name,
            description,
            inputSchema = new
            {
                type = "object",
                properties
            }
        };
    }

    private static JsonObject Result(JsonNode? id, object result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = JsonSerializer.SerializeToNode(result)
        };
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        return node is null ? null : JsonNode.Parse(node.ToJsonString());
    }

    private static JsonObject Error(JsonNode? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = JsonSerializer.SerializeToNode(new { code, message })
        };
    }

    private sealed record ActorInfo(string Name, string Type);
}
