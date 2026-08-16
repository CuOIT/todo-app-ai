param(
    [string]$Version = "0.1.0",
    [string]$Publisher = "CN=AI Task Tracker",
    [string]$CertificateThumbprint = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$distRoot = Join-Path $repoRoot "dist"
$publishDir = Join-Path $distRoot "win-x64"
$layoutDir = [System.IO.Path]::GetFullPath((Join-Path $distRoot "msix-layout"))
$assetsDir = Join-Path $layoutDir "Assets"
$outputDir = Join-Path $distRoot "msix"
$packageVersion = if ($Version -match '^\d+\.\d+\.\d+$') { "$Version.0" } else { $Version }

if (-not $layoutDir.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "MSIX layout escaped the repository root."
}

& (Join-Path $PSScriptRoot "Build-Release.ps1") -Version $Version -CertificateThumbprint $CertificateThumbprint | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Portable release build failed."
}

if (Test-Path -LiteralPath $layoutDir) {
    Remove-Item -LiteralPath $layoutDir -Recurse -Force
}
[System.IO.Directory]::CreateDirectory($assetsDir) | Out-Null
[System.IO.Directory]::CreateDirectory($outputDir) | Out-Null

Copy-Item -LiteralPath (Join-Path $publishDir "AiTaskTracker.exe") -Destination $layoutDir
& (Join-Path $PSScriptRoot "Generate-AppIcon.ps1") -PackageAssetsDirectory $assetsDir | Out-Null

$publisherXml = [System.Security.SecurityElement]::Escape($Publisher)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity Name="AiTaskTracker.Desktop" Publisher="$publisherXml" Version="$packageVersion" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>AI Task Tracker</DisplayName>
    <PublisherDisplayName>AI Task Tracker</PublisherDisplayName>
    <Description>Local-first task tracking for people and AI agents.</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Applications>
    <Application Id="App" Executable="AiTaskTracker.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="AI Task Tracker"
        Description="Local-first task tracking for people and AI agents."
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
$manifest | Set-Content -LiteralPath (Join-Path $layoutDir "AppxManifest.xml") -Encoding UTF8

function Find-WindowsSdkTool([string]$Name) {
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $kitsRoot -Filter $Name -Recurse -File |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

$makeAppx = Find-WindowsSdkTool "makeappx.exe"
if (-not $makeAppx) {
    throw "makeappx.exe was not found in the Windows SDK."
}

$packagePath = Join-Path $outputDir "AiTaskTracker-$Version-win-x64.msix"
& $makeAppx pack /d $layoutDir /p $packagePath /o
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $packagePath)) {
    throw "MSIX packaging failed."
}

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $signTool = Find-WindowsSdkTool "signtool.exe"
    if (-not $signTool) {
        throw "signtool.exe was not found in the Windows SDK."
    }

    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $packagePath
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX signing failed."
    }
}

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
[ordered]@{
    product = "AI Task Tracker"
    version = $Version
    package = [System.IO.Path]::GetFileName($packagePath)
    publisher = $Publisher
    sha256 = $hash
    signed = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
    built_at_utc = [DateTime]::UtcNow.ToString("o")
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir "msix-manifest.json") -Encoding UTF8

Write-Output "MSIX: $packagePath"
Write-Output "SHA256: $hash"
