using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Users;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Accounts in a list, as both the sign-in repository and the user-management one. Admin holds AdminUsers.
/// <see cref="Fail"/> makes every read throw, standing in for the store being unreachable.
/// </summary>
public sealed class FakeAccounts : IUserRepository, IUserAccountRepository
{
    public List<User> Users { get; } = [];
    public List<Role> Roles { get; } = [Fakes.Roles.Admin(), Fakes.Roles.Member()];
    public List<User> Added { get; } = [];
    public List<Guid> Discarded { get; } = [];
    public Exception? Fail { get; set; }

    public User Add(string email, Guid roleId, bool isActive = true, string? passwordHash = "hash:secret")
    {
        var user = new User
        {
            FullName = "Người Dùng",
            Email = email,
            RoleId = roleId,
            Role = Roles.First(r => r.Id == roleId),
            IsActive = isActive,
            PasswordHash = passwordHash,
        };
        Users.Add(user);
        return user;
    }

    private Task<T> Read<T>(Func<T> answer)
        => Fail is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Read(() => Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Read(() => Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindByGoogleSubjectIdAsync(string? googleSubjectId, CancellationToken ct = default)
        => Read(() => Users.FirstOrDefault(u => u.GoogleSubjectId is not null && u.GoogleSubjectId == googleSubjectId));

    public Task<User?> LinkGoogleSubjectByEmailAsync(string email, string googleSubjectId, CancellationToken ct = default)
        => Read(() =>
        {
            var user = Users.FirstOrDefault(u => u.Email == email && u.GoogleSubjectId is null && u.IsActive);
            if (user is not null) user.GoogleSubjectId = googleSubjectId;
            return user;
        });

    public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default)
        => Read<IReadOnlyCollection<Capability>>(() => roleId == BuiltInRoles.Admin ? [Capability.AdminUsers] : []);

    public Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct = default)
        => Read<IReadOnlyCollection<User>>(() => [.. Users]);

    public Task<string?> FindActiveEmailAsync(Guid id, CancellationToken ct = default)
        => Read(() => Users.FirstOrDefault(u => u.Id == id && u.IsActive)?.Email);

    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct = default) => Read(() => Roles.FirstOrDefault(r => r.Id == roleId));

    public Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken ct = default)
        => Read(() => Users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) && u.Id != exceptId));

    public Task<int> CountActiveWithCapabilityAsync(Capability capability, Guid exceptId, CancellationToken ct = default)
        => Read(() => Users.Count(u => u.IsActive && u.Id != exceptId && u.RoleId == BuiltInRoles.Admin));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        Added.Add(user);
        return Task.CompletedTask;
    }

    public Task DiscardChangesAsync(User user, CancellationToken ct = default)
    {
        Discarded.Add(user.Id);
        return Task.CompletedTask;
    }
}

/// <summary>Reversible on purpose: the "hash" of <c>x</c> is <c>hash:x</c>.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;
    public bool Verify(string passwordHash, string providedPassword) => passwordHash == Hash(providedPassword);
}

public sealed class FakeTokenIssuer : ITokenIssuer
{
    public string CreateAccessToken(User user, IReadOnlyCollection<Capability> capabilities) => "access:" + user.Id;
    public string CreateRefreshToken() => "refresh:" + Guid.NewGuid();
    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);
}

/// <summary>Refresh tokens in a dictionary, single-use like the real store.</summary>
public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    public Dictionary<string, Guid> Live { get; } = [];
    public List<Guid> RevokedAll { get; } = [];

    /// <summary>Runs first on every revoke-all, so a test can see what had happened by then.</summary>
    public Action? OnRevokeAll { get; set; }

    public Task StoreAsync(string refreshToken, Guid userId, TimeSpan lifetime, CancellationToken ct = default)
    {
        Live[refreshToken] = userId;
        return Task.CompletedTask;
    }

    public Task<Guid?> PeekAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult(Live.TryGetValue(refreshToken, out var id) ? id : (Guid?)null);

    public Task<Guid?> ConsumeAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult(Live.Remove(refreshToken, out var id) ? id : (Guid?)null);

    public Task<Guid?> RevokeAsync(string refreshToken, CancellationToken ct = default) => ConsumeAsync(refreshToken, ct);

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        OnRevokeAll?.Invoke();
        RevokedAll.Add(userId);
        foreach (var token in Live.Where(t => t.Value == userId).Select(t => t.Key).ToList()) Live.Remove(token);
        return Task.CompletedTask;
    }
}

public sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public GoogleIdentity? Identity { get; set; }
    public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default) => Task.FromResult(Identity);
}

/// <summary>One live reset link per account; a created link works only once activated, like the real store.</summary>
public sealed class FakePasswordResetTokenStore : IPasswordResetTokenStore
{
    public Dictionary<string, Guid> Live { get; } = [];
    public List<Guid> Revoked { get; } = [];

    public Task<string> CreateAsync(Guid userId, TimeSpan lifetime, CancellationToken ct = default)
        => Task.FromResult("reset:" + Guid.NewGuid());

    public Task ActivateAsync(Guid userId, string token, TimeSpan lifetime, CancellationToken ct = default)
    {
        Live[token] = userId;
        return Task.CompletedTask;
    }

    public Task<Guid?> PeekAsync(string token, CancellationToken ct = default)
        => Task.FromResult(Live.TryGetValue(token, out var id) ? id : (Guid?)null);

    public Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default)
        => Task.FromResult(Live.Remove(token, out var id) ? id : (Guid?)null);

    public Task RevokeAsync(Guid userId, CancellationToken ct = default)
    {
        Revoked.Add(userId);
        return Task.CompletedTask;
    }
}

public sealed class FakePasswordResetEmailSender : IPasswordResetEmailSender
{
    public bool Delivers { get; set; } = true;
    public List<string> SentTo { get; } = [];

    public Task<bool> SendAsync(string toAddress, string fullName, string resetToken, TimeSpan validFor, CancellationToken ct = default)
    {
        if (Delivers) SentTo.Add(toAddress);
        return Task.FromResult(Delivers);
    }
}

public sealed class FakeVoiceIdErasure : IVoiceIdErasure
{
    public List<Guid> Ended { get; } = [];

    public Task<bool> EndAsync(Guid userId, Guid actorId, VoiceIdEndReason reason, CancellationToken ct)
    {
        Ended.Add(userId);
        return Task.FromResult(false);
    }
}
