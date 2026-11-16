using MapMinimizer;

namespace MapMinimizer.Tests;

[TestClass]
public class OptimizerTests
{
    [TestMethod]
    public void EdgelessGraphReturnsIdentityAssignments()
    {
        var optimizer = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 1,
            MaxRefinementDepth = 0,
            RandomSeed = 7,
        });

        MapEquationResult result = optimizer.Optimize(new UndirectedGraph(3, []));

        Assert.AreEqual(1, result.LevelCount);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.GetAssignments());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.GetFinestAssignments());
    }

    [TestMethod]
    public void ConnectedPairProducesOneModule()
    {
        var optimizer = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 1,
            MaxRefinementDepth = 0,
            RandomSeed = 7,
        });
        var graph = new UndirectedGraph(2, [new WeightedEdge(0, 1, 1.0)]);

        MapEquationResult result = optimizer.Optimize(graph);

        CollectionAssert.AreEqual(new[] { 0, 0 }, result.GetAssignments());
        Assert.AreEqual(1.0, result.Codelength, 1e-12);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(16)]
    public void WeaklyConnectedTrianglesReturnTheirMeaningfulTopModules(int refinementDepth)
    {
        var graph = new UndirectedGraph(6,
        [
            new WeightedEdge(0, 1, 5.0),
            new WeightedEdge(1, 2, 5.0),
            new WeightedEdge(0, 2, 5.0),
            new WeightedEdge(3, 4, 5.0),
            new WeightedEdge(4, 5, 5.0),
            new WeightedEdge(3, 5, 5.0),
            new WeightedEdge(2, 3, 0.1),
        ]);
        var optimizer = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 20,
            RandomSeed = 7,
            MaxRefinementDepth = refinementDepth,
        });

        MapEquationResult result = optimizer.Optimize(graph);

        Assert.AreEqual(1, result.LevelCount);
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 1 }, result.GetAssignments());
        Assert.AreEqual(1.6204240317717526, result.Codelength, 1e-12);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(16)]
    public void NonImprovingSuperlevelsAreRejected(int refinementDepth)
    {
        UndirectedGraph graph = CreateTriangleRing(4);
        var flat = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 20,
            RandomSeed = 7,
            MaxLevels = 1,
            MaxRefinementDepth = 0,
        }).Optimize(graph);

        var hierarchical = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 20,
            RandomSeed = 7,
            MaxRefinementDepth = refinementDepth,
        }).Optimize(graph);

        Assert.IsTrue(hierarchical.Codelength <= flat.Codelength + 1e-12,
            $"Hierarchy increased codelength from {flat.Codelength:R} to {hierarchical.Codelength:R}.");
        Assert.AreEqual(1, hierarchical.LevelCount);
        Assert.AreEqual(4, hierarchical.GetAssignments().Distinct().Count());
        Assert.AreEqual(1.8550867837820149, hierarchical.Codelength, 1e-12);
    }

    [TestMethod]
    public void ImprovingSuperlevelsAreRetained()
    {
        UndirectedGraph graph = CreateTriangleRing(16);
        var flat = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 20,
            RandomSeed = 7,
            MaxLevels = 1,
            MaxRefinementDepth = 0,
        }).Optimize(graph);
        var hierarchical = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 20,
            RandomSeed = 7,
            MaxRefinementDepth = 0,
        }).Optimize(graph);

        Assert.IsTrue(hierarchical.Codelength < flat.Codelength - 1e-6);
        Assert.IsTrue(hierarchical.LevelCount > 1);
        Assert.IsTrue(hierarchical.GetAssignments().Distinct().Count() > 1);
        Assert.AreEqual(16, hierarchical.GetFinestAssignments().Distinct().Count());
    }

    [TestMethod]
    public void ParameterSnapshotIsNotAffectedByCallerMutation()
    {
        var parameters = new MapEquationParameters
        {
            Trials = 1,
            MaxRefinementDepth = 0,
            RandomSeed = 7,
        };
        var optimizer = new MapEquationOptimizer(parameters);
        parameters.Trials = 0;

        MapEquationResult result = optimizer.Optimize(new UndirectedGraph(2, []));

        Assert.AreEqual(1, result.LevelCount);
    }

    [TestMethod]
    public void AssignmentResultsAreDefensiveCopies()
    {
        var optimizer = new MapEquationOptimizer(new MapEquationParameters
        {
            Trials = 1,
            MaxRefinementDepth = 0,
            RandomSeed = 7,
        });
        MapEquationResult result = optimizer.Optimize(new UndirectedGraph(2, []));

        int[] first = result.GetAssignments();
        first[0] = 100;

        CollectionAssert.AreEqual(new[] { 0, 1 }, result.GetAssignments());
    }

    [TestMethod]
    public void SeededParallelTrialsAreDeterministic()
    {
        var graph = new UndirectedGraph(6,
        [
            new WeightedEdge(0, 1, 5.0),
            new WeightedEdge(1, 2, 5.0),
            new WeightedEdge(0, 2, 5.0),
            new WeightedEdge(3, 4, 5.0),
            new WeightedEdge(4, 5, 5.0),
            new WeightedEdge(3, 5, 5.0),
            new WeightedEdge(2, 3, 0.1),
        ]);
        var parameters = new MapEquationParameters
        {
            Trials = 4,
            MaxRefinementDepth = 0,
            RandomSeed = 1234,
        };

        MapEquationResult first = new MapEquationOptimizer(parameters).Optimize(graph);
        MapEquationResult second = new MapEquationOptimizer(parameters).Optimize(graph);

        Assert.AreEqual(first.LevelCount, second.LevelCount);
        Assert.AreEqual(first.Codelength, second.Codelength, 1e-12);
        for (int level = 0; level < first.LevelCount; level++)
            CollectionAssert.AreEqual(first.GetAssignments(level), second.GetAssignments(level));
    }

    [TestMethod]
    public void IncrementalOptimizerCanVerifyItsState()
    {
        var graph = new UndirectedGraph(4,
        [
            new WeightedEdge(0, 1, 2.0),
            new WeightedEdge(1, 2, 0.1),
            new WeightedEdge(2, 3, 2.0),
        ]);
        FlowNetwork network = FlowNetworkBuilder.Build(graph);
        var optimizer = new NodeMoveOptimizer(network, seed: 17);

        PartitionResult result = optimizer.OptimizeFromSingletons(
            maxPasses: 20,
            deltaTolerance: 1e-12);

        Assert.AreEqual(network.NodeCount, result.Assignments.Length);
        Assert.IsTrue(double.IsFinite(result.Codelength));
        Assert.IsTrue(result.ModuleCount > 0);
    }

    private static UndirectedGraph CreateTriangleRing(int triangleCount)
    {
        var edges = new List<WeightedEdge>();
        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            int first = 3 * triangle;
            edges.Add(new WeightedEdge(first, first + 1, 5.0));
            edges.Add(new WeightedEdge(first, first + 2, 5.0));
            edges.Add(new WeightedEdge(first + 1, first + 2, 5.0));
            edges.Add(new WeightedEdge(first, 3 * ((triangle + 1) % triangleCount), 0.5));
        }

        return new UndirectedGraph(3 * triangleCount, edges.ToArray());
    }
}
