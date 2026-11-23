namespace EventfulRanges;

/// <summary>
/// How a box engine behaves: which clock stamps its operations, how the cover is
/// shaped before it is returned, and how metadata joins when boxes combine.
/// </summary>
public sealed record BoxEngineOptions
{
    /// <summary>The clock that stamps operations accepted without a stamp.</summary>
    public IClock Clock { get; init; } = new HybridClock();

    /// <summary>
    /// Rewrites the materialized cover before it is returned; the identity
    /// canonicalizer by default, which keeps boxes as the strategy produced them.
    /// </summary>
    public Canonicalizer Canonicalizer { get; init; } = boxes => boxes;

    /// <summary>How the metadata of combined boxes joins.</summary>
    public MetaMerge MetaMerge { get; init; } = Meta.Union;
}

/// <summary>
/// Thrown when an operation's box has a different number of dimensions than the
/// engine's regions: a replica is one space, and its operations share its shape.
/// </summary>
public sealed class DimensionMismatchException : ArgumentException
{
    /// <summary>Creates the exception for an operation that does not fit.</summary>
    public DimensionMismatchException(int expected, int actual)
        : base(
            FormattableString.Invariant(
                $"The engine materializes {expected}-dimensional boxes and was given a {actual}-dimensional one."),
            "ops")
    {
        Expected = expected;
        Actual = actual;
    }

    /// <summary>The dimensionality the engine holds.</summary>
    public int Expected { get; }

    /// <summary>The dimensionality of the rejected operation.</summary>
    public int Actual { get; }
}
