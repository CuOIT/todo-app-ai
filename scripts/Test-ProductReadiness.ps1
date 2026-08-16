param(
    [string]$PortableDir = "",
    [string]$MsixDir = "",
    [string]$Version = "0.1.0",
    [string]$DataDir = "",
    [string]$ReportDir = "",
    [switch]$RequireSigned,
    [switch]$Strict
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

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $env:APPDATA "AiTaskTracker"
}
else {
    $DataDir = [System.IO.Path]::GetFullPath($DataDir)
}

if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $repoRoot "artifacts\product-readiness"
}
else {
    $ReportDir = [System.IO.Path]::GetFullPath($ReportDir)
}

[System.IO.Directory]::CreateDirectory($ReportDir) | Out-Null

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Area, [string]$Name, [string]$Status, [string]$Detail) {
    if ($Status -notin @("PASS", "PENDING", "FAIL")) {
        throw "Invalid check status: $Status"
    }

    $script:checks.Add([ordered]@{
        area = $Area
        name = $Name
        status = $Status
        detail = $Detail
    })
}

function Add-PassFail([string]$Area, [string]$Name, [bool]$Passed, [string]$Detail) {
    Add-Check $Area $Name ($(if ($Passed) { "PASS" } else { "FAIL" })) $Detail
}

function Read-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return ""
    }

    return Get-Content -LiteralPath $Path -Raw
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$mainWindowXaml = Read-Text (Join-Path $repoRoot "AiTaskTracker\MainWindow.xaml")
$kanbanCardTemplate = [regex]::Match($mainWindowXaml, '<DataTemplate x:Key="KanbanCardTemplate">.*?</DataTemplate>', [System.Text.RegularExpressions.RegexOptions]::Singleline).Value
$mainWindowCode = Read-Text (Join-Path $repoRoot "AiTaskTracker\MainWindow.xaml.cs")
$settingsCode = Read-Text (Join-Path $repoRoot "AiTaskTracker\SettingsWindow.cs")
$licenseStoreCode = Read-Text (Join-Path $repoRoot "AiTaskTracker\Services\LicenseStateStore.cs")
$mcpCode = Read-Text (Join-Path $repoRoot "AiTaskTracker.Mcp\Program.cs")
$privacyDoc = Read-Text (Join-Path $repoRoot "docs\distribution\PRIVACY.md")
$eulaDoc = Read-Text (Join-Path $repoRoot "docs\distribution\EULA.md")
$storeListingDoc = Read-Text (Join-Path $repoRoot "docs\distribution\STORE_LISTING.md")
$releaseChecklistDoc = Read-Text (Join-Path $repoRoot "docs\distribution\RELEASE_CHECKLIST.md")
$signingHandoffScript = Read-Text (Join-Path $repoRoot "scripts\Export-SigningHandoffKit.ps1")
$storeAssetsScript = Read-Text (Join-Path $repoRoot "scripts\Export-StoreAssets.ps1")
$storeAssetsManifest = Read-JsonFile (Join-Path $repoRoot "artifacts\store-assets\store-assets-manifest.json")
$signingDir = Join-Path $repoRoot "artifacts\signing"
$signingCer = Join-Path $signingDir "AiTaskTracker-dev-signing.cer"
$latestSigningHandoff = Get-ChildItem -LiteralPath $signingDir -Filter "signing-handoff-*.md" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$signingHandoffDoc = if ($latestSigningHandoff) { Read-Text $latestSigningHandoff.FullName } else { "" }
$distributionDir = Join-Path $repoRoot "artifacts\distribution"
$latestDistributionPackage = Get-ChildItem -LiteralPath $distributionDir -Filter "AiTaskTracker-*-distribution-*.zip" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$latestDistributionRoot = Get-ChildItem -LiteralPath $distributionDir -Directory -Filter "AiTaskTracker-*-distribution-*" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$distributionManifest = if ($latestDistributionRoot) { Read-JsonFile (Join-Path $latestDistributionRoot.FullName "distribution-manifest.json") } else { $null }

Add-PassFail "UI" "Kanban board has four sellable columns" `
    ($mainWindowXaml -match 'TodoKanbanList' -and $mainWindowXaml -match 'InProgressKanbanList' -and $mainWindowXaml -match 'DoneKanbanList' -and $mainWindowXaml -match 'CloseKanbanList') `
    "Expected TO-DO, IN-PROGRESS, DONE, CLOSE board lists."
Add-PassFail "UI" "Kanban card uses icon-only priority" `
    ($mainWindowXaml -match 'KanbanPriorityIconTemplate' -and $mainWindowXaml -notmatch 'KanbanPriorityLabel') `
    "Priority in Board mode should not render text labels."
Add-PassFail "UI" "Kanban card uses icon-only assignee" `
    ($mainWindowXaml -match 'KanbanAssigneeChip' -and $mainWindowXaml -match 'Text=\"&#xE77B;\"') `
    "Assignee in Board mode should render a compact person icon."
Add-PassFail "UI" "Kanban card title is clamped" `
    ($kanbanCardTemplate -match 'TextWrapping="NoWrap"' -and $kanbanCardTemplate -match 'TextTrimming="CharacterEllipsis"') `
    "Board task names should stay on one line and truncate instead of overflowing."
Add-PassFail "UI" "Kanban card does not repeat column status" `
    ($kanbanCardTemplate -notmatch 'StatusBadgeTemplate' -and $kanbanCardTemplate -notmatch 'StatusDisplay') `
    "Board columns already communicate status, so cards should not repeat a status badge."
Add-PassFail "UI" "Kanban board removes overflow action button" `
    ($kanbanCardTemplate -notmatch 'Task actions' -and $kanbanCardTemplate -notmatch 'TaskActionsButton_Click') `
    "Board mode should not show the overflow ellipsis action button."
Add-PassFail "UI" "Task Info drawer has animated open and close" `
    ($mainWindowCode -match 'AnimateTaskInfoPanel' -and $mainWindowCode -match 'TaskInfoDrawerWidth') `
    "Task Info should feel like a panel, not a static prototype form."
Add-PassFail "UI" "Settings includes product billing surface" `
    ($settingsCode -match 'Billing And IAP Readiness' -and $settingsCode -match 'Purchase gated') `
    "Settings must show a clear purchase/readiness state."
Add-PassFail "UI" "Desktop keyboard workflow exists" `
    ($mainWindowCode -match 'Key.K' -and $mainWindowCode -match 'Key.N' -and $mainWindowCode -match 'Key.D1' -and $mainWindowCode -match 'Key.D2' -and $mainWindowCode -match 'OpenDefaultQuickAdd') `
    "Desktop users should be able to search, quick-add, and switch List/Board views by keyboard."

Add-PassFail "Persistence" "Task snapshot storage exists" `
    ((Test-Path -LiteralPath (Join-Path $repoRoot "AiTaskTracker\Services\TaskStore.cs")) -and $mainWindowCode -match 'SaveTaskChange') `
    "Desktop task edits must persist to local JSON through TaskStore."
Add-PassFail "Persistence" "Backup includes task, audit, preferences, and license state" `
    ($settingsCode -match 'snapshot.json' -and $settingsCode -match 'operations.jsonl' -and $settingsCode -match 'ui-preferences.json' -and $settingsCode -match 'license-state.json') `
    "Backup archive should carry data needed for restore and entitlement continuity."

Add-PassFail "MCP" "MCP CRUD tools are present" `
    ($mcpCode -match 'create_task' -and $mcpCode -match 'update_task' -and $mcpCode -match 'list_tasks' -and $mcpCode -match 'get_task' -and $mcpCode -match 'get_today_focus') `
    "AI agents need stable CRUD/query tools."
Add-PassFail "MCP" "MCP actor metadata is recorded" `
    ($mcpCode -match 'actor_name' -and $mcpCode -match 'actor_type') `
    "AI changes must remain audit-attributable."

Add-PassFail "Distribution" "Privacy policy draft exists" `
    ($privacyDoc -match 'local-first' -and $privacyDoc -match 'Data Stored Locally' -and $privacyDoc -match 'AI Agent Access') `
    "Store distribution needs clear local data and AI-agent privacy notes."
Add-PassFail "Distribution" "EULA draft exists" `
    ($eulaDoc -match 'License Grant' -and $eulaDoc -match 'AI Agent Use' -and $eulaDoc -match 'Purchases') `
    "Commercial distribution needs a license agreement draft."
Add-PassFail "Distribution" "Store listing draft exists" `
    ($storeListingDoc -match 'Product Name' -and $storeListingDoc -match 'Key Features' -and $storeListingDoc -match 'Purchase Notes') `
    "Store submission should have listing copy and purchase notes."
Add-PassFail "Distribution" "Release checklist exists" `
    ($releaseChecklistDoc -match 'Current Local Release Gates' -and $releaseChecklistDoc -match 'Production Store Gates' -and $releaseChecklistDoc -match 'Known Pending Items') `
    "Release process needs explicit local and production gates."
Add-PassFail "Distribution" "Signing handoff script exists" `
    ($signingHandoffScript -match 'Export-Certificate' -and $signingHandoffScript -match 'Production Signing') `
    "Release handoff should export public dev cert and production signing notes."
Add-PassFail "Distribution" "Signing handoff kit generated" `
    ((Test-Path -LiteralPath $signingCer) -and $signingHandoffDoc -match 'AI Task Tracker Signing Handoff' -and $signingHandoffDoc -match 'Production Signing') `
    "Artifacts should include public dev certificate and signing handoff markdown."
Add-PassFail "Distribution" "Distribution package exporter exists" `
    ((Read-Text (Join-Path $repoRoot "scripts\Export-DistributionPackage.ps1")) -match 'distribution-manifest.json' -and (Read-Text (Join-Path $repoRoot "scripts\Export-DistributionPackage.ps1")) -match 'Compress-Archive') `
    "Release handoff should produce a portable distribution package."
Add-PassFail "Distribution" "Distribution package generated" `
    (($null -ne $latestDistributionPackage) -and ($null -ne $distributionManifest) -and $distributionManifest.product -eq "AI Task Tracker") `
    "Artifacts should include a zipped package with manifest, binaries, docs, reports, and signing handoff."
Add-PassFail "Distribution" "Store assets exporter exists" `
    ($storeAssetsScript -match 'store-assets-manifest.json' -and $storeAssetsScript -match 'screenshot_requirement') `
    "Store submission needs repeatable icon and screenshot asset export."
Add-PassFail "Distribution" "Store assets generated" `
    (($null -ne $storeAssetsManifest) -and
        @($storeAssetsManifest.icon_assets).Count -ge 4 -and
        @($storeAssetsManifest.screenshots).Count -ge 2 -and
        @($storeAssetsManifest.screenshots | Where-Object { $_.width -lt 1000 -or $_.height -lt 600 -or $_.bytes -lt 51200 }).Count -eq 0) `
    "Store assets should include required icons and at least two valid desktop screenshots at 1000x600 or larger."

Add-PassFail "License" "Local license state store exists" `
    ($licenseStoreCode -match 'LicenseStateStore' -and $licenseStoreCode -match 'MachineFingerprint' -and $licenseStoreCode -match 'ExportReadinessReport') `
    "The app needs a local entitlement contract before store integration."
Add-PassFail "License" "Entitlement adapter contract exists" `
    ($licenseStoreCode -match 'IEntitlementAdapter' -and $licenseStoreCode -match 'LocalEntitlementAdapter') `
    "Purchase providers need a replaceable adapter contract."
Add-PassFail "License" "Restore purchase flow is implemented" `
    ($licenseStoreCode -match 'RestorePurchases' -and $settingsCode -match 'Restore entitlement') `
    "Settings must expose a restore flow before real store integration."

$licensePath = Join-Path $DataDir "license-state.json"
$licenseState = Read-JsonFile $licensePath
Add-PassFail "License" "License state file is readable" ($null -ne $licenseState) $licensePath
if ($licenseState) {
    Add-PassFail "License" "License has machine-bound fingerprint" (-not [string]::IsNullOrWhiteSpace($licenseState.machine_fingerprint)) "fingerprint=$($licenseState.machine_fingerprint)"
    Add-PassFail "License" "License has product id" (-not [string]::IsNullOrWhiteSpace($licenseState.store_product_id)) "product=$($licenseState.store_product_id)"
    Add-Check "License" "Store entitlement adapter" ($(if ($licenseState.store_entitlement_adapter_ready -eq $true) { "PASS" } else { "PENDING" })) "adapter_ready=$($licenseState.store_entitlement_adapter_ready)"
    Add-Check "License" "Purchase restore flow" ($(if ($licenseState.purchase_restore_ready -eq $true) { "PASS" } else { "PENDING" })) "restore_ready=$($licenseState.purchase_restore_ready)"
}

$exePath = Join-Path $PortableDir "AiTaskTracker.exe"
$releaseManifestPath = Join-Path $PortableDir "release-manifest.json"
$msixPath = Join-Path $MsixDir "AiTaskTracker-$Version-win-x64.msix"
$msixManifestPath = Join-Path $MsixDir "msix-manifest.json"
$releaseManifest = Read-JsonFile $releaseManifestPath
$msixManifest = Read-JsonFile $msixManifestPath

Add-PassFail "Packaging" "Portable EXE exists" (Test-Path -LiteralPath $exePath) $exePath
Add-PassFail "Packaging" "MSIX exists" (Test-Path -LiteralPath $msixPath) $msixPath
Add-PassFail "Packaging" "Release manifest exists" ($null -ne $releaseManifest) $releaseManifestPath
Add-PassFail "Packaging" "MSIX manifest exists" ($null -ne $msixManifest) $msixManifestPath

if ($releaseManifest -and (Test-Path -LiteralPath $exePath)) {
    $actualExeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
    Add-PassFail "Packaging" "Portable hash matches manifest" ($actualExeHash -eq $releaseManifest.sha256) "actual=$actualExeHash manifest=$($releaseManifest.sha256)"
    Add-Check "Packaging" "Portable signed" ($(if ($releaseManifest.signed -eq $true) { "PASS" } elseif ($RequireSigned) { "FAIL" } else { "PENDING" })) "signed=$($releaseManifest.signed)"
    if ($releaseManifest.signed -eq $true) {
        $portableSignature = Get-AuthenticodeSignature -LiteralPath $exePath
        Add-Check "Packaging" "Portable signature trusted" ($(if ($portableSignature.Status -eq "Valid") { "PASS" } else { "PENDING" })) "status=$($portableSignature.Status); $($portableSignature.StatusMessage)"
    }
}

if ($msixManifest -and (Test-Path -LiteralPath $msixPath)) {
    $actualMsixHash = (Get-FileHash -LiteralPath $msixPath -Algorithm SHA256).Hash
    Add-PassFail "Packaging" "MSIX hash matches manifest" ($actualMsixHash -eq $msixManifest.sha256) "actual=$actualMsixHash manifest=$($msixManifest.sha256)"
    Add-Check "Packaging" "MSIX signed" ($(if ($msixManifest.signed -eq $true) { "PASS" } elseif ($RequireSigned) { "FAIL" } else { "PENDING" })) "signed=$($msixManifest.signed)"
    if ($msixManifest.signed -eq $true) {
        $msixSignature = Get-AuthenticodeSignature -LiteralPath $msixPath
        Add-Check "Packaging" "MSIX signature trusted" ($(if ($msixSignature.Status -eq "Valid") { "PASS" } else { "PENDING" })) "status=$($msixSignature.Status); $($msixSignature.StatusMessage)"
    }
}

$failed = @($checks | Where-Object { $_.status -eq "FAIL" })
$pending = @($checks | Where-Object { $_.status -eq "PENDING" })
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonReportPath = Join-Path $ReportDir "product-readiness-$timestamp.json"
$markdownReportPath = Join-Path $ReportDir "product-readiness-$timestamp.md"
$resultText = if ($failed.Count -eq 0 -and ($pending.Count -eq 0 -or -not $Strict)) { "PASS" } else { "FAIL" }

$summary = [ordered]@{
    product = "AI Task Tracker"
    version = $Version
    generated_at = [DateTimeOffset]::Now.ToString("o")
    strict = [bool]$Strict
    require_signed = [bool]$RequireSigned
    result = $resultText
    failed = $failed.Count
    pending = $pending.Count
    checks = $checks
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonReportPath -Encoding UTF8

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# AI Task Tracker Product Readiness")
$markdown.Add("")
$markdown.Add(('- Version: `{0}`' -f $Version))
$markdown.Add(('- Strict mode: `{0}`' -f ([bool]$Strict)))
$markdown.Add(('- Require signed: `{0}`' -f ([bool]$RequireSigned)))
$markdown.Add(('- Result: **{0}**' -f $resultText))
$markdown.Add(('- Failed: `{0}`' -f $failed.Count))
$markdown.Add(('- Pending: `{0}`' -f $pending.Count))
$markdown.Add("")
$markdown.Add("| Area | Check | Status | Detail |")
$markdown.Add("| --- | --- | --- | --- |")
foreach ($check in $checks) {
    $detail = ([string]$check.detail).Replace("|", "\|")
    $markdown.Add(('| {0} | {1} | {2} | `{3}` |' -f $check.area, $check.name, $check.status, $detail))
}
$markdown | Set-Content -LiteralPath $markdownReportPath -Encoding UTF8

Write-Output "Product readiness: $resultText"
Write-Output "Failed: $($failed.Count)"
Write-Output "Pending: $($pending.Count)"
Write-Output "JSON: $jsonReportPath"
Write-Output "Markdown: $markdownReportPath"

if ($failed.Count -gt 0 -or ($Strict -and $pending.Count -gt 0)) {
    $failedNames = (($failed + $(if ($Strict) { $pending } else { @() })) | ForEach-Object { "$($_.area): $($_.name)" }) -join ", "
    throw "Product readiness failed: $failedNames"
}
