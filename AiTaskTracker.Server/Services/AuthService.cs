using System.Security.Cryptography;
using System.Text;
using AiTaskTracker.Server.Contracts;
using AiTaskTracker.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace AiTaskTracker.Server.Services;

public sealed class AuthService
{
    private const int PasswordIterations = 120_000;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private readonly TaskDbContext _db;

    public AuthService(TaskDbContext db)
    {
        _db = db;
    }

    public async Task<(AuthResponse? Response, string? Error)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await _db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return (null, "email_already_exists");
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var user = new UserEntity
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = HashPassword(request.Password, salt),
            CreatedAtUtc = DateTime.UtcNow
        };
        var workspace = new WorkspaceEntity
        {
            Name = request.WorkspaceName.Trim(),
            InviteCode = CreateInviteCode(),
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        workspace.Members.Add(new WorkspaceMemberEntity
        {
            User = user,
            UserId = user.Id,
            Role = "owner",
            JoinedAtUtc = DateTime.UtcNow
        });

        _db.Users.Add(user);
        _db.Workspaces.Add(workspace);
        var session = CreateSession(user);
        _db.Sessions.Add(session.Entity);
        await _db.SaveChangesAsync(cancellationToken);

        return (await BuildResponseAsync(user, session.RawToken, session.Entity.ExpiresAtUtc, cancellationToken), null);
    }

    public async Task<(AuthResponse? Response, string? Error)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(
            item => item.Email == email && item.IsActive,
            cancellationToken);
        if (user is null || !VerifyPassword(request.Password, user.PasswordSalt, user.PasswordHash))
        {
            return (null, "invalid_credentials");
        }

        var session = CreateSession(user);
        _db.Sessions.Add(session.Entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (await BuildResponseAsync(user, session.RawToken, session.Entity.ExpiresAtUtc, cancellationToken), null);
    }

    public async Task<UserEntity?> ResolveUserAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(rawToken);
        var session = await _db.Sessions
            .Include(item => item.User)
            .FirstOrDefaultAsync(
                item => item.TokenHash == tokenHash && item.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
        return session?.User is { IsActive: true } ? session.User : null;
    }

    public async Task<List<WorkspaceDto>> GetWorkspacesAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.Workspace!.Name)
            .Select(member => new WorkspaceDto(
                member.WorkspaceId,
                member.Workspace!.Name,
                member.Role,
                member.Workspace.InviteCode,
                new DateTimeOffset(DateTime.SpecifyKind(member.JoinedAtUtc, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(
        UserEntity user,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = new WorkspaceEntity
        {
            Name = request.Name.Trim(),
            InviteCode = CreateInviteCode(),
            CreatedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        var membership = new WorkspaceMemberEntity
        {
            Workspace = workspace,
            WorkspaceId = workspace.Id,
            User = user,
            UserId = user.Id,
            Role = "owner",
            JoinedAtUtc = DateTime.UtcNow
        };
        _db.Workspaces.Add(workspace);
        _db.WorkspaceMembers.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(membership);
    }

    public async Task<(WorkspaceDto? Workspace, string? Error)> JoinWorkspaceAsync(
        UserEntity user,
        JoinWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var inviteCode = request.InviteCode.Trim().ToUpperInvariant();
        var workspace = await _db.Workspaces.FirstOrDefaultAsync(
            item => item.InviteCode == inviteCode,
            cancellationToken);
        if (workspace is null)
        {
            return (null, "invalid_invite_code");
        }

        var existing = await _db.WorkspaceMembers
            .Include(member => member.Workspace)
            .FirstOrDefaultAsync(
                member => member.WorkspaceId == workspace.Id && member.UserId == user.Id,
                cancellationToken);
        if (existing is not null)
        {
            return (ToDto(existing), null);
        }

        var membership = new WorkspaceMemberEntity
        {
            Workspace = workspace,
            WorkspaceId = workspace.Id,
            User = user,
            UserId = user.Id,
            Role = "member",
            JoinedAtUtc = DateTime.UtcNow
        };
        _db.WorkspaceMembers.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);
        return (ToDto(membership), null);
    }

    public async Task<bool> HasWorkspaceAccessAsync(
        string userId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        return await _db.WorkspaceMembers.AnyAsync(
            member => member.UserId == userId && member.WorkspaceId == workspaceId,
            cancellationToken);
    }

    private async Task<AuthResponse> BuildResponseAsync(
        UserEntity user,
        string rawToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var workspaces = await GetWorkspacesAsync(user.Id, cancellationToken);
        return new AuthResponse(
            rawToken,
            ToOffset(expiresAtUtc),
            new UserDto(user.Id, user.Email, user.DisplayName),
            workspaces);
    }

    private static (SessionEntity Entity, string RawToken) CreateSession(UserEntity user)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
        var now = DateTime.UtcNow;
        return (new SessionEntity
        {
            User = user,
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(SessionLifetime)
        }, rawToken);
    }

    private static string HashPassword(string password, byte[] salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string saltBase64, string expectedHashBase64)
    {
        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var expected = Convert.FromBase64String(expectedHashBase64);
            var actual = Convert.FromBase64String(HashPassword(password, salt));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string HashToken(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    private static string CreateInviteCode()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static WorkspaceDto ToDto(WorkspaceMemberEntity membership)
    {
        return new WorkspaceDto(
            membership.WorkspaceId,
            membership.Workspace?.Name ?? "",
            membership.Role,
            membership.Workspace?.InviteCode ?? "",
            ToOffset(membership.JoinedAtUtc));
    }

    private static DateTimeOffset ToOffset(DateTime utc)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }
}
