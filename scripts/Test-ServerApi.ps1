param(
    [string]$BaseUrl = "http://127.0.0.1:5187"
)

$ErrorActionPreference = "Stop"

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = ""
    )

    $headers = @{}
    if ($Token) {
        $headers.Authorization = "Bearer $Token"
    }

    $parameters = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        Headers = $headers
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $parameters.Body = ($Body | ConvertTo-Json -Depth 12)
    }

    Invoke-RestMethod @parameters
}

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$email = "dev+$stamp@example.local"
$password = "dev-password-123"

Write-Host "Checking health..."
$health = Invoke-Json -Method GET -Path "/health"

Write-Host "Registering test user/workspace..."
$auth = Invoke-Json -Method POST -Path "/api/auth/register" -Body @{
    email = $email
    password = $password
    display_name = "Dev Owner"
    workspace_name = "Dev Workspace"
}

$token = $auth.access_token
$workspaceId = $auth.workspaces[0].id

Write-Host "Creating task..."
$task = Invoke-Json -Method POST -Path "/api/workspaces/$workspaceId/tasks" -Token $token -Body @{
    title = "Smoke test task"
    notes = "Created by scripts/Test-ServerApi.ps1"
    status = "in_progress"
    priority = "high"
    actor = @{
        actor_name = "Server Smoke Test"
        actor_type = "system"
        source = "powershell"
    }
}

Write-Host "Adding log, subtask, and attachment reference..."
$null = Invoke-Json -Method POST -Path "/api/workspaces/$workspaceId/tasks/$($task.id)/logs" -Token $token -Body @{
    message = "Smoke test log"
}
$null = Invoke-Json -Method POST -Path "/api/workspaces/$workspaceId/tasks/$($task.id)/subtasks" -Token $token -Body @{
    title = "Smoke test subtask"
}
$null = Invoke-Json -Method POST -Path "/api/workspaces/$workspaceId/tasks/$($task.id)/attachments" -Token $token -Body @{
    type = "url"
    title = "Smoke test link"
    target = "https://example.local/spec"
    note = "Reference only; no upload"
}

Write-Host "Reading focus and audits..."
$focus = Invoke-Json -Method GET -Path "/api/workspaces/$workspaceId/focus/today" -Token $token
$audits = Invoke-Json -Method GET -Path "/api/workspaces/$workspaceId/tasks/$($task.id)/audits" -Token $token

[pscustomobject]@{
    health = $health.status
    email = $email
    workspace_id = $workspaceId
    task_id = $task.id
    task_short_id = $task.short_id
    focus_now_count = @($focus.now).Count
    audit_count = @($audits).Count
} | Format-List
