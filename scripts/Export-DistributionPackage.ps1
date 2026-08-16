param(
    [string]$Version = "0.1.0",
    [string]$PortableDir = "",
    [string]$MsixDir = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

if ([string]::IsNullOrWhiteSpace($PortableDir)) {
    $PortableDir = Join-Path $repoRoot "artifacts\AiTaskTracker-win-x64-v42-signed-dev"
}
else {
    $PortableDir = [System.IO.Path]::GetFullPath($PortableDir)
}

if ([string]::IsNullOrWhiteSpace($MsixDir)) {
    $MsixDir = Join-Path $repoRoot "artifacts\AiTaskTracker-msix-signed-dev-v42"
}
else {
    $MsixDir = [System.IO.Path]::GetFullPath($MsixDir)
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\distribution"
}
else {
    $OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
}

[System.IO.Directory]::CreateDirectory($OutputDir) | Out-Null

function Latest-File([string]$Directory, [string]$Filter) {
    $file = Get-ChildItem -LiteralPath $Directory -Filter $Filter -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $file) {
        throw "Missing required file: $Directory\$Filter"
    }

    return $file.FullName
}

function Copy-ToPackage([string]$Source, [string]$RelativeDestination) {
    $destination = Join-Path $script:packageRoot $RelativeDestination
    $destinationDirectory = Split-Path -Parent $destination
    [System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $destination -Force
    return $destination
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageName = "AiTaskTracker-$Version-distribution-$timestamp"
$packageRoot = Join-Path $OutputDir $packageName
$zipPath = Join-Path $OutputDir "$packageName.zip"

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
[System.IO.Directory]::CreateDirectory($packageRoot) | Out-Null

$portableExe = Join-Path $PortableDir "AiTaskTracker.exe"
$portableManifest = Join-Path $PortableDir "release-manifest.json"
$msixPackage = Join-Path $MsixDir "AiTaskTracker-$Version-win-x64.msix"
$msixManifest = Join-Path $MsixDir "msix-manifest.json"

foreach ($required in @($portableExe, $portableManifest, $msixPackage, $msixManifest)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing required package input: $required"
    }
}

Copy-ToPackage $portableExe "portable\AiTaskTracker.exe" | Out-Null
Copy-ToPackage $portableManifest "portable\release-manifest.json" | Out-Null
Copy-ToPackage $msixPackage "msix\AiTaskTracker-$Version-win-x64.msix" | Out-Null
Copy-ToPackage $msixManifest "msix\msix-manifest.json" | Out-Null

foreach ($doc in @("PRIVACY.md", "EULA.md", "STORE_LISTING.md", "RELEASE_CHECKLIST.md")) {
    Copy-ToPackage (Join-Path $repoRoot "docs\distribution\$doc") "docs\$doc" | Out-Null
}

$releaseReport = Latest-File (Join-Path $repoRoot "artifacts\release-readiness") "release-readiness-*.md"
$productReport = Latest-File (Join-Path $repoRoot "artifacts\product-readiness") "product-readiness-*.md"
$signingCert = Join-Path $repoRoot "artifacts\signing\AiTaskTracker-dev-signing.cer"
$signingHandoff = Latest-File (Join-Path $repoRoot "artifacts\signing") "signing-handoff-*.md"
$storeAssetsDir = Join-Path $repoRoot "artifacts\store-assets"
$storeAssetsManifest = Join-Path $storeAssetsDir "store-assets-manifest.json"

Copy-ToPackage $releaseReport "reports\release-readiness.md" | Out-Null
Copy-ToPackage $productReport "reports\product-readiness.md" | Out-Null
Copy-ToPackage $signingCert "signing\AiTaskTracker-dev-signing.cer" | Out-Null
Copy-ToPackage $signingHandoff "signing\signing-handoff.md" | Out-Null

if (Test-Path -LiteralPath $storeAssetsManifest) {
    Copy-Item -LiteralPath $storeAssetsDir -Destination (Join-Path $packageRoot "store-assets") -Recurse -Force
}

$manifest = [ordered]@{
    product = "AI Task Tracker"
    version = $Version
    generated_at = [DateTimeOffset]::Now.ToString("o")
    portable_exe_sha256 = (Get-FileHash -LiteralPath $portableExe -Algorithm SHA256).Hash
    msix_sha256 = (Get-FileHash -LiteralPath $msixPackage -Algorithm SHA256).Hash
    package = [System.IO.Path]::GetFileName($zipPath)
    source_portable_dir = $PortableDir
    source_msix_dir = $MsixDir
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot "distribution-manifest.json") -Encoding UTF8

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Distribution package ZIP was not created: $zipPath"
}

[ordered]@{
    package_root = $packageRoot
    zip = $zipPath
    sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
} | ConvertTo-Json
