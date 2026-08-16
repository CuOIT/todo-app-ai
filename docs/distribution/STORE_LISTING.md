# AI Task Tracker Store Listing Draft

## Product Name

AI Task Tracker

## Short Description

Local-first task tracking for people working with AI agents.

## Full Description

AI Task Tracker helps users keep work visible while multitasking with AI tools. It combines a focused desktop task board, ClickUp-lite list view, Kanban board, task details, audit history, and local MCP access so AI agents can create and update tasks without losing context.

The MVP is designed for local-first use on Windows. Task data stays in local JSON files by default, with audit logs for user and AI updates. Future server sync and mobile CRUD clients can build on the same schema.

## Key Features

- Today Focus for active, blocked, due, and recently changed work.
- ClickUp-lite list view and four-column Kanban board.
- Task info drawer with description, status, priority, progress, assignee, due date, logs, subtasks, attachments, and audit-aware updates.
- Local MCP tools for AI agents.
- Local backup export.
- Diagnostics and release readiness reports.
- Local entitlement and purchase-restore readiness flow.

## Target Audience

Developers, builders, and power users who work with multiple AI agents and need a live task memory.

## Category

Productivity

## Age Rating Notes

No mature content is intentionally included. User-generated task text is controlled by the user.

## Privacy Notes

The current desktop MVP stores task data locally by default and does not send task data to a cloud service unless a future sync provider is added.

## Purchase Notes

The current build includes entitlement readiness plumbing but does not process real payments. Store-managed or server-backed purchases require production signing and provider integration.
