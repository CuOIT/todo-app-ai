using AiTaskTracker.Server;
using AiTaskTracker.Server.Contracts;
using AiTaskTracker.Server.Data;
using AiTaskTracker.Server.Services;
using Microsoft.EntityFrameworkCore;

const string CurrentUserKey = "current_user";

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy();
    options.SerializerOptions.DictionaryKeyPolicy = new SnakeCaseJsonNamingPolicy();
});

var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=App_Data/aitasktracker.db";

builder.Services.AddDbContext<TaskDbContext>(options =>
{
    if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        EnsureDatabaseDirectory(builder.Environment.ContentRootPath, connectionString);
        options.UseSqlite(connectionString);
    }
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TaskApiService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(IsLocalOrigin));
});

var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    db.Database.Migrate();
}

app.Use(async (context, next) =>
{
    if (IsAnonymousPath(context.Request.Path))
    {
        await next();
        return;
    }

    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "missing_bearer_token" });
        return;
    }

    var rawToken = authorization["Bearer ".Length..].Trim();
    var auth = context.RequestServices.GetRequiredService<AuthService>();
    var user = await auth.ResolveUserAsync(rawToken, context.RequestAborted);
    if (user is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "invalid_or_expired_token" });
        return;
    }

    context.Items[CurrentUserKey] = user;
    await next();
});

app.MapGet("/", () => Results.Ok(new
{
    service = "AI Task Tracker Server",
    version = "0.2.0",
    database = databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ? "postgres" : "sqlite",
    mode = "multi_user_workspace"
}));

app.MapGet("/health", async (TaskDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ok", database = "connected", timestamp = DateTimeOffset.UtcNow })
        : Results.Problem("Database connection failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
});

var authEndpoints = app.MapGroup("/api/auth");

authEndpoints.MapPost("/register", async (
    RegisterRequest request,
    AuthService auth,
    CancellationToken cancellationToken) =>
{
    var validationError = ValidateRegistration(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var result = await auth.RegisterAsync(request, cancellationToken);
    return result.Response is null
        ? Results.Conflict(new { error = result.Error })
        : Results.Ok(result.Response);
});

authEndpoints.MapPost("/login", async (
    LoginRequest request,
    AuthService auth,
    CancellationToken cancellationToken) =>
{
    var result = await auth.LoginAsync(request, cancellationToken);
    return result.Response is null
        ? Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(result.Response);
});

var workspaces = app.MapGroup("/api/workspaces");

workspaces.MapGet("/", async (
    HttpContext context,
    AuthService auth,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    return Results.Ok(await auth.GetWorkspacesAsync(user.Id, cancellationToken));
});

workspaces.MapPost("/", async (
    HttpContext context,
    CreateWorkspaceRequest request,
    AuthService auth,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "name_is_required" });
    }

    var workspace = await auth.CreateWorkspaceAsync(
        GetCurrentUser(context),
        request,
        cancellationToken);
    return Results.Created($"/api/workspaces/{workspace.Id}", workspace);
});

workspaces.MapPost("/join", async (
    HttpContext context,
    JoinWorkspaceRequest request,
    AuthService auth,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.InviteCode))
    {
        return Results.BadRequest(new { error = "invite_code_is_required" });
    }

    var result = await auth.JoinWorkspaceAsync(GetCurrentUser(context), request, cancellationToken);
    return result.Workspace is null
        ? Results.NotFound(new { error = result.Error })
        : Results.Ok(result.Workspace);
});

var tasks = app.MapGroup("/api/workspaces/{workspaceId}/tasks");

tasks.MapGet("/", async (
    HttpContext context,
    string workspaceId,
    string? status,
    bool includeDeleted,
    DateTimeOffset? updatedSince,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    return Results.Ok(await service.ListAsync(
        workspaceId,
        status,
        includeDeleted,
        updatedSince,
        cancellationToken));
});

tasks.MapGet("/{id}", async (
    HttpContext context,
    string workspaceId,
    string id,
    bool includeDeleted,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var task = await service.GetAsync(workspaceId, id, includeDeleted, cancellationToken);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

tasks.MapPost("/", async (
    HttpContext context,
    string workspaceId,
    CreateTaskRequest request,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "title_is_required" });
    }

    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var task = await service.CreateAsync(workspaceId, user, request, cancellationToken);
    return Results.Created($"/api/workspaces/{workspaceId}/tasks/{task.Id}", task);
});

tasks.MapPatch("/{id}", async (
    HttpContext context,
    string workspaceId,
    string id,
    UpdateTaskRequest request,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "title_cannot_be_empty" });
    }

    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var task = await service.UpdateAsync(workspaceId, id, user, request, cancellationToken);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

tasks.MapDelete("/{id}", async (
    HttpContext context,
    string workspaceId,
    string id,
    string? actorName,
    string? actorType,
    string? source,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var deleted = await service.SoftDeleteAsync(
        workspaceId,
        id,
        user,
        new ActorDto(actorName ?? user.DisplayName, actorType ?? "user", source ?? ""),
        cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

tasks.MapPost("/{id}/logs", async (
    HttpContext context,
    string workspaceId,
    string id,
    AddTaskLogRequest request,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "message_is_required" });
    }

    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var log = await service.AddLogAsync(workspaceId, id, user, request, cancellationToken);
    return log is null ? Results.NotFound() : Results.Ok(log);
});

tasks.MapPost("/{id}/subtasks", async (
    HttpContext context,
    string workspaceId,
    string id,
    AddSubtaskRequest request,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "title_is_required" });
    }

    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var subtask = await service.AddSubtaskAsync(workspaceId, id, user, request, cancellationToken);
    return subtask is null ? Results.NotFound() : Results.Ok(subtask);
});

tasks.MapPatch("/{id}/subtasks/{subtaskId}", async (
    HttpContext context,
    string workspaceId,
    string id,
    string subtaskId,
    UpdateSubtaskRequest request,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var subtask = await service.UpdateSubtaskAsync(
        workspaceId,
        id,
        subtaskId,
        user,
        request,
        cancellationToken);
    return subtask is null ? Results.NotFound() : Results.Ok(subtask);
});

tasks.MapPost("/{id}/attachments", async (
    HttpContext context,
    string workspaceId,
    string id,
    AddAttachmentRequest request,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Target))
    {
        return Results.BadRequest(new { error = "target_is_required" });
    }

    if (!string.Equals(request.Type, "url", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(request.Type, "file", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "attachment_type_must_be_url_or_file" });
    }

    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    var attachment = await service.AddAttachmentAsync(workspaceId, id, user, request, cancellationToken);
    return attachment is null ? Results.NotFound() : Results.Ok(attachment);
});

tasks.MapGet("/{id}/audits", async (
    HttpContext context,
    string workspaceId,
    string id,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    return Results.Ok(await service.GetAuditsAsync(workspaceId, id, cancellationToken));
});

app.MapGet("/api/workspaces/{workspaceId}/focus/today", async (
    HttpContext context,
    string workspaceId,
    AuthService auth,
    TaskApiService service,
    CancellationToken cancellationToken) =>
{
    var user = GetCurrentUser(context);
    if (!await auth.HasWorkspaceAccessAsync(user.Id, workspaceId, cancellationToken))
    {
        return Results.Forbid();
    }

    return Results.Ok(await service.GetFocusAsync(workspaceId, cancellationToken));
});

app.Run();

static UserEntity GetCurrentUser(HttpContext context)
{
    return (UserEntity)context.Items[CurrentUserKey]!;
}

static string? ValidateRegistration(RegisterRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
    {
        return "valid_email_is_required";
    }

    if (request.Password.Length < 8)
    {
        return "password_must_have_at_least_8_characters";
    }

    if (string.IsNullOrWhiteSpace(request.DisplayName))
    {
        return "display_name_is_required";
    }

    return string.IsNullOrWhiteSpace(request.WorkspaceName) ? "workspace_name_is_required" : null;
}

static bool IsAnonymousPath(PathString path)
{
    return path == "/"
        || path == "/health"
        || path.StartsWithSegments("/api/auth");
}

static bool IsLocalOrigin(string origin)
{
    return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Host is "localhost" or "127.0.0.1" or "::1";
}

static void EnsureDatabaseDirectory(string contentRoot, string connectionString)
{
    const string dataSourcePrefix = "Data Source=";
    var dataSourcePart = connectionString
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault(part => part.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase));

    if (dataSourcePart is null)
    {
        return;
    }

    var path = dataSourcePart[dataSourcePrefix.Length..].Trim();
    if (string.IsNullOrWhiteSpace(path) || path == ":memory:")
    {
        return;
    }

    var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path, contentRoot);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
}
