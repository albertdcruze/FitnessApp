using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace FitTrack.Services;

public sealed record RememberedCredentials(string Username, string Password);

public sealed class RememberedCredentialsStore
{
    private readonly string _filePath;

    public RememberedCredentialsStore()
        : this(GetDefaultFilePath())
    {
    }

    public RememberedCredentialsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A credential file path is required.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public RememberedCredentials? Load()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            if (!File.Exists(_filePath))
            {
                return null;
            }

            var encryptedData = File.ReadAllBytes(_filePath);
            if (encryptedData.Length == 0)
            {
                return null;
            }

            var protectedData = ProtectedData.Unprotect(
                encryptedData,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            var credentials = JsonSerializer.Deserialize<RememberedCredentials>(protectedData);

            return credentials is not null
                   && !string.IsNullOrWhiteSpace(credentials.Username)
                   && !string.IsNullOrEmpty(credentials.Password)
                ? credentials
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    public void Save(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            Clear();
            return;
        }

        string? temporaryFilePath = null;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(
                new RememberedCredentials(username, password));
            var encryptedData = ProtectedData.Protect(
                payload,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

            var directoryPath = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            temporaryFilePath = string.Concat(
                _filePath,
                ".",
                Guid.NewGuid().ToString("N"),
                ".tmp");
            File.WriteAllBytes(temporaryFilePath, encryptedData);
            File.Move(temporaryFilePath, _filePath, overwrite: true);
        }
        catch (IOException)
        {
            DeleteTemporaryFile(temporaryFilePath);
        }
        catch (UnauthorizedAccessException)
        {
            DeleteTemporaryFile(temporaryFilePath);
        }
        catch (CryptographicException)
        {
            DeleteTemporaryFile(temporaryFilePath);
        }
        catch (PlatformNotSupportedException)
        {
            DeleteTemporaryFile(temporaryFilePath);
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetDefaultFilePath()
    {
        var directoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FitTrack");

        return Path.Combine(directoryPath, "remembered-credentials.dat");
    }

    private static void DeleteTemporaryFile(string? temporaryFilePath)
    {
        if (string.IsNullOrEmpty(temporaryFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
