param(
    [string]$Version = "0.1.0",
    [string]$MsixAssetsDir = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

function Get-ImageMetadata([string]$Path) {
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        return [ordered]@{
            width = $image.Width
            height = $image.Height
            bytes = (Get-Item -LiteralPath $Path).Length
        }
    }
    finally {
        $image.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($MsixAssetsDir)) {
    $MsixAssetsDir = Join-Path $repoRoot "dist\msix-layout\Assets"
}
else {
    $MsixAssetsDir = [System.IO.Path]::GetFullPath($MsixAssetsDir)
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\store-assets"
}
else {
    $OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
}

[System.IO.Directory]::CreateDirectory($OutputDir) | Out-Null

$iconsDir = Join-Path $OutputDir "icons"
$screenshotsDir = Join-Path $OutputDir "screenshots"
[System.IO.Directory]::CreateDirectory($iconsDir) | Out-Null
[System.IO.Directory]::CreateDirectory($screenshotsDir) | Out-Null

$requiredIcons = @(
    "Square44x44Logo.png",
    "Square150x150Logo.png",
    "Wide310x150Logo.png",
    "StoreLogo.png"
)

$iconEntries = New-Object System.Collections.Generic.List[object]
foreach ($icon in $requiredIcons) {
    $source = Join-Path $MsixAssetsDir $icon
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing store icon asset: $source"
    }

    $destination = Join-Path $iconsDir $icon
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $metadata = Get-ImageMetadata $destination
    $iconEntries.Add([ordered]@{
        file = "icons/$icon"
        sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        width = $metadata.width
        height = $metadata.height
        bytes = $metadata.bytes
    })
}

$screenshotEntries = Get-ChildItem -LiteralPath $screenshotsDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in @(".png", ".jpg", ".jpeg") } |
    Sort-Object Name |
    ForEach-Object {
        $metadata = Get-ImageMetadata $_.FullName
        [ordered]@{
            file = "screenshots/$($_.Name)"
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            width = $metadata.width
            height = $metadata.height
            bytes = $metadata.bytes
        }
    }

$manifest = [ordered]@{
    product = "AI Task Tracker"
    version = $Version
    generated_at = [DateTimeOffset]::Now.ToString("o")
    icon_assets = $iconEntries
    screenshots = @($screenshotEntries)
    screenshot_requirement = "Include at least two valid desktop screenshots, each at least 1000x600, before final store submission."
}

$manifestPath = Join-Path $OutputDir "store-assets-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$notes = @(
    "# AI Task Tracker Store Assets",
    "",
    "Generated: $([DateTimeOffset]::Now.ToString("o"))",
    "",
    "## Included Icon Assets",
    "",
    "- Square44x44Logo.png",
    "- Square150x150Logo.png",
    "- Wide310x150Logo.png",
    "- StoreLogo.png",
    "",
    "## Screenshot Requirement",
    "",
    "Add final screenshots under `screenshots/` before production store submission. Recommended captures:",
    "",
    "- Today Focus + Kanban board.",
    "- Task Info drawer open.",
    "- Settings Billing/IAP readiness panel.",
    "- Restore entitlement result modal."
)
$notes | Set-Content -LiteralPath (Join-Path $OutputDir "STORE_ASSETS.md") -Encoding UTF8

[ordered]@{
    output_dir = $OutputDir
    manifest = $manifestPath
    icon_count = $iconEntries.Count
    screenshot_count = @($screenshotEntries).Count
} | ConvertTo-Json
