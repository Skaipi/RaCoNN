namespace MapMinimizer;

/// <summary>
/// Searches for supermodules which compress the current top-level index.
/// </summary>
internal sealed class SupermoduleSearch
{
    private readonly MapEquationParameters parameters;
    private readonly PartitionOptimizer partitionOptimizer;

    internal SupermoduleSearch(MapEquationParameters parameters)
    {
        this.parameters = parameters;
        partitionOptimizer = new PartitionOptimizer(parameters);
    }

    /// <summary>
    /// Finds a two-level partition, then adds improving supermodule partitions above it.
    /// </summary>
    internal ModuleHierarchy FindHierarchy(FlowNetwork baseNetwork, int maxLevels, Random random)
    {
        int nodeCount = baseNetwork.NodeCount;
        var assignmentsBottomUp = new List<int[]>();
        var activeNetwork = baseNetwork;
        int[] originalToCurrentObject = Identity(nodeCount);

        for (int level = 0; level < maxLevels; level++)
        {
            var optimization = partitionOptimizer.Optimize(activeNetwork, random);

            int[] partition = optimization.Assignments;
            int moduleCount = optimization.ModuleCount;
            if (moduleCount == activeNetwork.NodeCount) break;

            if (level > 0)
            {
                bool improvesIndex = optimization.Codelength + parameters.DeltaTolerance < PartitionScorer.GetIndexCodebookCodelength(activeNetwork);
                // A superlevel replaces the current index codebook. Improving on singleton supermodules alone does not guarantee better compression.
                if (moduleCount <= 1 || !improvesIndex) break;
            }

            var projected = new int[nodeCount];
            for (int nodeId = 0; nodeId < nodeCount; nodeId++)
                projected[nodeId] = partition[originalToCurrentObject[nodeId]];
            assignmentsBottomUp.Add(projected);

            if (moduleCount <= 1) break;

            activeNetwork = FlowNetworkAggregator.BuildSuperNetwork(activeNetwork, partition, moduleCount);
            for (int nodeId = 0; nodeId < nodeCount; nodeId++)
                originalToCurrentObject[nodeId] = partition[originalToCurrentObject[nodeId]];
        }

        // Trivial partition for singleton optimization result
        if (assignmentsBottomUp.Count == 0) assignmentsBottomUp.Add(Identity(nodeCount));

        var assignmentsTopDown = new int[assignmentsBottomUp.Count][];
        for (int level = 0; level < assignmentsTopDown.Length; level++)
            assignmentsTopDown[level] = assignmentsBottomUp[assignmentsBottomUp.Count - level - 1];

        return HierarchyTreeBuilder.Build(baseNetwork, assignmentsTopDown);
    }



    /// <summary>
    /// Labeling with consecutive numbers [0, 1, 2, ...]
    /// </summary>
    private static int[] Identity(int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = i;
        return result;
    }
}
