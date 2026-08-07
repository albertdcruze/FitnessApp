using System;
using System.IO;
using System.Text;
using FitTrack.Services;
using Xunit;

namespace FitTrack.Tests.Services;

public sealed class RememberedCredentialsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsCredentialsWithoutWritingPlaintext()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(directoryPath, "remembered-credentials.dat");
            var store = new RememberedCredentialsStore(filePath);

            store.Save("RememberedUser01", "FitnessPass1");

            var loadedCredentials = store.Load();

            Assert.NotNull(loadedCredentials);
            Assert.Equal("RememberedUser01", loadedCredentials!.Username);
            Assert.Equal("FitnessPass1", loadedCredentials.Password);
            Assert.True(File.Exists(filePath));

            var fileContents = Encoding.UTF8.GetString(File.ReadAllBytes(filePath));
            Assert.DoesNotContain("FitnessPass1", fileContents, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporaryDirectory(directoryPath);
        }
    }

    [Fact]
    public void Clear_RemovesRememberedCredentials()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            var store = new RememberedCredentialsStore(
                Path.Combine(directoryPath, "remembered-credentials.dat"));
            store.Save("RememberedUser01", "FitnessPass1");

            store.Clear();

            Assert.Null(store.Load());
        }
        finally
        {
            DeleteTemporaryDirectory(directoryPath);
        }
    }

    [Fact]
    public void Load_IgnoresCorruptCredentialFiles()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(directoryPath, "remembered-credentials.dat");
            File.WriteAllBytes(filePath, [1, 2, 3, 4]);

            var store = new RememberedCredentialsStore(filePath);

            Assert.Null(store.Load());
        }
        finally
        {
            DeleteTemporaryDirectory(directoryPath);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "FitTrackTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteTemporaryDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
