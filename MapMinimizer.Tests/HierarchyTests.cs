using MapMinimizer;

namespace MapMinimizer.Tests;

[TestClass]
public class HierarchyTests
{
    [TestMethod]
    public void CompleteCutsDoNotMergeUnbalancedBranches()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(4, []));
        ModuleHierarchy hierarchy = HierarchyTreeBuilder.Build(network,
        [
            [0, 0, 1, 1],
            [0, 0, 2, 3],
        ]);

        int[][] levels = hierarchy.GetAssignments(4);

        Assert.AreEqual(2, levels.Length);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, levels[0]);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 2 }, levels[1]);
        AssertRefines(levels[0], levels[1]);
    }

    [TestMethod]
    public void ObjectLeavesAreNotReturnedAsClusterLevel()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(4, []));
        ModuleHierarchy hierarchy = HierarchyTreeBuilder.Build(network, [[0, 0, 1, 1]]);

        int[][] levels = hierarchy.GetAssignments(4);

        Assert.AreEqual(1, levels.Length);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, levels[0]);
    }

    [TestMethod]
    public void RedundantEnclosingIndexModuleIsRemoved()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(4,
        [
            new WeightedEdge(0, 1, 5.0),
            new WeightedEdge(1, 2, 0.1),
            new WeightedEdge(2, 3, 5.0),
        ]));
        ModuleHierarchy hierarchy = HierarchyTreeBuilder.Build(network,
        [
            [0, 0, 0, 0],
            [0, 0, 1, 1],
        ]);
        ModuleHierarchy flat = HierarchyTreeBuilder.Build(network, [[0, 0, 1, 1]]);

        int[][] levels = hierarchy.GetAssignments(4);

        Assert.AreEqual(1, levels.Length);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, levels[0]);
        Assert.AreEqual(2, hierarchy.Root.Children.Count);
        Assert.AreEqual(HierarchicalScorer.Evaluate(flat.Root).TotalCodelength,
            HierarchicalScorer.Evaluate(hierarchy.Root).TotalCodelength, 1e-12);
    }

    [TestMethod]
    public void SingleLeafModuleIsPreserved()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(2, [new WeightedEdge(0, 1, 1.0)]));
        ModuleHierarchy hierarchy = HierarchyTreeBuilder.Build(network, [[0, 0]]);

        int[][] levels = hierarchy.GetAssignments(2);

        Assert.AreEqual(1, levels.Length);
        CollectionAssert.AreEqual(new[] { 0, 0 }, levels[0]);
        Assert.AreEqual(1.0, HierarchicalScorer.Evaluate(hierarchy.Root).TotalCodelength, 1e-12);
    }

    [TestMethod]
    public void RefinementRemovesEnclosingModuleAfterFindingSubmodules()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(4,
        [
            new WeightedEdge(0, 1, 5.0),
            new WeightedEdge(1, 2, 0.1),
            new WeightedEdge(2, 3, 5.0),
        ]));
        ModuleHierarchy hierarchy = HierarchyTreeBuilder.Build(network, [[0, 0, 0, 0]]);
        double initialScore = HierarchicalScorer.Evaluate(hierarchy.Root).TotalCodelength;
        var parameters = new MapEquationParameters();
        var refiner = new SubmoduleSearch(parameters, new SupermoduleSearch(parameters));

        refiner.RefineHierarchy(network, hierarchy, maxDepth: 1, new Random(7));

        int[][] levels = hierarchy.GetAssignments(4);
        Assert.AreEqual(1, levels.Length);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, levels[0]);
        Assert.IsTrue(HierarchicalScorer.Evaluate(hierarchy.Root).TotalCodelength < initialScore);
    }

    private static void AssertRefines(int[] coarse, int[] fine)
    {
        for (int left = 0; left < fine.Length; left++)
        {
            for (int right = left + 1; right < fine.Length; right++)
            {
                if (fine[left] == fine[right])
                    Assert.AreEqual(coarse[left], coarse[right]);
            }
        }
    }
}
