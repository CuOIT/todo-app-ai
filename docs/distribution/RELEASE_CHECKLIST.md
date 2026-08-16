# AI Task Tracker Release Checklist

## Current Local Release Gates

- Build portable self-contained Windows EXE.
- Build MSIX package.
- Generate release manifests with SHA256 hashes.
- Run release readiness verifier.
- Run product readiness verifier.
- Verify Settings, Billing/IAP readiness, backup, diagnostics, and license restore flow.
- Verify local MCP configuration.
- Generate signing handoff kit with public dev certificate and production signing notes.

## Production Store Gates

- Replace draft privacy policy with reviewed final text.
- Replace draft EULA with reviewed final text.
- Configure support email, website, and publisher identity.
- Use a trusted code-signing certificate or store-managed signing.
- Keep signing private keys out of the repository and use a trusted certificate store or CI signing service.
- Replace local entitlement adapter with Microsoft Store, server-backed, or chosen purchase provider adapter.
- Validate purchase restore with the real provider.
- Create final screenshots and store assets.
- Run product readiness in strict mode.

## Known Pending Items

- Dev-signed artifacts are signed but not trusted by default because the certificate is self-signed.
- Production trusted signing requires a trusted certificate or store signing.
- Real payment processing is not enabled in the desktop MVP.
