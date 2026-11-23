using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// An immutable box operation, the atomic unit of the n-dimensional event log.
/// </summary>
/// <param name="Id">Process-unique, roughly time-ordered identifier.</param>
/// <param name="Kind">Whether the box joins, leaves, or is cancelled.</param>
/// <param name="Box">The region the operation applies to.</param>
/// <param name="Timestamp">
/// The stamp used to order concurrent operations. Zero means "not stamped yet"; an
/// engine stamps it when the operation is applied.
/// </param>
/// <param name="Ref">For a retraction, the identifier of the operation it cancels.</param>
/// <param name="Origin">Who produced the operation, when the producer says so.</param>
public sealed record BoxOp(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] BoxOpKind Kind,
    [property: JsonPropertyName("box")] Box Box,
    [property: JsonPropertyName("ts")] long Timestamp = 0,
    [property: JsonPropertyName("ref")] string? Ref = null,
    [property: JsonPropertyName("origin")] string? Origin = null)
{
    /// <summary>Reports the first reason the operation is not well-formed, if any.</summary>
    public BoxOpError Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            return BoxOpError.MissingId;
        }

        var kindError = ValidateKind();
        if (kindError != BoxOpError.None)
        {
            return kindError;
        }

        if (!Box.IsValid)
        {
            return BoxOpError.InvalidBox;
        }

        return Box.IsEmpty ? BoxOpError.EmptyBox : BoxOpError.None;
    }

    /// <summary>Whether the kind is known, and a retraction names its target.</summary>
    private BoxOpError ValidateKind() => Kind switch
    {
        BoxOpKind.Add or BoxOpKind.Remove => BoxOpError.None,
        BoxOpKind.Retract => string.IsNullOrEmpty(Ref) ? BoxOpError.MissingRetractTarget : BoxOpError.None,
        _ => BoxOpError.UnknownKind,
    };

    /// <summary>Throws when the operation is not well-formed.</summary>
    /// <exception cref="InvalidBoxOpException">The operation failed validation.</exception>
    public void EnsureValid()
    {
        var error = Validate();
        if (error != BoxOpError.None)
        {
            throw new InvalidBoxOpException(this, error);
        }
    }

    /// <summary>Creates an operation of the given kind with a freshly generated identifier.</summary>
    public static BoxOp Create(BoxOpKind kind, Box box) => new(Ids.New(), kind, box);

    /// <summary>Creates an addition over the half-open box <c>[min, max)</c>.</summary>
    public static BoxOp Add(IReadOnlyList<double> min, IReadOnlyList<double> max) =>
        Create(BoxOpKind.Add, new Box(min, max));

    /// <summary>Creates an addition with explicit inclusivity per face.</summary>
    public static BoxOp AddWithBounds(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        IReadOnlyList<Bound>? minBound,
        IReadOnlyList<Bound>? maxBound) =>
        Create(BoxOpKind.Add, new Box(min, max, minBound, maxBound));

    /// <summary>Creates a removal over the half-open box <c>[min, max)</c>.</summary>
    public static BoxOp Remove(IReadOnlyList<double> min, IReadOnlyList<double> max) =>
        Create(BoxOpKind.Remove, new Box(min, max));

    /// <summary>Creates a removal with explicit inclusivity per face.</summary>
    public static BoxOp RemoveWithBounds(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        IReadOnlyList<Bound>? minBound,
        IReadOnlyList<Bound>? maxBound) =>
        Create(BoxOpKind.Remove, new Box(min, max, minBound, maxBound));

    /// <summary>
    /// Creates a retraction cancelling the operation named <paramref name="ref"/>. It
    /// carries that operation's box, so the retraction is expressed in the same
    /// region it undoes.
    /// </summary>
    public static BoxOp Retract(string @ref, Box box) =>
        new(Ids.New(), BoxOpKind.Retract, box, Ref: @ref);

    /// <summary>Renders the operation as <c>add [0 10) x [2 5)</c>.</summary>
    public override string ToString() => FormattableString.Invariant($"{Kind.ToWireName()} {Box}");
}
