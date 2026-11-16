namespace MapMinimizer;

/// <summary>
/// Recomputes the hierarchical map-equation score directly from a hierarchy tree.
/// </summary>
internal static class HierarchicalScorer
{
    private const double InvLog2 = 1.4426950408889634;

    /// <summary>
    /// Evaluates the total codelength together with its index/module decomposition.
    /// </summary>
    internal static HierarchicalScore Evaluate(HierarchyNode root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));

        double total = EvaluateIterative(root, treatRootAsTopIndex: true, out double index, out double module);
        return new HierarchicalScore(total, index, module);
    }

    /// <summary>
    /// Evaluates one subtree as the contribution of a non-root module.
    /// </summary>
    internal static double EvaluateSubtreeCodelength(HierarchyNode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        return node.IsLeaf ? 0.0 : EvaluateIterative(node, treatRootAsTopIndex: false, out _, out _);
    }

    /// <summary>
    /// Evaluates a hierarchy or subtree using an explicit postorder traversal.
    /// </summary>
    private static double EvaluateIterative(
        HierarchyNode root,
        bool treatRootAsTopIndex,
        out double totalIndex,
        out double totalModule)
    {
        totalIndex = 0.0;
        totalModule = 0.0;
        if (root.IsLeaf) return 0.0;

        var stack = new List<LocalScore>(64) { GetLocalScore(root, treatRootAsTopIndex) };

        while (stack.Count > 0)
        {
            int scoreIndex = stack.Count - 1;
            var score = stack[scoreIndex];

            while (score.NextChildIndex < score.Node.Children.Count)
            {
                var child = score.Node.Children[score.NextChildIndex++];
                stack[scoreIndex] = score; // score is copied struct so we have to update it on stack

                if (child.IsLeaf) continue;

                stack.Add(GetLocalScore(child, treatAsTopIndex: false));
                goto ContinueTraversal;
            }

            double subtreeTotal = score.LocalTotal + score.DescendantTotal;
            double subtreeIndex = score.LocalIndex + score.DescendantIndex;
            double subtreeModule = score.LocalModule + score.DescendantModule;
            stack.RemoveAt(scoreIndex);

            if (stack.Count == 0)
            {
                totalIndex = subtreeIndex;
                totalModule = subtreeModule;
                return subtreeTotal;
            }

            int parentIndex = stack.Count - 1;
            var parent = stack[parentIndex];
            parent.DescendantTotal += subtreeTotal;
            parent.DescendantIndex += subtreeIndex;
            parent.DescendantModule += subtreeModule;
            stack[parentIndex] = parent; // score is copied struct so we have to update it on stack

        ContinueTraversal:
            ;
        }

        return 0.0;
    }

    /// <summary>
    /// Computes the local map-equation contribution for one hierarchy node before descending into its children.
    /// </summary>
    private static LocalScore GetLocalScore(HierarchyNode node, bool treatAsTopIndex)
    {
        var score = new LocalScore(node);
        if (node.IsLeaf) return score;

        if (treatAsTopIndex && !node.Children[0].IsObject)
        {
            double totalEnterRate = node.ExitFlow;
            double enterEntropy = 0.0;
            foreach (var child in node.Children)
            {
                totalEnterRate += child.EnterFlow;
                enterEntropy += Plogp(child.EnterFlow);
            }

            double localIndex = Plogp(totalEnterRate) - Plogp(node.ExitFlow) - enterEntropy;
            score.LocalIndex = localIndex;
            score.LocalTotal = localIndex;
            return score;
        }

        if (node.Children[0].IsObject)
        {
            double codebookUseRate = node.ExitFlow;
            double nodeEntropy = 0.0;
            foreach (var child in node.Children)
            {
                codebookUseRate += child.Flow;
                nodeEntropy += Plogp(child.Flow);
            }

            double localModule = Plogp(codebookUseRate) - Plogp(node.ExitFlow) - nodeEntropy;
            score.LocalModule = localModule;
            score.LocalTotal = localModule;
            return score;
        }

        double submapUseRate = node.ExitFlow;
        double childEnterEntropy = 0.0;
        foreach (var child in node.Children)
        {
            submapUseRate += child.EnterFlow;
            childEnterEntropy += Plogp(child.EnterFlow);
        }

        double localSubmapIndex = Plogp(submapUseRate) - Plogp(node.ExitFlow) - childEnterEntropy;
        score.LocalIndex = localSubmapIndex;
        score.LocalTotal = localSubmapIndex;
        return score;
    }

    private static double Plogp(double x) => x <= 0.0 ? 0.0 : x * Math.Log(x) * InvLog2;

    /// <summary>
    /// Score value for module in hierarchy tree independent of parent/children score
    /// </summary>
    private struct LocalScore
    {
        internal readonly HierarchyNode Node;
        internal int NextChildIndex;
        internal double LocalTotal;
        internal double LocalIndex;
        internal double LocalModule;
        internal double DescendantTotal;
        internal double DescendantIndex;
        internal double DescendantModule;

        internal LocalScore(HierarchyNode node)
        {
            Node = node;
            NextChildIndex = 0;
            LocalTotal = 0.0;
            LocalIndex = 0.0;
            LocalModule = 0.0;
            DescendantTotal = 0.0;
            DescendantIndex = 0.0;
            DescendantModule = 0.0;
        }
    }
}
