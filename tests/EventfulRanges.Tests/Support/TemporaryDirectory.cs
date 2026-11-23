namespace EventfulRanges.Tests;

/// <summary>A directory that removes itself when the test is done.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public string DirectoryPath { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "eventfulranges-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
