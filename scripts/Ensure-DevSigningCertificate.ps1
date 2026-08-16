param(
    [string]$Subject = "CN=AI Task Tracker",
    [int]$YearsValid = 3
)

$ErrorActionPreference = "Stop"

$certificate = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $Subject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddDays(30)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears($YearsValid)
}

$result = [ordered]@{
    subject = $certificate.Subject
    thumbprint = $certificate.Thumbprint
    not_before = $certificate.NotBefore.ToString("o")
    not_after = $certificate.NotAfter.ToString("o")
    store = "Cert:\\CurrentUser\\My"
    usage = "local dev signing only"
}

$result | ConvertTo-Json
