using System.Reflection;
using MapMinimizer;

namespace MapMinimizer.Tests;

[TestClass]
public class RknncIntegrationTests
{
    [TestMethod]
    public void RknncBuildsUndirectedGraphAndReturnsAssignments()
    {
        double[][] data =
        [
            [0.0, 0.0],
            [0.1, 0.0],
            [10.0, 10.0],
            [10.1, 10.0],
        ];

        var rknnc = new RKNNC.RKNNC(data, k: 1, minPoints: 1);
        int[] assignments = rknnc.GetClusterAssignments();

        Assert.AreEqual(data.Length, assignments.Length);
        Assert.IsTrue(assignments.All(label => label >= 0));
    }

    [TestMethod]
    public void MinimumClusterSizeKeepsConnectedPair()
    {
        double[][] data =
        [
            [0.0, 0.0],
            [0.1, 0.0],
        ];

        var rknnc = new RKNNC.RKNNC(data, k: 1, minPoints: 2);
        int[] assignments = rknnc.GetClusterAssignments();

        CollectionAssert.AreEqual(new[] { 0, 0 }, assignments);
    }

    [DataTestMethod]
    [DataRow(1, 2)]
    [DataRow(2, 2)]
    [DataRow(1, int.MaxValue)]
    [DataRow(2, int.MaxValue)]
    public void DuplicateCoordinatesProduceAllReciprocalPairs(int dimensions, int k)
    {
        double[][] data = Enumerable.Range(0, 3).Select(_ => new double[dimensions]).ToArray();
        var rknnc = new RKNNC.RKNNC(data, k, minPoints: 3);
        UndirectedGraph graph = GetGraph(rknnc);

        WeightedEdge[] edges = graph.Edges.ToArray();
        Assert.AreEqual(3, edges.Length);
        Assert.IsTrue(edges.All(edge => edge.Source != edge.Target));
        Assert.AreEqual(3, edges.Select(edge =>
            (Math.Min(edge.Source, edge.Target), Math.Max(edge.Source, edge.Target))).Distinct().Count());
        CollectionAssert.AreEqual(new[] { 0, 0, 0 }, rknnc.GetClusterAssignments());
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void SinglePointWithLargeKHasNoSelfEdges(int dimensions)
    {
        var rknnc = new RKNNC.RKNNC([new double[dimensions]], k: int.MaxValue, minPoints: 1);

        Assert.AreEqual(0, GetGraph(rknnc).Edges.Length);
        CollectionAssert.AreEqual(new[] { 0 }, rknnc.GetClusterAssignments());
    }

    private static UndirectedGraph GetGraph(RKNNC.RKNNC rknnc)
    {
        FieldInfo? graphField = typeof(RKNNC.RKNNC).GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(graphField);
        return (UndirectedGraph)graphField.GetValue(rknnc)!;
    }
}
