# AI Task Tracker MCP Usage

The project uses a stdio MCP server. In normal use, the AI client spawns it automatically; you should not need to keep a terminal open.

## Recommended Setup

Build the MCP server once:

```powershell
dotnet build AiTaskTracker.Mcp\AiTaskTracker.Mcp.csproj
```

Then install a client config:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Install-McpConnector.ps1 -Client Antigravity
```

or:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Install-McpConnector.ps1 -Client Claude
```

or:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Install-McpConnector.ps1 -Client Cursor
```

For other clients, use the generic config in:

```text
mcp\ai-task-tracker.mcp.json
```

The configured command is:

```text
C:\Users\legen\TO-DO-App\mcp\ai-task-tracker-mcp.cmd
```

That launcher runs the built MCP server DLL. If the DLL does not exist yet, it falls back to `dotnet run` for development.

Antigravity stores this server entry in:

```text
%USERPROFILE%\.gemini\antigravity\mcp_config.json
```

## Data Location

The MCP server uses the same local data folder as the desktop app:

```text
%AppData%\AiTaskTracker
```

Writes update `snapshot.json` and append audit entries to `operations.jsonl`. The desktop app refreshes local data every two seconds.

## Tools

- `create_task`
- `update_task`
- `delete_task`
- `list_tasks`
- `get_task`
- `add_task_log`
- `add_subtask`
- `update_subtask`
- `get_today_focus`

Every write tool accepts `actor_name`; MCP callers should send the agent name, such as `Codex`, `Claude`, or `Cursor`.

## Manual Dev Run

Use this only for testing the MCP server directly:

```powershell
dotnet run --project AiTaskTracker.Mcp\AiTaskTracker.Mcp.csproj --no-build
```
