namespace MapMinimizer;

/// <summary>The module tree obtained from single optimization trial.</summary>
internal sealed class ModuleHierarchy
{
    internal HierarchyNode Root { get; }

    internal ModuleHierarchy(HierarchyNode root) => Root = root;

    /// <summary>Returns complete module cuts from the coarsest to the finest level.</summary>
    internal int[][] GetAssignments(int nodeCount) => HierarchyCuts.GetAssignments(Root, nodeCount);
}

