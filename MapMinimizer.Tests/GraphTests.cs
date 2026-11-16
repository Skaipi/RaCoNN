using MapMinimizer;

namespace MapMinimizer.Tests;

[TestClass]
public class GraphTests
{
    [TestMethod]
    public void ConstructorCopiesAndValidatesEdges()
    {
        var edges = new[] { new WeightedEdge(0, 1, 2.0) };
        var graph = new UndirectedGraph(2, edges);
        edges[0] = new WeightedEdge(1, 1, 10.0);

        Assert.AreEqual(0, graph.Edges[0].Source);
        Assert.AreEqual(1, graph.Edges[0].Target);
        Assert.AreEqual(2.0, graph.Edges[0].Weight);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new UndirectedGraph(2, [new WeightedEdge(-1, 0, 1.0)]));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new UndirectedGraph(2, [new WeightedEdge(0, 2, 1.0)]));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new UndirectedGraph(2, [new WeightedEdge(0, 1, double.NaN)]));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new UndirectedGraph(2, [new WeightedEdge(0, 1, -1.0)]));
    }

    [TestMethod]
    public void FlowNetworkNormalizesUndirectedEdges()
    {
        var graph = new UndirectedGraph(2, [new WeightedEdge(0, 1, 2.0)]);
        FlowNetwork network = FlowNetworkBuilder.Build(graph);

        Assert.AreEqual(0.5, network.Nodes[0].Prior, 1e-12);
        Assert.AreEqual(0.5, network.Nodes[1].Prior, 1e-12);
        Assert.AreEqual(0.5, network.GetEdges(0)[0].Flow, 1e-12);
        Assert.AreEqual(0.5, network.GetEdges(1)[0].Flow, 1e-12);
        Assert.AreEqual(1, network.GetEdges(0)[0].Target);
        Assert.AreEqual(0, network.GetEdges(1)[0].Target);
    }

    [TestMethod]
    public void FlowNetworkUsesUniformPriorForEdgelessGraph()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(3, []));

        for (int nodeId = 0; nodeId < 3; nodeId++)
        {
            Assert.AreEqual(1.0 / 3.0, network.Nodes[nodeId].Prior, 1e-12);
            Assert.AreEqual(0, network.GetEdges(nodeId).Length);
        }
    }

    [TestMethod]
    public void FlowNetworkRejectsOverflowingTotalWeight()
    {
        var graph = new UndirectedGraph(2, [new WeightedEdge(0, 1, double.MaxValue)]);
        Assert.ThrowsException<ArgumentException>(() => FlowNetworkBuilder.Build(graph));
    }
}
