namespace EventfulRanges;

/// <summary>
/// A static, read-only R-tree over axis-aligned boxes: build it once from a cover,
/// query it many times, throw it away when the cover changes.
/// </summary>
/// <remarks>
/// Construction packs the boxes by median split — each internal node splits on the
/// axis with the widest span, at the median — so the tree is balanced, and every
/// node remembers the smallest box covering its children. A search descends only
/// into the children whose covering box overlaps the query, turning a scan of the
/// cover into a descent plus the matches.
/// <para>
/// A tree carries no identity and no history, so it is safe to share. Building is a
/// pure function of the boxes, so two replicas that converged on the same cover
/// build the same tree.
/// </para>
/// <para>
/// The Go original offers a second build that stores the caller's boxes instead of
/// copying them. There is no such choice here: <see cref="Box"/> is immutable, so
/// one build stores them directly and results can be handed back as they are.
/// </para>
/// </remarks>
public sealed class RTree
{
    private const int Capacity = 8;

    private readonly Node? root;

    private RTree(Node? root, int length, int dimensions)
    {
        this.root = root;
        Len = length;
        Dimensions = dimensions;
    }

    /// <summary>How many boxes the tree indexes.</summary>
    public int Len { get; }

    /// <summary>The dimensionality of the indexed boxes, or 0 when empty.</summary>
    public int Dimensions { get; }

    /// <summary>
    /// Bulk-loads a tree over <paramref name="boxes"/>. Boxes that hold no point are
    /// dropped; every survivor must be well-formed and of one shared dimensionality.
    /// </summary>
    /// <exception cref="InvalidBoxException">A box is not well-formed.</exception>
    /// <exception cref="ArgumentException">The boxes do not share one dimensionality.</exception>
    public static RTree Build(IReadOnlyList<Box> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var kept = new List<Box>(boxes.Count);
        var dimensions = 0;
        foreach (var box in boxes.Where(box => !box.IsEmpty))
        {
            box.EnsureValid();
            dimensions = dimensions == 0 ? box.Dimensions : dimensions;
            if (box.Dimensions != dimensions)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A tree indexes one dimensionality: got a {box.Dimensions}-dimensional box in a {dimensions}-dimensional tree."),
                    nameof(boxes));
            }

            kept.Add(box);
        }

        return kept.Count == 0
            ? new RTree(root: null, length: 0, dimensions: 0)
            : new RTree(Pack(kept), kept.Count, dimensions);
    }

    /// <summary>
    /// Every indexed box that shares a point with <paramref name="query"/>, in
    /// canonical order. An empty tree, an empty query, or a query of another
    /// dimensionality matches nothing.
    /// </summary>
    public IReadOnlyList<Box> Search(Box query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var matches = SearchInTraversalOrder(query).ToList();
        matches.Sort(static (left, right) => left.CompareTo(right));
        return matches;
    }

    /// <summary>
    /// The matching boxes in traversal order rather than canonical order: the fast
    /// path for callers that only read them. Deterministic for a given tree.
    /// </summary>
    public IReadOnlyList<Box> SearchInTraversalOrder(Box query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var matches = new List<Box>();
        if (root is not null && !query.IsEmpty && query.Dimensions == Dimensions)
        {
            Collect(root, query, matches);
        }

        return matches;
    }

    /// <summary>Packs boxes into a balanced tree, splitting at the widest axis.</summary>
    private static Node Pack(List<Box> boxes)
    {
        if (boxes.Count <= Capacity)
        {
            return Node.Leaf(boxes, CoverOf(boxes));
        }

        var axis = WidestAxis(boxes);
        boxes.Sort((left, right) => CompareOn(left, right, axis));
        var middle = boxes.Count / 2;
        var children = new[] { Pack(boxes[..middle]), Pack(boxes[middle..]) };
        return Node.Internal(children, CoverOf(children.Select(child => child.Cover).ToList()));
    }

    /// <summary>Descends only into the nodes whose cover box overlaps the query.</summary>
    private static void Collect(Node node, Box query, List<Box> matches)
    {
        if (!node.Cover.Overlaps(query))
        {
            return;
        }

        foreach (var box in node.Boxes.Where(box => box.Overlaps(query)))
        {
            matches.Add(box);
        }

        foreach (var child in node.Children)
        {
            Collect(child, query, matches);
        }
    }

    /// <summary>The axis along which the boxes span the most, so the split separates them.</summary>
    private static int WidestAxis(List<Box> boxes)
    {
        var widest = 0;
        var width = 0.0;
        for (var axis = 0; axis < boxes[0].Dimensions; axis++)
        {
            var span = boxes.Max(box => box.Max[axis]) - boxes.Min(box => box.Min[axis]);
            if (span > width)
            {
                widest = axis;
                width = span;
            }
        }

        return widest;
    }

    /// <summary>
    /// Orders boxes along one axis for the median split, falling back to canonical
    /// order so the packing is deterministic on ties.
    /// </summary>
    private static int CompareOn(Box left, Box right, int axis)
    {
        if (left.Min[axis] != right.Min[axis])
        {
            return left.Min[axis].CompareTo(right.Min[axis]);
        }

        if (left.Max[axis] != right.Max[axis])
        {
            return left.Max[axis].CompareTo(right.Max[axis]);
        }

        return left.CompareTo(right);
    }

    /// <summary>
    /// The smallest box covering the given boxes, closed on every face: a child whose
    /// face is closed must not escape its parent's cover, or a search could prune it.
    /// </summary>
    private static Box CoverOf(List<Box> boxes)
    {
        var dimensions = boxes[0].Dimensions;
        var min = new double[dimensions];
        var max = new double[dimensions];
        for (var axis = 0; axis < dimensions; axis++)
        {
            min[axis] = boxes.Min(box => box.Min[axis]);
            max[axis] = boxes.Max(box => box.Max[axis]);
        }

        var closedFaces = Enumerable.Repeat(Bound.Closed, dimensions).ToList();
        return new Box(min, max, closedFaces, closedFaces);
    }

    /// <summary>One node: a leaf holds boxes, an internal node holds children.</summary>
    private sealed class Node
    {
        private Node(Box cover, List<Box> boxes, Node[] children)
        {
            Cover = cover;
            Boxes = boxes;
            Children = children;
        }

        public Box Cover { get; }

        public List<Box> Boxes { get; }

        public Node[] Children { get; }

        public static Node Leaf(List<Box> boxes, Box cover) => new(cover, boxes, []);

        public static Node Internal(Node[] children, Box cover) => new(cover, [], children);
    }
}
