namespace MapMinimizer;

/// <summary>
/// Read-only CSR state for a flow network.
/// </summary>
internal sealed class FlowNetwork
{
    // Contiguous buffer to keep access fast
    private readonly EdgeData[] edges;
    private readonly int[] edgeOffsets;

    internal NodeData[] Nodes { get; }
    internal int NodeCount => Nodes.Length;
    internal double ExitFlow { get; }
    // True only for uncontracted, loop-free random-walk nodes. It enables an
    // exact interior-node pruning rule; arbitrary/precomputed flows do not qualify.
    internal bool HasAtomicNodeFlow { get; }

    /// <summary>
    /// Creates a flow network from flattened adjacency lists.
    /// </summary>
    internal FlowNetwork(NodeData[] nodes, EdgeData[] edges, int[] edgeOffsets, bool hasAtomicNodeFlow = false)
    {
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        this.edges = edges ?? throw new ArgumentNullException(nameof(edges));
        this.edgeOffsets = edgeOffsets ?? throw new ArgumentNullException(nameof(edgeOffsets));
        HasAtomicNodeFlow = hasAtomicNodeFlow;
        ValidateAdjacency(nodes.Length, this.edges, this.edgeOffsets, nameof(edges));

        for (int nodeId = 0; nodeId < nodes.Length; nodeId++)
            ExitFlow += nodes[nodeId].ExternalFlow;
    }

    /// <summary>
    /// Returns adjacency list of the node.
    /// </summary>
    internal ReadOnlySpan<EdgeData> GetEdges(int nodeId)
    {
        ValidateNodeId(nodeId);
        int start = edgeOffsets[nodeId];
        return edges.AsSpan(start, edgeOffsets[nodeId + 1] - start);
    }

    /// <summary>
    /// Validates adjacency buffer.
    /// </summary>
    private static void ValidateAdjacency(int nodeCount, EdgeData[] edges, int[] offsets, string paramName)
    {
        if (offsets.Length != nodeCount + 1)
            throw new ArgumentException("Offsets must have length nodeCount + 1.", paramName);
        if (offsets[0] != 0)
            throw new ArgumentException("Offsets must start at zero.", paramName);
        if (offsets[nodeCount] != edges.Length)
            throw new ArgumentException("Offsets must terminate at the edge-buffer length.", paramName);

        for (int nodeId = 0; nodeId < nodeCount; nodeId++)
        {
            int start = offsets[nodeId];
            int end = offsets[nodeId + 1];
            if (start > end)
                throw new ArgumentException("Offsets must be non-decreasing.", paramName);

            for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                int target = edges[edgeIndex].Target;
                if ((uint)target >= (uint)nodeCount)
                    throw new ArgumentOutOfRangeException(paramName, $"Edge target {target} is out of range.");
            }
        }
    }

    /// <summary>
    /// Validates span accessors against invalid node ids.
    /// </summary>
    private void ValidateNodeId(int nodeId)
    {
        if ((uint)nodeId >= (uint)Nodes.Length)
            throw new ArgumentOutOfRangeException(nameof(nodeId));
    }
}
