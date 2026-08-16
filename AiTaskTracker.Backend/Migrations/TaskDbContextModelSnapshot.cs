using System;
using AiTaskTracker.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AiTaskTracker.Server.Migrations;

[DbContext(typeof(TaskDbContext))]
partial class TaskDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "7.0.20");

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Email).IsUnique();
            entity.Property(item => item.Email).HasMaxLength(320);
            entity.Property(item => item.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<WorkspaceEntity>(entity =>
        {
            entity.ToTable("workspaces");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.InviteCode).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.InviteCode).HasMaxLength(32);
        });

        modelBuilder.Entity<WorkspaceMemberEntity>(entity =>
        {
            entity.ToTable("workspace_members");
            entity.HasKey(item => new { item.WorkspaceId, item.UserId });
            entity.HasIndex(item => item.UserId);
            entity.HasOne(item => item.Workspace)
                .WithMany(item => item.Members)
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.User)
                .WithMany(item => item.Memberships)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionEntity>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAtUtc);
            entity.HasIndex(item => item.UserId);
            entity.HasOne(item => item.User)
                .WithMany(item => item.Sessions)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.ToTable("tasks");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.ShortId);
            entity.HasIndex(item => new { item.WorkspaceId, item.IsDeleted, item.Status, item.UpdatedAtUtc });
            entity.Property(item => item.Title).HasMaxLength(500);
            entity.Property(item => item.Status).HasMaxLength(32);
            entity.Property(item => item.Priority).HasMaxLength(32);
            entity.Property(item => item.ProjectId).HasMaxLength(128);
            entity.Property(item => item.ListId).HasMaxLength(128);
            entity.Property(item => item.Assignee).HasMaxLength(256);
            entity.HasOne(item => item.Workspace)
                .WithMany(item => item.Tasks)
                .HasForeignKey(item => item.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubtaskEntity>(entity =>
        {
            entity.ToTable("subtasks");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TaskId);
            entity.HasOne(item => item.Task)
                .WithMany(item => item.Subtasks)
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskLogEntity>(entity =>
        {
            entity.ToTable("task_logs");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TaskId);
            entity.HasOne(item => item.Task)
                .WithMany(item => item.Logs)
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttachmentEntity>(entity =>
        {
            entity.ToTable("attachments");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TaskId);
            entity.HasOne(item => item.Task)
                .WithMany(item => item.Attachments)
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEntity>(entity =>
        {
            entity.ToTable("audits");
            entity.HasKey(item => item.OperationId);
            entity.HasIndex(item => new { item.TaskId, item.TimestampUtc });
            entity.HasOne(item => item.Task)
                .WithMany(item => item.Audits)
                .HasForeignKey(item => item.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
