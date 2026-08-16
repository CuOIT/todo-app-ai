param(
    [string]$PortableDir = "",
    [string]$MsixDir = "",
    [string]$Version = "0.1.0",
    [switch]$RequireSigned,
    [string]$ReportDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

if ([string]::IsNullOrWhiteSpace($PortableDir)) {
    $PortableDir = Join-Path $repoRoot "dist\win-x64"
}
else {
    $PortableDir = [System.IO.Path]::GetFullPath($PortableDir)
}

if ([string]::IsNullOrWhiteSpace($MsixDir)) {
    $MsixDir = Join-Path $repoRoot "dist\msix"
}
else {
    $MsixDir = [System.IO.Path]::GetFullPath($MsixDir)
}

if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $repoRoot "artifacts\release-readiness"
}
else {
    $ReportDir = [System.IO.Path]::GetFullPath($ReportDir)
}

[System.IO.Directory]::CreateDirectory($ReportDir) | Out-Null

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $script:checks.Add([ordered]@{
        name = $Name
        passed = $Passed
        detail = $Detail
    })
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$exePath = Join-Path $PortableDir "AiTaskTracker.exe"
$releaseManifestPath = Join-Path $PortableDir "release-manifest.json"
$msixPath = Join-Path $MsixDir "AiTaskTracker-$Version-win-x64.msix"
$msixManifestPath = Join-Path $MsixDir "msix-manifest.json"

Add-Check "Portable EXE exists" (Test-Path -LiteralPath $exePath) $exePath
Add-Check "Portable manifest exists" (Test-Path -LiteralPath $releaseManifestPath) $releaseManifestPath
Add-Check "MSIX package exists" (Test-Path -LiteralPath $msixPath) $msixPath
Add-Check "MSIX manifest exists" (Test-Path -LiteralPath $msixManifestPath) $msixManifestPath

$releaseManifest = Read-JsonFile $releaseManifestPath
if ($releaseManifest) {
    Add-Check "Portable version matches" ($releaseManifest.version -eq $Version) "manifest=$($releaseManifest.version), expected=$Version"
    Add-Check "Portable runtime is win-x64" ($releaseManifest.runtime -eq "win-x64") "runtime=$($releaseManifest.runtime)"
    Add-Check "Portable file name is stable" ($releaseManifest.file -eq "AiTaskTracker.exe") "file=$($releaseManifest.file)"

    if (Test-Path -LiteralPath $exePath) {
        $actualExeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
        Add-Check "Portable SHA256 matches manifest" ($actualExeHash -eq $releaseManifest.sha256) "actual=$actualExeHash manifest=$($releaseManifest.sha256)"
        $exeInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
        Add-Check "Portable product name present" ($exeInfo.ProductName -eq "AI Task Tracker") "product=$($exeInfo.ProductName)"
        Add-Check "Portable file version present" (-not [string]::IsNullOrWhiteSpace($exeInfo.FileVersion)) "fileVersion=$($exeInfo.FileVersion)"
    }

    if ($RequireSigned) {
        Add-Check "Portable is signed" ($releaseManifest.signed -eq $true) "signed=$($releaseManifest.signed)"
    }
    else {
        Add-Check "Portable signed flag recorded" ($null -ne $releaseManifest.signed) "signed=$($releaseManifest.signed)"
    }
}

$msixManifest = Read-JsonFile $msixManifestPath
if ($msixManifest) {
    Add-Check "MSIX version matches" ($msixManifest.version -eq $Version) "manifest=$($msixManifest.version), expected=$Version"
    Add-Check "MSIX package name matches" ($msixManifest.package -eq "AiTaskTracker-$Version-win-x64.msix") "package=$($msixManifest.package)"

    if (Test-Path -LiteralPath $msixPath) {
        $actualMsixHash = (Get-FileHash -LiteralPath $msixPath -Algorithm SHA256).Hash
        Add-Check "MSIX SHA256 matches manifest" ($actualMsixHash -eq $msixManifest.sha256) "actual=$actualMsixHash manifest=$($msixManifest.sha256)"
    }

    if ($RequireSigned) {
        Add-Check "MSIX is signed" ($msixManifest.signed -eq $true) "signed=$($msixManifest.signed)"
    }
    else {
        Add-Check "MSIX signed flag recorded" ($null -ne $msixManifest.signed) "signed=$($msixManifest.signed)"
    }
}

$releaseFiles = @(
    "AiTaskTracker.exe",
    "release-manifest.json"
)
foreach ($file in $releaseFiles) {
    $path = Join-Path $PortableDir $file
    Add-Check "Portable contains $file" (Test-Path -LiteralPath $path) $path
}

$msixFiles = @(
    "AiTaskTracker-$Version-win-x64.msix",
    "msix-manifest.json"
)
foreach ($file in $msixFiles) {
    $path = Join-Path $MsixDir $file
    Add-Check "MSIX artifact contains $file" (Test-Path -LiteralPath $path) $path
}

$failed = @($checks | Where-Object { -not $_.passed })
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonReportPath = Join-Path $ReportDir "release-readiness-$timestamp.json"
$markdownReportPath = Join-Path $ReportDir "release-readiness-$timestamp.md"

$summary = [ordered]@{
    product = "AI Task Tracker"
    version = $Version
    portable_dir = $PortableDir
    msix_dir = $MsixDir
    require_signed = [bool]$RequireSigned
    generated_at = [DateTimeOffset]::Now.ToString("o")
    passed = ($failed.Count -eq 0)
    checks = $checks
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonReportPath -Encoding UTF8

$requireSignedText = [bool]$RequireSigned
$resultText = if ($failed.Count -eq 0) { "PASS" } else { "FAIL" }

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# AI Task Tracker Release Readiness")
$markdown.Add("")
$markdown.Add(('- Version: `{0}`' -f $Version))
$markdown.Add(('- Portable: `{0}`' -f $PortableDir))
$markdown.Add(('- MSIX: `{0}`' -f $MsixDir))
$markdown.Add(('- Require signed: `{0}`' -f $requireSignedText))
$markdown.Add("- Result: **$resultText**")
$markdown.Add("")
$markdown.Add("| Check | Result | Detail |")
$markdown.Add("| --- | --- | --- |")
foreach ($check in $checks) {
    $result = if ($check.passed) { "PASS" } else { "FAIL" }
    $detail = [string]$check.detail
    $detail = $detail.Replace("|", "\|")
    $markdown.Add(('| {0} | {1} | `{2}` |' -f $check.name, $result, $detail))
}
$markdown | Set-Content -LiteralPath $markdownReportPath -Encoding UTF8

Write-Output "Release readiness: $resultText"
Write-Output "JSON: $jsonReportPath"
Write-Output "Markdown: $markdownReportPath"

if ($failed.Count -gt 0) {
    $failedNames = ($failed | ForEach-Object { $_.name }) -join ", "
    throw "Release readiness failed: $failedNames"
}
