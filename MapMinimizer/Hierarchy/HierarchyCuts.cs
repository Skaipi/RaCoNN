namespace MapMinimizer;

/// <summary>Traverses a module tree to obtain complete cuts at each module depth.</summary>
internal static class HierarchyCuts
{
    internal static int[][] GetAssignments(HierarchyNode root, int nodeCount)
    {
        int maxModuleDepth = ComputeMaxModuleDepth(root);
        if (maxModuleDepth < 0) return [new int[nodeCount]];

        var levels = new int[maxModuleDepth + 1][];
        for (int level = 0; level < levels.Length; level++)
        {
            var assignments = new int[nodeCount];
            int nextLabel = 0;
            FillCompleteCut(root, currentModuleDepth: -1, level, assignments, ref nextLabel);
            levels[level] = assignments;
        }

        return levels;
    }

    /// <summary>
    /// Scan module tree with DFS to discover the longest branch.
    /// </summary>
    private static int ComputeMaxModuleDepth(HierarchyNode root)
    {
        var stack = new Stack<(HierarchyNode Node, int Depth)>(64);
        stack.Push((root, -1));

        int maxDepth = -1;
        while (stack.Count > 0)
        {
            (HierarchyNode node, int depth) = stack.Pop();

            for (int childIndex = 0; childIndex < node.Children.Count; childIndex++)
            {
                HierarchyNode child = node.Children[childIndex];
                if (child.IsObject) continue;

                int childDepth = depth + 1;
                maxDepth = Math.Max(maxDepth, childDepth);
                stack.Push((child, childDepth));
            }
        }

        return maxDepth;
    }

    private static void FillCompleteCut(HierarchyNode node, int currentModuleDepth, int targetModuleDepth, int[] assignments, ref int nextLabel)
    {
        bool hasModuleChildren = false;
        for (int childIndex = 0; childIndex < node.Children.Count; childIndex++)
        {
            if (!node.Children[childIndex].IsObject)
            {
                hasModuleChildren = true;
                break;
            }
        }

        // A shallow branch keeps its finest module when the requested cut is deeper.
        if (currentModuleDepth >= 0 && (currentModuleDepth == targetModuleDepth || !hasModuleChildren))
        {
            int label = nextLabel++;
            ReadOnlySpan<int> leaves = node.GetLeafSpan();
            for (int leafIndex = 0; leafIndex < leaves.Length; leafIndex++)
                assignments[leaves[leafIndex]] = label;
            return;
        }

        for (int childIndex = 0; childIndex < node.Children.Count; childIndex++)
        {
            HierarchyNode child = node.Children[childIndex];
            if (!child.IsObject) FillCompleteCut(child, currentModuleDepth + 1, targetModuleDepth, assignments, ref nextLabel);
        }
    }
}

