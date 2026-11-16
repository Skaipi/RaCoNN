namespace MapMinimizer;

/// <summary>
/// Builds induced networks while preserving node masses and boundary flow.
/// </summary>
internal static class InducedNetworkBuilder
{
    /// <summary>
    /// Extracts an induced network, preserving original masses and flow across its boundary.
    /// </summary>
    internal static FlowNetwork Build(FlowNetwork network, int[] objectIds, int objectIdOffset, int objectCount, InducedNetworkWorkspace nodePool)
    {
        if (objectIds == null) throw new ArgumentNullException(nameof(objectIds));
        if (nodePool == null) throw new ArgumentNullException(nameof(nodePool));
        if (objectIdOffset < 0 || objectCount < 0 || objectIdOffset > objectIds.Length - objectCount)
            throw new ArgumentOutOfRangeException(nameof(objectIdOffset));
        if (objectCount == 0) return new FlowNetwork([], [], new int[1]);

        int membershipToken = IndexMembership(
            network, objectIds, objectIdOffset, objectCount, nodePool, out int[] nodeStamp, out int[] localIndexByNode);

        var nodes = new NodeData[objectCount];
        int[] outDegree = nodePool.RentOutDegreeBuffer(objectCount);
        CountEdgesAndBoundaryFlow(
            network, objectIds, objectIdOffset, objectCount, membershipToken, nodeStamp, nodes, outDegree);

        int[] edgeOffsets = CsrLayout.BuildOffsets(outDegree, objectCount);
        var edges = new EdgeData[edgeOffsets[objectCount]];
        CopyLocalEdges(
            network, objectIds, objectIdOffset, objectCount, membershipToken, nodeStamp, localIndexByNode, edges, edgeOffsets);

        FlowNetworkBuilder.ComputeNodeEnterExit(nodes, edges, edgeOffsets);

        return new FlowNetwork(nodes, edges, edgeOffsets, network.HasAtomicNodeFlow);
    }

    private static int IndexMembership(
        FlowNetwork network, int[] objectIds, int objectIdOffset, int objectCount, InducedNetworkWorkspace nodePool,
        out int[] nodeStamp, out int[] localIndexByNode)
    {
        int membershipToken = nodePool.NextNodeStamp(network.NodeCount);
        nodeStamp = nodePool.NodeStamp;
        localIndexByNode = nodePool.LocalIndexByNode;
        for (int localId = 0; localId < objectCount; localId++)
        {
            int globalId = objectIds[objectIdOffset + localId];
            if ((uint)globalId >= (uint)network.NodeCount)
                throw new ArgumentOutOfRangeException(nameof(objectIds));
            if (nodeStamp[globalId] == membershipToken)
                throw new ArgumentException("Induced-subnetwork object ids must be unique.", nameof(objectIds));

            nodeStamp[globalId] = membershipToken;
            localIndexByNode[globalId] = localId;
        }

        return membershipToken;
    }

    private static void CountEdgesAndBoundaryFlow(
        FlowNetwork network, int[] objectIds, int objectIdOffset, int objectCount,
        int membershipToken, int[] nodeStamp, NodeData[] nodes, int[] outDegree)
    {
        // Count edges within subnetwork
        for (int localId = 0; localId < objectCount; localId++)
        {
            int globalId = objectIds[objectIdOffset + localId];
            nodes[localId] = network.Nodes[globalId];
            ReadOnlySpan<EdgeData> nodeEdges = network.GetEdges(globalId);
            for (int edgeIndex = 0; edgeIndex < nodeEdges.Length; edgeIndex++)
            {
                if (nodeStamp[nodeEdges[edgeIndex].Target] == membershipToken)
                    outDegree[localId]++;
                else
                    nodes[localId].ExternalFlow += nodeEdges[edgeIndex].Flow;
            }
        }
    }

    private static void CopyLocalEdges(
        FlowNetwork network, int[] objectIds, int objectIdOffset, int objectCount,
        int membershipToken, int[] nodeStamp, int[] localIndexByNode, EdgeData[] edges, int[] edgeOffsets)
    {
        int[] cursor = CsrLayout.CreateCursor(edgeOffsets);

        // Create subnetwork edges
        for (int localId = 0; localId < objectCount; localId++)
        {
            int globalId = objectIds[objectIdOffset + localId];
            ReadOnlySpan<EdgeData> nodeEdges = network.GetEdges(globalId);
            for (int edgeIndex = 0; edgeIndex < nodeEdges.Length; edgeIndex++)
            {
                EdgeData edge = nodeEdges[edgeIndex];
                if (nodeStamp[edge.Target] != membershipToken) continue;

                edges[cursor[localId]++] = new EdgeData
                {
                    Target = localIndexByNode[edge.Target],
                    Flow = edge.Flow,
                };
            }
        }
    }
}
