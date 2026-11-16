namespace MapMinimizer;

/// <summary>Releases submodules so they can move across the incumbent module boundaries.</summary>
internal sealed class CoarseTuner
{
    private readonly NodeAggregator aggregator;

    internal CoarseTuner(NodeAggregator aggregator) => this.aggregator = aggregator;

    internal PartitionResult Tune(FlowNetwork network, PartitionResult incumbent, Random random, InducedNetworkWorkspace pool)
    {
        var submodules = FindSubmodules(network, incumbent, random, pool);
        if (submodules.ModuleCount == incumbent.ModuleCount) return incumbent;

        var coarseNetwork = FlowNetworkAggregator.AggregateModules(network, submodules.Assignments, submodules.ModuleCount);
        var tuned = aggregator.FindPartition(coarseNetwork, random, submodules.ParentModules);
        var assignments = new int[network.NodeCount];
        for (int i = 0; i < assignments.Length; i++)
            assignments[i] = tuned.Assignments[submodules.Assignments[i]];
        return PartitionScorer.Evaluate(network, assignments);
    }

    private SubmodulePartition FindSubmodules(FlowNetwork network, PartitionResult incumbent, Random random, InducedNetworkWorkspace pool)
    {
        var members = new ModuleMembers(incumbent.Assignments, incumbent.ModuleCount);
        var assignments = new int[network.NodeCount];
        var parents = new List<int>();
        for (int module = 0; module < incumbent.ModuleCount; module++)
        {
            int start = members.GetStart(module), count = members.GetCount(module);
            if (count == 1)
            {
                // Preserve seed consumption when no local moving search is needed.
                random.Next();
                assignments[members.ObjectIds[start]] = parents.Count;
                parents.Add(module);
                continue;
            }

            var induced = InducedNetworkBuilder.Build(network, members.ObjectIds, start, count, pool);
            var local = aggregator.FindPartition(induced, random);
            int offset = parents.Count;
            for (int s = 0; s < local.ModuleCount; s++) parents.Add(module);
            for (int i = 0; i < count; i++)
                assignments[members.ObjectIds[start + i]] = offset + local.Assignments[i];
        }
        return new SubmodulePartition(assignments, parents.ToArray());
    }
}
