namespace MapMinimizer;

/// <summary>
/// Improves discovered modules by optimizing their induced subnetworks.
/// Builds an induced subnetwork containing only internal edges with local indexes.
/// </summary>
internal sealed class SubmoduleSearch
{
    private readonly MapEquationParameters parameters;
    private readonly SupermoduleSearch supermoduleSearch;

    internal SubmoduleSearch(MapEquationParameters parameters, SupermoduleSearch supermoduleSearch)
    {
        this.parameters = parameters;
        this.supermoduleSearch = supermoduleSearch;
    }

    internal void RefineHierarchy(FlowNetwork baseNetwork, ModuleHierarchy hierarchy, int maxDepth, Random random)
    {
        var nodePool = new InducedNetworkWorkspace(baseNetwork.NodeCount);
        RefineRecursively(baseNetwork, hierarchy.Root, maxDepth, random, nodePool);
        HierarchyTreeBuilder.RemoveRedundantRootModules(hierarchy.Root);
    }

    private void RefineRecursively(FlowNetwork baseNetwork, HierarchyNode root, int maxDepth, Random random, InducedNetworkWorkspace nodePool)
    {
        if (maxDepth <= 0 || root.IsLeaf) return;

        foreach (HierarchyNode child in root.Children)
        {
            if (child.IsObject || child.LeafCount <= 1) continue;

            var candidate = FindSubmodules(baseNetwork, child, random, nodePool);
            ReplaceIfImproved(child, candidate);
            RefineRecursively(baseNetwork, child, maxDepth - 1, random, nodePool);
        }
    }

    private HierarchyNode FindSubmodules(FlowNetwork network, HierarchyNode module, Random random, InducedNetworkWorkspace workspace)
    {
        var induced = InducedNetworkBuilder.Build(network, module.LeafOrder, module.LeafStart, module.LeafCount, workspace);
        var candidate = supermoduleSearch.FindHierarchy(induced, parameters.MaxLevels, random);
        RemapLocalLeavesToGlobal(candidate.Root, module.GetLeafSpan());
        // The induced network preserves global flow, so its summaries need no rescaling.
        return RemoveEnclosingModule(candidate.Root);
    }

    private void ReplaceIfImproved(HierarchyNode module, HierarchyNode candidate)
    {
        double currentScore = HierarchicalScorer.EvaluateSubtreeCodelength(module);
        double candidateScore = HierarchicalScorer.EvaluateSubtreeCodelength(candidate);
        if (candidateScore + parameters.DeltaTolerance >= currentScore) return;

        module.Children.Clear();
        module.Children.AddRange(candidate.Children);
    }

    /// <summary>
    /// Skip redundant wrappers in hierarchy tree
    /// </summary>
    private static HierarchyNode RemoveEnclosingModule(HierarchyNode root)
    {
        while (root.Children.Count == 1 && !root.Children[0].IsObject && root.Children[0].LeafCount == root.LeafCount)
        {
            root = root.Children[0];
        }

        return root;
    }

    /// <summary>
    /// Move ids of singleton leaves from range [0, ..., m] assigned by local refinement method
    /// to ids from flow network, where m is span of leaves in current local scope.
    /// </summary>
    private static void RemapLocalLeavesToGlobal(HierarchyNode node, ReadOnlySpan<int> globalLeafIds)
    {
        if (node.IsObject)
        {
            int localId = node.GetSingleLeafId();
            int globalId = globalLeafIds[localId];
            node.LeafOrder[node.LeafStart] = globalId;
            return;
        }

        foreach (HierarchyNode child in node.Children) RemapLocalLeavesToGlobal(child, globalLeafIds);
    }

}
