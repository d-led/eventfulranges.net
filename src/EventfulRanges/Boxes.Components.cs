namespace EventfulRanges;

/// <summary>
/// The connected components of a cover.
/// </summary>
public static partial class Boxes
{
    /// <summary>
    /// Groups the boxes into maximal sets whose union is connected: two boxes
    /// belong together when they overlap, or when they meet along a face of
    /// positive measure — touching on one axis and overlapping on every other.
    /// Boxes that meet only at a corner are separate.
    /// </summary>
    /// <remarks>
    /// The cover is normalized first, and both the components and the boxes
    /// inside each one come back in canonical order. Connectivity is a question
    /// about neighbourhoods, not about membership, so touching faces count
    /// whatever their inclusivity: the boxes on either side of an open face are
    /// still one region. Every box must have the same number of dimensions.
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<Box>> Components(IReadOnlyList<Box> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var cover = Normalize(boxes);
        var indices = new Indices(cover.Count);
        JoinConnected(cover, indices);
        return Group(cover, indices);
    }

    private static void JoinConnected(IReadOnlyList<Box> cover, Indices indices)
    {
        for (var first = 0; first < cover.Count; first++)
        {
            for (var second = first + 1; second < cover.Count; second++)
            {
                if (Connected(cover[first], cover[second]))
                {
                    indices.Join(first, second);
                }
            }
        }
    }

    /// <summary>
    /// The components in the order their first box appears, each holding its
    /// boxes in canonical order.
    /// </summary>
    private static List<IReadOnlyList<Box>> Group(IReadOnlyList<Box> cover, Indices indices)
    {
        var byRoot = new Dictionary<int, List<Box>>();
        var components = new List<IReadOnlyList<Box>>();
        for (var index = 0; index < cover.Count; index++)
        {
            var root = indices.Root(index);
            if (!byRoot.TryGetValue(root, out var component))
            {
                component = [];
                byRoot[root] = component;
                components.Add(component);
            }

            component.Add(cover[index]);
        }

        return components;
    }

    /// <summary>Whether the boxes belong to the same region.</summary>
    private static bool Connected(Box first, Box second) =>
        first.Dimensions == second.Dimensions
        && (first.Overlaps(second) || MeetAlongAFace(first, second));

    /// <summary>Whether the boxes touch on one axis and overlap on every other.</summary>
    private static bool MeetAlongAFace(Box first, Box second)
    {
        for (var axis = 0; axis < first.Dimensions; axis++)
        {
            if (MeetsOn(first, second, axis) && TouchesAlongAFace(first, second, axis))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MeetsOn(Box first, Box second, int axis) =>
        first.Max[axis] == second.Min[axis] || second.Max[axis] == first.Min[axis];

    /// <summary>
    /// Whether the boxes overlap with positive measure on every axis but the
    /// one they meet on, so the shared face is a face and not a corner.
    /// </summary>
    private static bool TouchesAlongAFace(Box first, Box second, int sharedAxis)
    {
        for (var axis = 0; axis < first.Dimensions; axis++)
        {
            if (axis == sharedAxis)
            {
                continue;
            }

            if (first.Max[axis] <= second.Min[axis] || second.Max[axis] <= first.Min[axis])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Union-find over box indices, with path compression.</summary>
    private sealed class Indices
    {
        private readonly int[] parents;

        public Indices(int count) => parents = [.. Enumerable.Range(0, count)];

        public int Root(int index)
        {
            if (parents[index] == index)
            {
                return index;
            }

            parents[index] = Root(parents[index]);
            return parents[index];
        }

        public void Join(int first, int second)
        {
            var root = Root(first);
            var otherRoot = Root(second);
            if (root != otherRoot)
            {
                parents[otherRoot] = root;
            }
        }
    }
}
