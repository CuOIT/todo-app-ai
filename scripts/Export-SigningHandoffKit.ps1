param(
    [string]$Subject = "CN=AI Task Tracker",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\signing"
}
else {
    $OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
}

[System.IO.Directory]::CreateDirectory($OutputDir) | Out-Null

$certificateInfo = & (Join-Path $PSScriptRoot "Ensure-DevSigningCertificate.ps1") -Subject $Subject | ConvertFrom-Json
$certificate = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object { $_.Thumbprint -eq $certificateInfo.thumbprint } |
    Select-Object -First 1

if (-not $certificate) {
    throw "Signing certificate was not found after creation."
}

$cerPath = Join-Path $OutputDir "AiTaskTracker-dev-signing.cer"
Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$handoffPath = Join-Path $OutputDir "signing-handoff-$timestamp.md"
$thumbprint = $certificate.Thumbprint
$subjectLine = $certificate.Subject
$notAfter = $certificate.NotAfter.ToString("o")

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# AI Task Tracker Signing Handoff")
$lines.Add("")
$lines.Add(("Generated: {0}" -f (Get-Date -Format "o")))
$lines.Add("")
$lines.Add("## Current Dev Certificate")
$lines.Add("")
$lines.Add(('- Subject: `{0}`' -f $subjectLine))
$lines.Add(('- Thumbprint: `{0}`' -f $thumbprint))
$lines.Add(('- Expires: `{0}`' -f $notAfter))
$lines.Add(('- Public certificate: `{0}`' -f $cerPath))
$lines.Add("- Private key export: not included")
$lines.Add("")
$lines.Add("## Dev Verification")
$lines.Add("")
$lines.Add("The current signed-dev artifact is signed with a self-signed code-signing certificate. Authenticode can show the signature exists, but Windows will report the chain as untrusted until the certificate is trusted locally.")
$lines.Add("")
$lines.Add("Optional local trust command for development machines:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add(('Import-Certificate -FilePath "{0}" -CertStoreLocation Cert:\CurrentUser\TrustedPeople' -f $cerPath))
$lines.Add('```')
$lines.Add("")
$lines.Add("Use this only on machines where you intentionally trust this local development publisher.")
$lines.Add("")
$lines.Add("## Production Signing")
$lines.Add("")
$lines.Add("For real distribution, replace the dev certificate with one of:")
$lines.Add("")
$lines.Add("- Microsoft Store managed signing.")
$lines.Add("- A trusted code-signing certificate issued to the final publisher.")
$lines.Add("- A CI/CD signing service that holds the private key outside the repository.")
$lines.Add("")
$lines.Add("Production builds should run:")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add('.\scripts\Build-MSIX.ps1 -Version 0.1.0 -Publisher "CN=<production publisher>" -CertificateThumbprint <trusted-cert-thumbprint>')
$lines.Add('.\scripts\Test-ReleaseReadiness.ps1 -PortableDir .\dist\win-x64 -MsixDir .\dist\msix -Version 0.1.0 -RequireSigned')
$lines.Add('.\scripts\Test-ProductReadiness.ps1 -PortableDir .\dist\win-x64 -MsixDir .\dist\msix -Version 0.1.0 -RequireSigned -Strict')
$lines.Add('```')
$lines.Add("")
$lines.Add("## Security Notes")
$lines.Add("")
$lines.Add("- Do not commit PFX/private-key material.")
$lines.Add("- Do not auto-install a self-signed certificate into trusted roots from app code.")
$lines.Add("- Treat trusted signing as a release-management responsibility, not app runtime behavior.")

$lines | Set-Content -LiteralPath $handoffPath -Encoding UTF8

[ordered]@{
    subject = $subjectLine
    thumbprint = $thumbprint
    public_certificate = $cerPath
    handoff = $handoffPath
    output_dir = $OutputDir
} | ConvertTo-Json
