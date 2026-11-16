namespace MapMinimizer;

/// <summary>Connects nested partition assignments into a flow-annotated module tree.</summary>
internal static class HierarchyTreeBuilder
{
    internal static ModuleHierarchy Build(FlowNetwork baseNetwork, int[][] topDownAssignments)
    {
        if (baseNetwork == null) throw new ArgumentNullException(nameof(baseNetwork));
        if (topDownAssignments == null) throw new ArgumentNullException(nameof(topDownAssignments));

        int nodeCount = baseNetwork.NodeCount;
        double rootFlow = 0.0;
        for (int nodeId = 0; nodeId < nodeCount; nodeId++)
            rootFlow += baseNetwork.Nodes[nodeId].VisitRate;

        var root = new HierarchyNode
        {
            Flow = rootFlow,
            EnterFlow = baseNetwork.ExitFlow,
            ExitFlow = baseNetwork.ExitFlow,
        };
        int[] leafOrder = Identity(nodeCount);
        root.SetLeafRange(leafOrder, 0, nodeCount);

        var summariesByLevel = new ModuleSummary[topDownAssignments.Length][];
        int maxLabelCount = 1;
        for (int level = 0; level < topDownAssignments.Length; level++)
        {
            summariesByLevel[level] = ModuleFlowCalculator.Summarize(baseNetwork, topDownAssignments[level]);
            maxLabelCount = Math.Max(maxLabelCount, summariesByLevel[level].Length);
        }

        var workspace = new HierarchyBuildWorkspace(maxLabelCount, nodeCount);
        BuildChildren(root, baseNetwork, topDownAssignments, summariesByLevel, 0, workspace);
        var hierarchy = new ModuleHierarchy(root);
        RemoveRedundantRootModules(hierarchy.Root);
        return hierarchy;
    }

    /// <summary>Removes enclosing codebooks which duplicate the scope's root.</summary>
    internal static void RemoveRedundantRootModules(HierarchyNode root)
    {
        while (root.Children.Count == 1)
        {
            HierarchyNode enclosing = root.Children[0];
            if (enclosing.IsObject || enclosing.Children.Count == 0 ||
                (enclosing.Children[0].IsObject && root.ExitFlow == 0.0))
                break;

            // The enclosing codebook duplicates the scope's own index. For an
            // open scope it also charges an unnecessary entry/exit symbol pair.
            root.Children.Clear();
            root.Children.AddRange(enclosing.Children);
        }
    }

private static void BuildChildren(
        HierarchyNode parent,
        FlowNetwork baseNetwork,
        int[][] topDownAssignments,
        ModuleSummary[][] summariesByLevel,
        int depth,
        HierarchyBuildWorkspace workspace)
    {
        if (depth >= topDownAssignments.Length)
        {
            AddObjectLeaves(parent, baseNetwork);
            return;
        }

        int[] labels = topDownAssignments[depth];
        int distinctLabelCount = CountModuleMembers(parent, labels, summariesByLevel[depth].Length, workspace);
        if (distinctLabelCount == 1 && depth > 0)
        {
            BuildChildren(parent, baseNetwork, topDownAssignments, summariesByLevel, depth + 1, workspace);
            return;
        }

        ReorderLeavesByModule(parent, labels, distinctLabelCount, workspace);
        int childStartIndex = parent.Children.Count;
        AddModuleChildren(parent, summariesByLevel[depth], distinctLabelCount, workspace);
        for (int i = 0; i < distinctLabelCount; i++)
            BuildChildren(parent.Children[childStartIndex + i], baseNetwork, topDownAssignments, summariesByLevel, depth + 1, workspace);
    }

    private static void AddObjectLeaves(HierarchyNode parent, FlowNetwork baseNetwork)
    {
        parent.Children.EnsureCapacity(parent.LeafCount);
        ReadOnlySpan<int> leafSpan = parent.GetLeafSpan();
        for (int i = 0; i < leafSpan.Length; i++)
        {
            int leafId = leafSpan[i];
            var leaf = new HierarchyNode
            {
                IsObject = true,
                Flow = baseNetwork.Nodes[leafId].VisitRate,
                EnterFlow = baseNetwork.Nodes[leafId].EnterFlow,
                ExitFlow = baseNetwork.Nodes[leafId].ExitFlow,
            };
            leaf.SetLeafRange(parent.LeafOrder, parent.LeafStart + i, 1);
            parent.Children.Add(leaf);
        }
    }

    private static int CountModuleMembers(HierarchyNode parent, int[] labelsAtDepth, int labelCapacity, HierarchyBuildWorkspace workspace)
    {
        int stamp = workspace.NextLabelStamp(labelCapacity);
        int[] labelStamp = workspace.LabelStamp;
        int[] labelCounts = workspace.LabelCounts;
        int[] encounteredLabels = workspace.EncounteredLabels;
        int distinctLabelCount = 0;
        ReadOnlySpan<int> parentLeafSpan = parent.GetLeafSpan();

        // Mark each unseen label with a stamp and count labels.
        for (int i = 0; i < parentLeafSpan.Length; i++)
        {
            int leafId = parentLeafSpan[i];
            int label = labelsAtDepth[leafId];
            if (labelStamp[label] != stamp)
            {
                labelStamp[label] = stamp;
                labelCounts[label] = 0;
                encounteredLabels[distinctLabelCount++] = label;
            }

            labelCounts[label]++;
        }

        return distinctLabelCount;
    }

    private static void ReorderLeavesByModule(HierarchyNode parent, int[] labelsAtDepth, int distinctLabelCount, HierarchyBuildWorkspace workspace)
    {
        int[] encounteredLabels = workspace.EncounteredLabels;
        int[] labelCounts = workspace.LabelCounts;
        int[] labelCursor = workspace.LabelCursor;
        ReadOnlySpan<int> parentLeafSpan = parent.GetLeafSpan();
        int segmentOffset = 0;
        for (int i = 0; i < distinctLabelCount; i++)
        {
            int label = encounteredLabels[i];
            labelCursor[label] = segmentOffset;
            segmentOffset += labelCounts[label];
        }

        // Assign each leaf to correct label-bucket. Effectively a bucket sort of nodes.
        int[] leafScratch = workspace.RentLeafScratch(parent.LeafCount);
        for (int i = 0; i < parentLeafSpan.Length; i++)
        {
            int leafId = parentLeafSpan[i];
            int label = labelsAtDepth[leafId];
            leafScratch[labelCursor[label]++] = leafId;
        }

        // Keep every module's descendants in a contiguous leaf range.
        Array.Copy(leafScratch, 0, parent.LeafOrder, parent.LeafStart, parent.LeafCount);

    }

    private static void AddModuleChildren(HierarchyNode parent, ModuleSummary[] summaries, int distinctLabelCount, HierarchyBuildWorkspace workspace)
    {
        int[] encounteredLabels = workspace.EncounteredLabels;
        int[] labelCounts = workspace.LabelCounts;
        parent.Children.EnsureCapacity(parent.Children.Count + distinctLabelCount);
        int childLeafStart = parent.LeafStart;
        for (int i = 0; i < distinctLabelCount; i++)
        {
            int label = encounteredLabels[i];
            ModuleSummary summary = summaries[label];
            var child = new HierarchyNode
            {
                Flow = summary.Flow,
                EnterFlow = summary.EnterFlow,
                ExitFlow = summary.ExitFlow,
            };
            child.SetLeafRange(parent.LeafOrder, childLeafStart, labelCounts[label]);
            childLeafStart += labelCounts[label];
            parent.Children.Add(child);
        }

    }

    private static int[] Identity(int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = i;
        return result;
    }
}

