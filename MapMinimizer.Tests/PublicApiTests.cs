using MapMinimizer;

namespace MapMinimizer.Tests;

[TestClass]
public class PublicApiTests
{
    [TestMethod]
    public void NamedHierarchyCutsPreserveExistingApiAndReturnCopies()
    {
        var edges = new List<WeightedEdge>();
        for (int group = 0; group < 16; group++)
        {
            int node = group * 3;
            edges.Add(new(node, node + 1, 5));
            edges.Add(new(node, node + 2, 5));
            edges.Add(new(node + 1, node + 2, 5));
            edges.Add(new(node, 3 * ((group + 1) % 16), .5));
        }
        var optimizer = new MapEquationOptimizer(new() { Trials = 1, RandomSeed = 7 });
        var result = optimizer.Optimize(new(48, edges.ToArray()));

        Assert.IsTrue(result.LevelCount > 1);
        CollectionAssert.AreEqual(result.GetAssignments(), result.GetCoarsestAssignments());
        int[] coarse = result.GetCoarsestAssignments(), fine = result.GetFinestAssignments();
        coarse[0] = fine[0] = -123;
        Assert.IsTrue(result.GetCoarsestAssignments().All(label => label >= 0));
        Assert.IsTrue(result.GetFinestAssignments().All(label => label >= 0));
    }

    [TestMethod]
    public void ClusterEntryPointKeepsCompatibilityAndMinimumSizeFiltering()
    {
        double[][] data = [[0, 0], [.1, 0], [10, 10], [10.1, 10]];
        var model = new RKNNC.RKNNC(data, k: 1, minPoints: 2);
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, model.Cluster());
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, model.GetClusterAssignments());
        CollectionAssert.AreEqual(new[] { -1, -1, -1, -1 }, new RKNNC.RKNNC(data, 1, minPoints: 3).Cluster());
    }
}
