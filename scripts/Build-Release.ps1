param(
    [string]$Version = "0.1.0",
    [string]$CertificateThumbprint = "",
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$project = Join-Path $repoRoot "AiTaskTracker\AiTaskTracker.csproj"
$publishDir = Join-Path $repoRoot "dist\win-x64"
$exePath = Join-Path $publishDir "AiTaskTracker.exe"

& (Join-Path $PSScriptRoot "Generate-AppIcon.ps1") | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:FileVersion="$Version.0" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exePath)) {
    throw "Release publish failed."
}

function Find-SignTool {
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $kitsRoot -Filter signtool.exe -Recurse -File |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

function Sign-Artifact([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        return
    }

    $signTool = Find-SignTool
    if (-not $signTool) {
        throw "signtool.exe was not found in the Windows SDK."
    }

    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for $Path"
    }
}

Sign-Artifact $exePath

$hash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
$manifest = [ordered]@{
    product = "AI Task Tracker"
    version = $Version
    runtime = "win-x64"
    file = "AiTaskTracker.exe"
    sha256 = $hash
    signed = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
    built_at_utc = [DateTime]::UtcNow.ToString("o")
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishDir "release-manifest.json") -Encoding UTF8

if ($BuildInstaller) {
    $compilerCandidates = @(
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    $compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $compiler) {
        throw "Inno Setup 6 is required to build the installer."
    }

    & $compiler "/DAppVersion=$Version" (Join-Path $repoRoot "installer\AiTaskTracker.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Installer compilation failed."
    }

    $installer = Get-ChildItem -LiteralPath (Join-Path $repoRoot "dist\installer") -Filter "*.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($installer) {
        Sign-Artifact $installer.FullName
    }
}

Write-Output "Release: $exePath"
Write-Output "SHA256: $hash"
