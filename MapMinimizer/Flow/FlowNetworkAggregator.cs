namespace MapMinimizer;

/// <summary>
/// Aggregates modules into objects for the two-level or multilevel phase.
/// </summary>
/// <remarks>
/// Only inter-module flow becomes edges. Intra-module flow is absorbed into the module summaries.
/// </remarks>
internal static class FlowNetworkAggregator
{
    /// <summary>
    /// Groups each module into one object while preserving its original object prior mass.
    /// </summary>
    internal static FlowNetwork AggregateModules(FlowNetwork activeNetwork, int[] moduleOfObject, int moduleCount)
        => Aggregate(activeNetwork, moduleOfObject, moduleCount, asSuperNetwork: false);

    /// <summary>
    /// Builds a super-network whose object priors are the entry rates of the lower-level modules.
    /// </summary>
    internal static FlowNetwork BuildSuperNetwork(FlowNetwork activeNetwork, int[] moduleOfObject, int moduleCount)
        => Aggregate(activeNetwork, moduleOfObject, moduleCount, asSuperNetwork: true);

    private static FlowNetwork Aggregate(FlowNetwork activeNetwork, int[] moduleOfObject, int moduleCount, bool asSuperNetwork)
    {
        ValidateArguments(activeNetwork, moduleOfObject, moduleCount);

        var workspace = new ModuleAggregationWorkspace(activeNetwork.NodeCount, moduleCount);
        try
        {
            AccumulateModuleFlow(activeNetwork, moduleOfObject, moduleCount, in workspace);
            NodeData[] moduleNodes = BuildModuleNodes(workspace.Summaries, workspace.ExternalFlowByModule, moduleCount, asSuperNetwork);
            return AggregatedNetworkBuilder.Build(moduleNodes, ref workspace);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    private static void ValidateArguments(FlowNetwork activeNetwork, int[] moduleOfObject, int moduleCount)
    {
        if (activeNetwork == null) throw new ArgumentNullException(nameof(activeNetwork));
        if (moduleOfObject == null) throw new ArgumentNullException(nameof(moduleOfObject));
        if (moduleOfObject.Length != activeNetwork.NodeCount)
            throw new ArgumentException("moduleOfObject length mismatch.", nameof(moduleOfObject));
        if (moduleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(moduleCount));
    }

    private static void AccumulateModuleFlow(FlowNetwork activeNetwork, int[] moduleOfObject, int moduleCount, in ModuleAggregationWorkspace workspace)
    {
        ModuleSummary[] summaries = workspace.Summaries;
        double[] externalFlowByModule = workspace.ExternalFlowByModule;
        Dictionary<long, double> edgeFlowByPair = workspace.EdgeFlowByPair;

        // Keep node totals interleaved with adjacency visits to preserve the order of every flow addition.
        for (int u = 0; u < activeNetwork.NodeCount; u++)
        {
            int mu = moduleOfObject[u];
            if ((uint)mu >= (uint)moduleCount)
                throw new ArgumentException("Module ids must be dense and within moduleCount.", nameof(moduleOfObject));
            summaries[mu].Flow += activeNetwork.Nodes[u].VisitRate;
            summaries[mu].Prior += activeNetwork.Nodes[u].Prior;
            double externalFlow = activeNetwork.Nodes[u].ExternalFlow;
            externalFlowByModule[mu] += externalFlow;
            summaries[mu].EnterFlow += externalFlow;
            summaries[mu].ExitFlow += externalFlow;

            ReadOnlySpan<EdgeData> edges = activeNetwork.GetEdges(u);
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                EdgeData edge = edges[edgeIndex];
                int mv = moduleOfObject[edge.Target];
                if (mu == mv || edge.Flow <= 0.0)
                    continue;
                // Pooled capacity can exceed moduleCount, so validate the logical index before writing.
                if ((uint)mv >= (uint)moduleCount)
                    throw new ArgumentException("Module ids must be dense and within moduleCount.", nameof(moduleOfObject));

                summaries[mu].ExitFlow += edge.Flow;
                summaries[mv].EnterFlow += edge.Flow;

                if (mu > mv)
                    continue;

                long key = MakeKey(mu, mv);
                edgeFlowByPair[key] = edgeFlowByPair.TryGetValue(key, out double old) ? old + edge.Flow : edge.Flow;
            }
        }
    }

    private static NodeData[] BuildModuleNodes(ModuleSummary[] summaries, double[] externalFlowByModule, int moduleCount, bool asSuperNetwork)
    {
        var moduleNodes = new NodeData[moduleCount];
        for (int m = 0; m < moduleCount; m++)
        {
            moduleNodes[m] = new NodeData
            {
                VisitRate = summaries[m].Flow,
                // Superlevels encode module entries; flat passes keep the original object mass.
                Prior = asSuperNetwork ? summaries[m].EnterFlow : summaries[m].Prior,
                ExternalFlow = externalFlowByModule[m],
                EnterFlow = summaries[m].EnterFlow,
                ExitFlow = summaries[m].ExitFlow,
            };
        }

        return moduleNodes;
    }

    /// <summary>
    /// Packs a module pair into one dictionary key without allocating tuples on every edge visit.
    /// </summary>
    private static long MakeKey(int src, int dst) => ((long)src << 32) | (uint)dst;
}
