using System;

namespace FitnessApp.Models;

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
}
