namespace MapMinimizer;

/// <summary>
/// Computes module flow summaries for network partitions.
/// </summary>
internal static class ModuleFlowCalculator
{
    /// <summary>
    /// Computes flow summaries for a dense partition of this network.
    /// </summary>
    internal static ModuleSummary[] Summarize(FlowNetwork network, int[] moduleOfObject)
    {
        if (moduleOfObject == null) throw new ArgumentNullException(nameof(moduleOfObject));
        if (moduleOfObject.Length != network.NodeCount) throw new ArgumentException("moduleOfObject length mismatch.", nameof(moduleOfObject));

        int maxModule = -1;
        for (int nodeId = 0; nodeId < moduleOfObject.Length; nodeId++)
        {
            if (moduleOfObject[nodeId] < 0) throw new ArgumentException("Module ids must be non-negative.", nameof(moduleOfObject));
            maxModule = Math.Max(maxModule, moduleOfObject[nodeId]);
        }

        var summaries = new ModuleSummary[maxModule + 1];
        for (int nodeId = 0; nodeId < network.NodeCount; nodeId++)
        {
            int module = moduleOfObject[nodeId];
            summaries[module].Flow += network.Nodes[nodeId].VisitRate;
            summaries[module].Prior += network.Nodes[nodeId].Prior;
            summaries[module].EnterFlow += network.Nodes[nodeId].ExternalFlow;
            summaries[module].ExitFlow += network.Nodes[nodeId].ExternalFlow;
        }

        for (int nodeId = 0; nodeId < network.NodeCount; nodeId++)
        {
            int sourceModule = moduleOfObject[nodeId];
            ReadOnlySpan<EdgeData> nodeEdges = network.GetEdges(nodeId);
            for (int edgeIndex = 0; edgeIndex < nodeEdges.Length; edgeIndex++)
            {
                EdgeData edge = nodeEdges[edgeIndex];
                int targetModule = moduleOfObject[edge.Target];
                if (sourceModule == targetModule)
                    continue;

                summaries[sourceModule].ExitFlow += edge.Flow;
                summaries[targetModule].EnterFlow += edge.Flow;
            }
        }

        return summaries;
    }
}
