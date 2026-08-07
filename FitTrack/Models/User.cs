using System;

namespace FitTrack.Models;

public sealed class User
{
    public User(string username, string passwordHash, DateTimeOffset createdAtUtc)
        : this(0, username, passwordHash, 0, null, createdAtUtc)
    {
    }

    internal User(
        long userId,
        string username,
        string passwordHash,
        int failedLoginAttempts,
        DateTimeOffset? lockoutUntilUtc,
        DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        Username = username;
        PasswordHash = passwordHash;
        FailedLoginAttempts = failedLoginAttempts;
        LockoutUntilUtc = lockoutUntilUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public long UserId { get; private set; }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockoutUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
    }

    public void ApplyLockout(DateTimeOffset lockoutUntilUtc)
    {
        LockoutUntilUtc = lockoutUntilUtc.ToUniversalTime();
    }

    public void ResetLoginFailures()
    {
        FailedLoginAttempts = 0;
        LockoutUntilUtc = null;
    }

    public bool IsLocked(DateTimeOffset nowUtc)
    {
        return LockoutUntilUtc.HasValue
            && nowUtc.ToUniversalTime() < LockoutUntilUtc.Value.ToUniversalTime();
    }
}
