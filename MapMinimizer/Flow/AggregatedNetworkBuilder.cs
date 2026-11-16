namespace MapMinimizer;

/// <summary>
/// Builds CSR edges directly from aggregated module-to-module flow.
/// </summary>
internal static class AggregatedNetworkBuilder
{
    internal static FlowNetwork Build(NodeData[] moduleNodes, ref ModuleAggregationWorkspace workspace)
    {
        int moduleCount = moduleNodes.Length;
        Dictionary<long, double> edgeFlowByPair = workspace.EdgeFlowByPair;
        int[] degree = workspace.RentDegree();
        CountDegrees(edgeFlowByPair, degree);

        int[] edgeOffsets = CsrLayout.BuildOffsets(degree, moduleCount);
        var edges = new EdgeData[edgeOffsets[moduleCount]];
        int[] cursor = workspace.RentCursor(edgeOffsets);
        WriteEdges(edgeFlowByPair, edges, cursor);

        return new FlowNetwork(moduleNodes, edges, edgeOffsets);
    }

    private static void CountDegrees(Dictionary<long, double> edgeFlowByPair, int[] degree)
    {
        foreach ((long key, double flow) in edgeFlowByPair)
        {
            if (flow <= 0.0)
                continue;

            int src = (int)(key >> 32);
            int dst = (int)(key & 0xFFFFFFFF);
            degree[src]++;
            if (src != dst)
                degree[dst]++;
        }
    }

    private static void WriteEdges(Dictionary<long, double> edgeFlowByPair, EdgeData[] edges, int[] cursor)
    {
        foreach ((long key, double flow) in edgeFlowByPair)
        {
            if (flow <= 0.0)
                continue;

            int src = (int)(key >> 32);
            int dst = (int)(key & 0xFFFFFFFF);
            edges[cursor[src]++] = new EdgeData
            {
                Target = dst,
                Flow = flow,
            };

            if (src != dst)
            {
                edges[cursor[dst]++] = new EdgeData
                {
                    Target = src,
                    Flow = flow,
                };
            }
        }
    }
}
