namespace MapMinimizer;

/// <summary>
/// Infomap's core search: move nodes, aggregate the resulting modules, and repeat.
/// Aggregation replaces the current partition without adding an index level.
/// </summary>
internal sealed class NodeAggregator
{
    private readonly MapEquationParameters parameters;

    internal NodeAggregator(MapEquationParameters parameters) => this.parameters = parameters;

    internal PartitionResult FindPartition(FlowNetwork network, Random random, int[]? initialPartition = null)
    {
        if (network.NodeCount <= 2) return SmallPartitionSolver.FindPartitionWithIndex(network);
        PartitionResult best;
        using (var mover = new NodeMoveOptimizer(network, random.Next()))
            best = initialPartition == null
                ? mover.OptimizeFromSingletons(parameters.MaxPasses, parameters.DeltaTolerance)
                : mover.Optimize(initialPartition, parameters.MaxPasses, parameters.DeltaTolerance);
        var active = network;
        int[] projection = best.Assignments;
        int[] partition = best.Assignments;
        int moduleCount = best.ModuleCount;
        bool aggregated = false;

        while (moduleCount > 1 && moduleCount < active.NodeCount)
        {
            active = FlowNetworkAggregator.AggregateModules(active, partition, moduleCount);
            using var coarseMover = new NodeMoveOptimizer(active, random.Next());
            var merged = coarseMover.OptimizeFromSingletons(
                parameters.MaxPasses, parameters.DeltaTolerance);
            if (merged.ModuleCount == active.NodeCount) break;
            double delta = merged.Codelength - merged.InitialCodelength;
            if (delta >= -parameters.DeltaTolerance) break;

            int[] candidateLabels = new int[network.NodeCount];
            for (int i = 0; i < candidateLabels.Length; i++)
                candidateLabels[i] = merged.Assignments[projection[i]];
            // Object entropy changes by a constant on contraction, so the
            // coarse improvement is exact without rescanning the original graph.
            best = new(candidateLabels, merged.ModuleCount, best.Codelength + delta);
            aggregated = true;
            projection = candidateLabels;
            partition = merged.Assignments;
            moduleCount = merged.ModuleCount;
        }
        // Rebuild the original objective once, rather than once per contraction.
        return aggregated ? PartitionScorer.Evaluate(network, best.Assignments) : best;
    }
}

