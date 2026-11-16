namespace MapMinimizer;

/// <summary>
/// Node in the final hierarchy tree.
/// </summary>
/// <remarks>
/// Internal nodes represent modules and object nodes represent original graph nodes. Every subtree points at contiguous
/// slices of a shared leaf-order array, avoiding a separate descendant list at every internal level.
/// </remarks>
internal class HierarchyNode
{
    private int[] leafOrder = Array.Empty<int>();

    /// <summary>
    /// Indicates that tree node is equivalent to graph node
    /// </summary>
    internal bool IsObject { get; set; }
    internal double Flow { get; set; }
    internal double EnterFlow { get; set; }
    internal double ExitFlow { get; set; }
    internal List<HierarchyNode> Children { get; } = new();
    internal int LeafStart { get; private set; }
    internal int LeafCount { get; private set; }

    /// <summary>
    /// Indicates whether node has children or not.
    /// </summary>
    internal bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// Returns the original leaf ids that currently belong to this module.
    /// </summary>
    internal ReadOnlySpan<int> GetLeafSpan() => leafOrder.AsSpan(LeafStart, LeafCount);

    /// <summary>
    /// Returns the single original leaf id represented by a leaf node.
    /// </summary>
    internal int GetSingleLeafId() => leafOrder[LeafStart];

    /// <summary>
    /// Exposes this subtree's leaf-order array for induced-subnetwork construction.
    /// </summary>
    internal int[] LeafOrder => leafOrder;

    /// <summary>
    /// Assigns a leaf-order array and the subrange occupied by this node.
    /// </summary>
    internal void SetLeafRange(int[] sharedLeafOrder, int leafStart, int leafCount)
    {
        if (sharedLeafOrder == null) throw new ArgumentNullException(nameof(sharedLeafOrder));
        if (leafStart < 0 || leafCount < 0 || leafStart + leafCount > sharedLeafOrder.Length)
            throw new ArgumentOutOfRangeException(nameof(leafStart));

        leafOrder = sharedLeafOrder;
        LeafStart = leafStart;
        LeafCount = leafCount;
    }
}
