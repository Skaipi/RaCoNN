namespace MapMinimizer;

/// <summary>Solves scopes with at most two objects by comparing their possible partitions.</summary>
internal static class SmallPartitionSolver
{
    internal static PartitionResult FindPartition(FlowNetwork network)
        => OptimizePair(network, allowUnsplitCodebook: true);

    internal static PartitionResult FindPartitionWithIndex(FlowNetwork network)
        => OptimizePair(network, allowUnsplitCodebook: false);

    private static PartitionResult OptimizePair(FlowNetwork network, bool allowUnsplitCodebook)
    {
        if (network.NodeCount == 0) return new([], 0, 0);
        double unsplit = PartitionScorer.GetOneModuleCodelength(network);
        if (!allowUnsplitCodebook) unsplit += 2.0 * network.ExitFlow;
        if (network.NodeCount == 1) return new([0], 1, unsplit);

        double indexUse = network.ExitFlow, indexEntropy = 0.0, modules = 0.0;
        for (int i = 0; i < 2; i++)
        {
            double boundary = network.Nodes[i].ExternalFlow;
            foreach (var edge in network.GetEdges(i))
                if (edge.Target != i) boundary += edge.Flow;
            double p = network.Nodes[i].Prior;
            indexUse += boundary;
            indexEntropy += Plogp(boundary);
            modules += Plogp(p + boundary) - Plogp(boundary) - Plogp(p);
        }
        double split = Plogp(indexUse) - Plogp(network.ExitFlow) - indexEntropy + modules;
        return unsplit < split
            ? new([0, 0], 1, unsplit, split)
            : new([0, 1], 2, split, split);
    }

    private static double Plogp(double x) => x <= 0.0 ? 0.0 : x * Math.Log2(x);
}

