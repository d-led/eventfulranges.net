namespace EventfulRanges;

/// <summary>
/// One entry of a layered front: a region painted in order, later layers over
/// earlier ones. <see cref="BoxOpKind.Add"/> paints, and
/// <see cref="BoxOpKind.Remove"/> erases by painting the background over whatever
/// lies beneath.
/// </summary>
/// <param name="Box">The region this layer covers.</param>
/// <param name="Kind">Whether the layer paints or erases.</param>
public readonly record struct Layer(Box Box, BoxOpKind Kind);
