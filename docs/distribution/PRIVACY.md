# AI Task Tracker Privacy Policy Draft

Last updated: 2026-06-29

AI Task Tracker is a local-first desktop task tracker for people working with AI agents.

## Data Stored Locally

The desktop app stores task data on the user's Windows profile under the app data directory. Local files can include:

- `snapshot.json`: current task data.
- `operations.jsonl`: user and AI agent audit log.
- `ui-preferences.json`: desktop layout and display preferences.
- `license-state.json`: local entitlement and restore-readiness state.
- backup ZIP files created by the user.

## Data Sent To External Services

The current desktop MVP does not send task data to a cloud service by default. MCP tools run locally and write to the same local task store.

Future server, sync, or mobile features must add an updated privacy notice before remote data transfer is enabled.

## AI Agent Access

Local AI agents can access tasks only through configured local MCP tools. Agent actions are recorded in the operation log with actor metadata when provided.

## Purchases And Entitlements

The current build includes a local entitlement contract and restore flow, but it does not process real payments. A future store or server-backed purchase provider must document what purchase metadata is collected and where it is processed.

## User Controls

Users can:

- Export a local backup.
- Inspect data paths in Settings.
- Copy MCP configuration details.
- Remove local app data manually from the app data directory.

## Contact

Production distribution should replace this section with a real support email, website, and legal entity.
