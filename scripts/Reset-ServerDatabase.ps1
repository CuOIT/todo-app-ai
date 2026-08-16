param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dbPath = Join-Path $repoRoot "AiTaskTracker.Server\App_Data\aitasktracker.db"
$dbDirectory = Split-Path $dbPath -Parent

if (-not $dbPath.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to reset a database outside the workspace: $dbPath"
}

if (-not $Force) {
    Write-Host "This will delete the local dev SQLite database:"
    Write-Host $dbPath
    Write-Host "Run with -Force to confirm."
    exit 1
}

Get-Process AiTaskTracker.Server -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path -LiteralPath $dbPath) {
    Remove-Item -LiteralPath $dbPath -Force
}

New-Item -ItemType Directory -Path $dbDirectory -Force | Out-Null
Write-Host "Server database reset:"
Write-Host $dbPath
