using System;
using System.Threading.Tasks;
using FitnessApp.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitnessApp.Tests.Repositories;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task AddAndFindAsync_PersistsAllUserFields_AndUsernameLookupIsCaseInsensitive()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var createdAtUtc = new DateTimeOffset(2026, 8, 3, 1, 2, 3, TimeSpan.FromHours(6.5));
        var user = new User("Oak01", "fake-test-hash", createdAtUtc);

        var userId = await database.Users.AddAsync(user);
        var byId = await database.Users.FindByIdAsync(userId);
        var byUsername = await database.Users.FindByUsernameAsync("oAk01");

        Assert.NotNull(byId);
        Assert.NotNull(byUsername);
        Assert.Equal(userId, byId!.UserId);
        Assert.Equal(userId, byUsername!.UserId);
        Assert.Equal("Oak01", byId.Username);
        Assert.Equal("Oak01", byUsername.Username);
        Assert.Equal("fake-test-hash", byId.PasswordHash);
        Assert.Equal(0, byId.FailedLoginAttempts);
        Assert.Null(byId.LockoutUntilUtc);
        Assert.Equal(createdAtUtc.ToUniversalTime(), byId.CreatedAtUtc);
        Assert.Equal(byId.CreatedAtUtc, byUsername.CreatedAtUtc);
    }

    [Fact]
    public async Task AddAsync_RejectsCaseInsensitiveDuplicateUsername_WithConstraintInnerException()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        await database.Users.AddAsync(new User(
            "Oak01",
            "fake-first-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Users.AddAsync(
            new User(
                "oak01",
                "fake-second-hash",
                new DateTimeOffset(2026, 8, 3, 0, 0, 1, TimeSpan.Zero))));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);

        Assert.Equal(19, sqliteException.SqliteErrorCode & 0xFF);

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users;";
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task UpdateLoginStateAsync_UpdatesAndResetsOnlyLoginStateFields()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var createdAtUtc = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        var userId = await database.Users.AddAsync(new User("state-user", "fake-hash", createdAtUtc));
        var lockoutUntilUtc = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.FromHours(6.5));

        var lockedUser = new User(
            userId,
            "state-user",
            "fake-hash",
            3,
            lockoutUntilUtc,
            createdAtUtc);
        await database.Users.UpdateLoginStateAsync(lockedUser);

        var updatedUser = await database.Users.FindByIdAsync(userId);
        Assert.NotNull(updatedUser);
        Assert.Equal(3, updatedUser!.FailedLoginAttempts);
        Assert.Equal(lockoutUntilUtc.ToUniversalTime(), updatedUser.LockoutUntilUtc);
        Assert.Equal("state-user", updatedUser.Username);
        Assert.Equal("fake-hash", updatedUser.PasswordHash);
        Assert.Equal(createdAtUtc, updatedUser.CreatedAtUtc);

        var resetUser = new User(
            userId,
            "state-user",
            "fake-hash",
            0,
            null,
            createdAtUtc);
        await database.Users.UpdateLoginStateAsync(resetUser);

        var resetStoredUser = await database.Users.FindByIdAsync(userId);
        Assert.NotNull(resetStoredUser);
        Assert.Equal(0, resetStoredUser!.FailedLoginAttempts);
        Assert.Null(resetStoredUser.LockoutUntilUtc);
        Assert.Equal("state-user", resetStoredUser.Username);
        Assert.Equal("fake-hash", resetStoredUser.PasswordHash);
        Assert.Equal(createdAtUtc, resetStoredUser.CreatedAtUtc);
    }
}
