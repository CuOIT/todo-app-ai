using System;
using AiTaskTracker.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTaskTracker.Server.Migrations;

[DbContext(typeof(TaskDbContext))]
[Migration("202607050001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                Email = table.Column<string>(maxLength: 320, nullable: false),
                DisplayName = table.Column<string>(maxLength: 128, nullable: false),
                PasswordHash = table.Column<string>(nullable: false),
                PasswordSalt = table.Column<string>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "workspaces",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                InviteCode = table.Column<string>(maxLength: 32, nullable: false),
                CreatedByUserId = table.Column<string>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspaces", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "sessions",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TokenHash = table.Column<string>(nullable: false),
                UserId = table.Column<string>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_sessions_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tasks",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                WorkspaceId = table.Column<string>(nullable: false),
                ShortId = table.Column<string>(nullable: false),
                Title = table.Column<string>(maxLength: 500, nullable: false),
                Notes = table.Column<string>(nullable: false),
                Status = table.Column<string>(maxLength: 32, nullable: false),
                Priority = table.Column<string>(maxLength: 32, nullable: false),
                ProgressPercent = table.Column<int>(nullable: false),
                ProjectId = table.Column<string>(maxLength: 128, nullable: false),
                ListId = table.Column<string>(maxLength: 128, nullable: false),
                Assignee = table.Column<string>(maxLength: 256, nullable: false),
                TagsJson = table.Column<string>(nullable: false),
                Estimate = table.Column<string>(nullable: false),
                StartDateUtc = table.Column<DateTime>(nullable: true),
                DueDateUtc = table.Column<DateTime>(nullable: true),
                BlockedByTaskIdsJson = table.Column<string>(nullable: false),
                IsPinned = table.Column<bool>(nullable: false),
                IsDeleted = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(nullable: false),
                CreatedBy = table.Column<string>(nullable: false),
                UpdatedBy = table.Column<string>(nullable: false),
                CreatedByUserId = table.Column<string>(nullable: false),
                UpdatedByUserId = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_tasks_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "workspace_members",
            columns: table => new
            {
                WorkspaceId = table.Column<string>(nullable: false),
                UserId = table.Column<string>(nullable: false),
                Role = table.Column<string>(nullable: false),
                JoinedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_members", x => new { x.WorkspaceId, x.UserId });
                table.ForeignKey(
                    name: "FK_workspace_members_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_workspace_members_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "attachments",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TaskId = table.Column<string>(nullable: false),
                Type = table.Column<string>(nullable: false),
                Title = table.Column<string>(nullable: false),
                Target = table.Column<string>(nullable: false),
                Note = table.Column<string>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_attachments_tasks_TaskId",
                    column: x => x.TaskId,
                    principalTable: "tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "audits",
            columns: table => new
            {
                OperationId = table.Column<string>(nullable: false),
                TaskId = table.Column<string>(nullable: false),
                ActorType = table.Column<string>(nullable: false),
                ActorName = table.Column<string>(nullable: false),
                Source = table.Column<string>(nullable: false),
                Action = table.Column<string>(nullable: false),
                TimestampUtc = table.Column<DateTime>(nullable: false),
                ChangedFieldsJson = table.Column<string>(nullable: false),
                BeforeJson = table.Column<string>(nullable: false),
                AfterJson = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audits", x => x.OperationId);
                table.ForeignKey(
                    name: "FK_audits_tasks_TaskId",
                    column: x => x.TaskId,
                    principalTable: "tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "subtasks",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TaskId = table.Column<string>(nullable: false),
                Title = table.Column<string>(nullable: false),
                Status = table.Column<string>(nullable: false),
                ProgressPercent = table.Column<int>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_subtasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_subtasks_tasks_TaskId",
                    column: x => x.TaskId,
                    principalTable: "tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "task_logs",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                TaskId = table.Column<string>(nullable: false),
                ActorType = table.Column<string>(nullable: false),
                ActorName = table.Column<string>(nullable: false),
                Message = table.Column<string>(nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_task_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_task_logs_tasks_TaskId",
                    column: x => x.TaskId,
                    principalTable: "tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_attachments_TaskId", table: "attachments", column: "TaskId");
        migrationBuilder.CreateIndex(name: "IX_audits_TaskId_TimestampUtc", table: "audits", columns: new[] { "TaskId", "TimestampUtc" });
        migrationBuilder.CreateIndex(name: "IX_sessions_ExpiresAtUtc", table: "sessions", column: "ExpiresAtUtc");
        migrationBuilder.CreateIndex(name: "IX_sessions_TokenHash", table: "sessions", column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_sessions_UserId", table: "sessions", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_subtasks_TaskId", table: "subtasks", column: "TaskId");
        migrationBuilder.CreateIndex(name: "IX_task_logs_TaskId", table: "task_logs", column: "TaskId");
        migrationBuilder.CreateIndex(name: "IX_tasks_ShortId", table: "tasks", column: "ShortId");
        migrationBuilder.CreateIndex(name: "IX_tasks_WorkspaceId_IsDeleted_Status_UpdatedAtUtc", table: "tasks", columns: new[] { "WorkspaceId", "IsDeleted", "Status", "UpdatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_users_Email", table: "users", column: "Email", unique: true);
        migrationBuilder.CreateIndex(name: "IX_workspace_members_UserId", table: "workspace_members", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_workspaces_InviteCode", table: "workspaces", column: "InviteCode", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "attachments");
        migrationBuilder.DropTable(name: "audits");
        migrationBuilder.DropTable(name: "sessions");
        migrationBuilder.DropTable(name: "subtasks");
        migrationBuilder.DropTable(name: "task_logs");
        migrationBuilder.DropTable(name: "workspace_members");
        migrationBuilder.DropTable(name: "tasks");
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "workspaces");
    }
}
