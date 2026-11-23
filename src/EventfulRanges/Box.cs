using System.Collections.Immutable;

namespace EventfulRanges;

/// <summary>
/// An axis-aligned n-dimensional rectangle. Each of its 2n faces is either
/// included (closed) or excluded (open). Built from two corners it is
/// half-open — every lower face closed, every upper face open — which is the
/// canonical form the algebra returns to.
/// </summary>
/// <remarks>
/// Coordinates are compared, never combined, so no rounding error accumulates.
/// A box is not required to be well-formed or to hold a point: <see
/// cref="Validate"/> names what is wrong with it, and <see cref="IsEmpty"/>
/// says whether it covers anything. <see cref="Equals(Box)"/> and <see
/// cref="CompareTo(Box)"/> describe geometry only; <see cref="Meta"/> is opaque
/// JSON that the algebra joins when boxes merge, never part of a box's identity.
/// </remarks>
public sealed class Box : IEquatable<Box>, IComparable<Box>
{
    /// <summary>Whether the boxes cover the same region.</summary>
    public static bool operator ==(Box? left, Box? right) => Equals(left, right);

    /// <summary>Whether the boxes cover different regions.</summary>
    public static bool operator !=(Box? left, Box? right) => !Equals(left, right);

    /// <summary>Whether <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(Box? left, Box? right) => Compare(left, right) < 0;

    /// <summary>Whether <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(Box? left, Box? right) => Compare(left, right) > 0;

    /// <summary>Whether <paramref name="left"/> sorts at or before <paramref name="right"/>.</summary>
    public static bool operator <=(Box? left, Box? right) => Compare(left, right) <= 0;

    /// <summary>Whether <paramref name="left"/> sorts at or after <paramref name="right"/>.</summary>
    public static bool operator >=(Box? left, Box? right) => Compare(left, right) >= 0;

    /// <summary>Creates the half-open box <c>[min, max)</c>.</summary>
    public Box(IReadOnlyList<double> min, IReadOnlyList<double> max)
        : this(min, max, null, null)
    {
    }

    /// <summary>
    /// Creates a box with explicit per-face inclusivity. A bound slice that is
    /// <c>null</c> or empty means the default: every lower face closed, every
    /// upper face open.
    /// </summary>
    public Box(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        IReadOnlyList<Bound>? minBound,
        IReadOnlyList<Bound>? maxBound)
    {
        ArgumentNullException.ThrowIfNull(min);
        ArgumentNullException.ThrowIfNull(max);

        Min = [.. min];
        Max = [.. max];
        MinBound = minBound is null ? [] : [.. minBound];
        MaxBound = maxBound is null ? [] : [.. maxBound];
    }

    /// <summary>The lower corner, one coordinate per axis.</summary>
    public ImmutableArray<double> Min { get; }

    /// <summary>The upper corner, one coordinate per axis.</summary>
    public ImmutableArray<double> Max { get; }

    /// <summary>
    /// Inclusivity of the lower faces, one per axis; empty means every lower
    /// face is closed.
    /// </summary>
    public ImmutableArray<Bound> MinBound { get; }

    /// <summary>
    /// Inclusivity of the upper faces, one per axis; empty means every upper
    /// face is open.
    /// </summary>
    public ImmutableArray<Bound> MaxBound { get; }

    /// <summary>Opaque JSON metadata carried by the box, joined on merge.</summary>
    public ImmutableArray<byte> Meta { get; private init; }

    /// <summary>The number of dimensions.</summary>
    public int Dimensions => Min.Length;

    /// <summary>Whether the box covers no point at all.</summary>
    public bool IsEmpty
    {
        get
        {
            for (var axis = 0; axis < Dimensions; axis++)
            {
                if (Min[axis] > Max[axis])
                {
                    return true;
                }

                if (Min[axis] == Max[axis] && (LowestBound(axis) == Bound.Open || HighestBound(axis) == Bound.Open))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Whether the box is well-formed.</summary>
    public bool IsValid => Validate() == BoxError.None;

    /// <summary>Whether the lower face on <paramref name="axis"/> is included.</summary>
    public Bound LowestBound(int axis) =>
        axis < MinBound.Length ? MinBound[axis] : Bound.Closed;

    /// <summary>Whether the upper face on <paramref name="axis"/> is included.</summary>
    public Bound HighestBound(int axis) =>
        axis < MaxBound.Length ? MaxBound[axis] : Bound.Open;

    /// <summary>Returns a copy of the box carrying the given metadata.</summary>
    public Box WithMeta(ImmutableArray<byte> meta) => new(Min, Max, MinBound, MaxBound) { Meta = meta };

    /// <summary>Reports the first reason the box is not well-formed, if any.</summary>
    public BoxError Validate()
    {
        if (Min.Length != Max.Length)
        {
            return BoxError.Dimensions;
        }

        if (Min.Length == 0)
        {
            return BoxError.NoDimensions;
        }

        if (HasMismatchedBounds())
        {
            return BoxError.Bounds;
        }

        for (var axis = 0; axis < Dimensions; axis++)
        {
            var error = ValidateAxis(axis);
            if (error != BoxError.None)
            {
                return error;
            }
        }

        return BoxError.None;
    }

    /// <summary>Throws when the box is not well-formed.</summary>
    /// <exception cref="InvalidBoxException">The box failed validation.</exception>
    public void EnsureValid()
    {
        var error = Validate();
        if (error != BoxError.None)
        {
            throw new InvalidBoxException(this, error);
        }
    }

    /// <summary>Whether <paramref name="point"/> lies inside the box.</summary>
    /// <exception cref="ArgumentException">The point has the wrong number of coordinates.</exception>
    public bool Contains(IReadOnlyList<double> point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.Count != Dimensions)
        {
            throw new ArgumentException(
                FormattableString.Invariant($"A point in {Dimensions} dimensions needs {Dimensions} coordinates."),
                nameof(point));
        }

        for (var axis = 0; axis < Dimensions; axis++)
        {
            if (!HoldsOnAxis(axis, point[axis]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether every point of <paramref name="other"/> lies inside this box.
    /// Boxes of different dimensionality contain nothing of each other.
    /// </summary>
    public bool Contains(Box other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Dimensions != Dimensions)
        {
            return false;
        }

        for (var axis = 0; axis < Dimensions; axis++)
        {
            if (!CoversAxis(other, axis))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the boxes share at least one point.</summary>
    public bool Overlaps(Box other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Dimensions != Dimensions)
        {
            return false;
        }

        for (var axis = 0; axis < Dimensions; axis++)
        {
            if (SharedSpan(axis, other) is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Canonical cover order: lower corner, then upper corner, then the faces,
    /// axis by axis.
    /// </summary>
    public int CompareTo(Box? other)
    {
        if (other is null)
        {
            return 1;
        }

        for (var axis = 0; axis < Dimensions; axis++)
        {
            var order = CompareAxis(other, axis);
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }

    /// <summary>
    /// Whether the boxes cover the same region. Metadata is not part of their
    /// identity.
    /// </summary>
    public bool Equals(Box? other) =>
        other is not null && Dimensions == other.Dimensions && CompareTo(other) == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Box);

    private int CompareAxis(Box other, int axis)
    {
        if (Min[axis] != other.Min[axis])
        {
            return Min[axis].CompareTo(other.Min[axis]);
        }

        if (Max[axis] != other.Max[axis])
        {
            return Max[axis].CompareTo(other.Max[axis]);
        }

        if (LowestBound(axis) != other.LowestBound(axis))
        {
            return LowestBound(axis).CompareTo(other.LowestBound(axis));
        }

        return HighestBound(axis).CompareTo(other.HighestBound(axis));
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Dimensions);
        for (var axis = 0; axis < Dimensions; axis++)
        {
            hash.Add(Min[axis]);
            hash.Add(Max[axis]);
            hash.Add(LowestBound(axis));
            hash.Add(HighestBound(axis));
        }

        return hash.ToHashCode();
    }

    /// <summary>Renders the box per axis, e.g. <c>[0 10] x [2 5)</c>.</summary>
    public override string ToString()
    {
        var axes = new string[Dimensions];
        for (var axis = 0; axis < Dimensions; axis++)
        {
            var left = LowestBound(axis) == Bound.Open ? '(' : '[';
            var right = HighestBound(axis) == Bound.Open ? ')' : ']';
            axes[axis] = FormattableString.Invariant($"{left}{Min[axis]} {Max[axis]}{right}");
        }

        return string.Join(" x ", axes);
    }

    private static int Compare(Box? left, Box? right) =>
        left is null ? (right is null ? 0 : -1) : left.CompareTo(right);

    private bool HasMismatchedBounds() => (MinBound.Length != 0 && MinBound.Length != Dimensions)
        || (MaxBound.Length != 0 && MaxBound.Length != Dimensions);

    private BoxError ValidateAxis(int axis)
    {
        if (double.IsNaN(Min[axis]) || double.IsNaN(Max[axis]))
        {
            return BoxError.NotANumber;
        }

        if (double.IsInfinity(Min[axis]) || double.IsInfinity(Max[axis]))
        {
            return BoxError.NotFinite;
        }

        return Min[axis] > Max[axis] ? BoxError.Inverted : BoxError.None;
    }

    private bool HoldsOnAxis(int axis, double coordinate) =>
        coordinate >= Min[axis]
        && coordinate <= Max[axis]
        && !(coordinate == Min[axis] && LowestBound(axis) == Bound.Open)
        && !(coordinate == Max[axis] && HighestBound(axis) == Bound.Open);

    /// <summary>Whether this box covers <paramref name="other"/> on one axis.</summary>
    private bool CoversAxis(Box other, int axis) =>
        other.Min[axis] >= Min[axis]
        && other.Max[axis] <= Max[axis]
        && CoversLowerFace(other, axis)
        && CoversUpperFace(other, axis);

    /// <summary>
    /// Whether a shared lower corner is covered: the inner box may not include
    /// a face this box excludes.
    /// </summary>
    private bool CoversLowerFace(Box other, int axis) =>
        other.Min[axis] != Min[axis]
        || LowestBound(axis) == Bound.Closed
        || other.LowestBound(axis) == Bound.Open;

    /// <summary>
    /// Whether a shared upper corner is covered: the inner box may not include
    /// a face this box excludes.
    /// </summary>
    private bool CoversUpperFace(Box other, int axis) =>
        other.Max[axis] != Max[axis]
        || HighestBound(axis) == Bound.Closed
        || other.HighestBound(axis) == Bound.Open;

    /// <summary>The shared span on one axis, or <c>null</c> when it is empty.</summary>
    private double? SharedSpan(int axis, Box other)
    {
        var low = Math.Max(Min[axis], other.Min[axis]);
        var high = Math.Min(Max[axis], other.Max[axis]);
        if (low < high)
        {
            return low;
        }

        if (low > high)
        {
            return null;
        }

        return HoldsOnAxis(axis, low) && other.HoldsOnAxis(axis, low) ? low : null;
    }
}
