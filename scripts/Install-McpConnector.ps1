param(
    [ValidateSet("Antigravity", "Claude", "Cursor", "Generic")]
    [string]$Client = "Generic",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$LauncherPath = Join-Path $RepoRoot "mcp\ai-task-tracker-mcp.cmd"
$McpProjectPath = Join-Path $RepoRoot "AiTaskTracker.Mcp\AiTaskTracker.Mcp.csproj"
$ServerName = "ai-task-tracker"

if (-not (Test-Path $LauncherPath)) {
    throw "MCP launcher not found: $LauncherPath"
}

if (-not $SkipBuild) {
    dotnet build $McpProjectPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $McpProjectPath"
    }
}

$ServerConfig = [ordered]@{
    command = $LauncherPath
    args = @()
    env = @{}
}

function ConvertTo-PrettyJson {
    param([Parameter(Mandatory)] $Value)
    return ($Value | ConvertTo-Json -Depth 20)
}

function Read-JsonObject {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        return [pscustomobject]@{}
    }

    $raw = Get-Content -Raw -LiteralPath $Path
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return [pscustomobject]@{}
    }

    return $raw | ConvertFrom-Json
}

function Ensure-Property {
    param(
        [Parameter(Mandatory)]$Object,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)]$Value
    )

    if ($Object.PSObject.Properties.Name -contains $Name) {
        $Object.$Name = $Value
    }
    else {
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
    }
}

function Install-McpServerEntry {
    param([Parameter(Mandatory)][string]$ConfigPath)

    $configDir = Split-Path -Parent $ConfigPath
    if (-not (Test-Path $configDir)) {
        New-Item -ItemType Directory -Force -Path $configDir | Out-Null
    }

    if (Test-Path $ConfigPath) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        Copy-Item -LiteralPath $ConfigPath -Destination "$ConfigPath.bak-$timestamp" -Force
    }

    $config = Read-JsonObject -Path $ConfigPath
    if ($config.PSObject.Properties.Name -notcontains "mcpServers" -or $null -eq $config.mcpServers) {
        $config | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value ([pscustomobject]@{})
    }

    Ensure-Property -Object $config.mcpServers -Name $ServerName -Value ([pscustomobject]$ServerConfig)
    ConvertTo-PrettyJson -Value $config | Set-Content -LiteralPath $ConfigPath -Encoding UTF8
    Write-Host "Installed $ServerName MCP server into $ConfigPath"
}

if ($Client -eq "Antigravity") {
    $path = Join-Path $env:USERPROFILE ".gemini\antigravity\mcp_config.json"
    Install-McpServerEntry -ConfigPath $path
    return
}

if ($Client -eq "Claude") {
    $path = Join-Path $env:APPDATA "Claude\claude_desktop_config.json"
    Install-McpServerEntry -ConfigPath $path
    return
}

if ($Client -eq "Cursor") {
    $path = Join-Path $env:USERPROFILE ".cursor\mcp.json"
    Install-McpServerEntry -ConfigPath $path
    return
}

$genericConfig = [ordered]@{
    mcpServers = [ordered]@{
        $ServerName = $ServerConfig
    }
}

Write-Host "Generic MCP config:"
ConvertTo-PrettyJson -Value $genericConfig | Write-Host
Write-Host ""
Write-Host "To install automatically:"
Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\Install-McpConnector.ps1 -Client Antigravity"
Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\Install-McpConnector.ps1 -Client Claude"
Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\Install-McpConnector.ps1 -Client Cursor"
