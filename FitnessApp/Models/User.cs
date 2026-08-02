using System;

namespace FitnessApp.Models;

public sealed class User
{
    public User(string username, string passwordHash, DateTimeOffset createdAtUtc)
    {
        Username = username;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }

    public long UserId { get; private set; }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockoutUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
