namespace MapMinimizer;

/// <summary>Evaluates a flat partition or the unsplit codebook on the same flow scale.</summary>
internal static class PartitionScorer
{
    internal static PartitionResult Evaluate(FlowNetwork network, int[] partition)
    {
        int[] labels = Labeling.Compact((int[])partition.Clone(), out int count);
        using var partitionState = new PartitionState(network);
        partitionState.Initialize(labels);
        return new(labels, count, partitionState.Codelength);
    }

    internal static double GetOneModuleCodelength(FlowNetwork network)
    {
        double prior = 0.0, entropy = 0.0;
        foreach (NodeData node in network.Nodes)
        {
            prior += node.Prior;
            entropy += Plogp(node.Prior);
        }
        return Plogp(prior + network.ExitFlow) - Plogp(network.ExitFlow) - entropy;
    }

    /// <summary>Cost of naming the current modules, including exits from an enclosing scope.</summary>
    internal static double GetIndexCodebookCodelength(FlowNetwork moduleNetwork)
    {
        double totalEnterRate = moduleNetwork.ExitFlow;
        double enterEntropy = 0.0;
        foreach (NodeData node in moduleNetwork.Nodes)
        {
            totalEnterRate += node.Prior;
            enterEntropy += Plogp(node.Prior);
        }

        return Plogp(totalEnterRate) - Plogp(moduleNetwork.ExitFlow) - enterEntropy;
    }

    private static double Plogp(double x) => x <= 0.0 ? 0.0 : x * Math.Log2(x);
}

