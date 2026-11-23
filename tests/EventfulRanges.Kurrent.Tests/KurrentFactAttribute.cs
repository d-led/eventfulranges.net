namespace EventfulRanges.Kurrent.Tests;

/// <summary>
/// A test that needs a running KurrentDB. It skips rather than fails when
/// <c>KURRENTDB_CONNECTION</c> is not set, so the ordinary gate stays honest:
/// what cannot run is reported as skipped, never as passed.
/// </summary>
public sealed class KurrentFactAttribute : FactAttribute
{
    /// <summary>The variable naming the server, as in <c>esdb://localhost:2113?tls=false</c>.</summary>
    public const string ConnectionVariable = "KURRENTDB_CONNECTION";

    /// <summary>Creates the attribute, skipping when no server is configured.</summary>
    public KurrentFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Connection))
        {
            Skip = $"{ConnectionVariable} is not set; run scripts/kurrent-up.sh first.";
        }
    }

    /// <summary>The connection string a test should use, or <c>null</c> when none is configured.</summary>
    public static string? Connection => Environment.GetEnvironmentVariable(ConnectionVariable);
}
