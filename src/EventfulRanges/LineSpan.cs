namespace EventfulRanges;

/// <summary>
/// A half-open interval <c>[start, end)</c> of a line's parameter.
/// </summary>
/// <remarks>
/// The Go original calls this <c>Span</c>; the name is avoided here because it
/// would shadow <see cref="System.Span{T}"/> for anyone importing this namespace.
/// </remarks>
/// <param name="Start">Where the line enters.</param>
/// <param name="End">Where the line leaves.</param>
public readonly record struct LineSpan(double Start, double End)
{
    /// <summary>Whether the span has no positive length.</summary>
    public bool IsEmpty => Start >= End;
}
