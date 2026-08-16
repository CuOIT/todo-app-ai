using Microsoft.EntityFrameworkCore;

namespace AiTaskTracker.Server.Data;

public sealed class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();
    public DbSet<WorkspaceMemberEntity> WorkspaceMembers => Set<WorkspaceMemberEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<SubtaskEntity> Subtasks => Set<SubtaskEntity>();
    public DbSet<TaskLogEntity> TaskLogs => Set<TaskLogEntity>();
    public DbSet<AttachmentEntity> Attachments => Set<AttachmentEntity>();
    public DbSet<AuditEntity> Audits => Set<AuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<UserEntity>();
        user.ToTable("users");
        user.HasKey(item => item.Id);
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.Email).HasMaxLength(320);
        user.Property(item => item.DisplayName).HasMaxLength(128);

        var workspace = modelBuilder.Entity<WorkspaceEntity>();
        workspace.ToTable("workspaces");
        workspace.HasKey(item => item.Id);
        workspace.HasIndex(item => item.InviteCode).IsUnique();
        workspace.Property(item => item.Name).HasMaxLength(200);
        workspace.Property(item => item.InviteCode).HasMaxLength(32);

        var membership = modelBuilder.Entity<WorkspaceMemberEntity>();
        membership.ToTable("workspace_members");
        membership.HasKey(item => new { item.WorkspaceId, item.UserId });
        membership.HasOne(item => item.Workspace)
            .WithMany(item => item.Members)
            .HasForeignKey(item => item.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        membership.HasOne(item => item.User)
            .WithMany(item => item.Memberships)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var session = modelBuilder.Entity<SessionEntity>();
        session.ToTable("sessions");
        session.HasKey(item => item.Id);
        session.HasIndex(item => item.TokenHash).IsUnique();
        session.HasIndex(item => item.ExpiresAtUtc);
        session.HasOne(item => item.User)
            .WithMany(item => item.Sessions)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var task = modelBuilder.Entity<TaskEntity>();
        task.ToTable("tasks");
        task.HasKey(item => item.Id);
        task.HasIndex(item => item.ShortId);
        task.HasIndex(item => new { item.WorkspaceId, item.IsDeleted, item.Status, item.UpdatedAtUtc });
        task.Property(item => item.Title).HasMaxLength(500);
        task.Property(item => item.Status).HasMaxLength(32);
        task.Property(item => item.Priority).HasMaxLength(32);
        task.Property(item => item.ProjectId).HasMaxLength(128);
        task.Property(item => item.ListId).HasMaxLength(128);
        task.Property(item => item.Assignee).HasMaxLength(256);
        task.HasOne(item => item.Workspace)
            .WithMany(item => item.Tasks)
            .HasForeignKey(item => item.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        var subtask = modelBuilder.Entity<SubtaskEntity>();
        subtask.ToTable("subtasks");
        subtask.HasKey(item => item.Id);
        subtask.HasIndex(item => item.TaskId);
        subtask.HasOne(item => item.Task)
            .WithMany(item => item.Subtasks)
            .HasForeignKey(item => item.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        var taskLog = modelBuilder.Entity<TaskLogEntity>();
        taskLog.ToTable("task_logs");
        taskLog.HasKey(item => item.Id);
        taskLog.HasIndex(item => item.TaskId);
        taskLog.HasOne(item => item.Task)
            .WithMany(item => item.Logs)
            .HasForeignKey(item => item.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        var attachment = modelBuilder.Entity<AttachmentEntity>();
        attachment.ToTable("attachments");
        attachment.HasKey(item => item.Id);
        attachment.HasIndex(item => item.TaskId);
        attachment.HasOne(item => item.Task)
            .WithMany(item => item.Attachments)
            .HasForeignKey(item => item.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        var audit = modelBuilder.Entity<AuditEntity>();
        audit.ToTable("audits");
        audit.HasKey(item => item.OperationId);
        audit.HasIndex(item => new { item.TaskId, item.TimestampUtc });
        audit.HasOne(item => item.Task)
            .WithMany(item => item.Audits)
            .HasForeignKey(item => item.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
