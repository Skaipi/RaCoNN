namespace MapMinimizer;

/// <summary>
/// Finds a two-level partition with repeated node aggregation and alternating fine/coarse tuning.
/// </summary>
internal sealed class PartitionOptimizer
{
    private readonly MapEquationParameters parameters;
    private readonly NodeAggregator aggregator;
    private readonly CoarseTuner coarseTuner;

    internal PartitionOptimizer(MapEquationParameters parameters)
    {
        this.parameters = parameters;
        aggregator = new NodeAggregator(parameters);
        coarseTuner = new CoarseTuner(aggregator);
    }

    internal PartitionResult Optimize(FlowNetwork network, Random random, int[]? initialPartition = null)
    {
        if (initialPartition != null && initialPartition.Length != network.NodeCount)
            throw new ArgumentException("Partition must have one label per object.", nameof(initialPartition));
        if (network.NodeCount == 0) return new([], 0, 0.0);
        if (network.NodeCount <= 2) return SmallPartitionSolver.FindPartition(network);

        var partition = aggregator.FindPartition(network, random, initialPartition);
        var tuned = TunePartition(network, partition, random);
        double unsplit = PartitionScorer.GetOneModuleCodelength(network);
        return unsplit + parameters.DeltaTolerance < tuned.Codelength
            ? new(new int[network.NodeCount], 1, unsplit)
            : tuned;
    }

    private PartitionResult TunePartition(FlowNetwork network, PartitionResult best, Random random)
    {
        InducedNetworkWorkspace? pool = null;
        bool improvedInCycle = false;
        double cycleStart = best.Codelength;
        for (int iteration = 0; iteration < parameters.TuneIterationLimit && best.ModuleCount > 1; iteration++)
        {
            var candidate = iteration % 2 == 0
                ? aggregator.FindPartition(network, random, best.Assignments)
                : coarseTuner.Tune(network, best, random, pool ??= new InducedNetworkWorkspace(network.NodeCount));
            if (candidate.Codelength + parameters.DeltaTolerance < best.Codelength)
            {
                best = candidate;
                improvedInCycle = true;
            }

            // A complete cycle always tries coarse tuning, even when fine tuning failed.
            if (iteration % 2 == 1)
            {
                if (!improvedInCycle || cycleStart - best.Codelength <=
                    Math.Max(parameters.DeltaTolerance, Math.Abs(cycleStart) * parameters.TuneRelativeTolerance)) break;
                improvedInCycle = false;
                cycleStart = best.Codelength;
            }
        }
        return best;
    }
}
