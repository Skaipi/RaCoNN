using MapMinimizer;

namespace MapMinimizer.Tests;

[TestClass]
public class CandidateMoveTests
{
    [DataTestMethod]
    [DataRow("weighted")]
    [DataRow("induced")]
    [DataRow("coarsened")]
    [DataRow("edgeless")]
    public void PreparedCandidatesAndAppliedMovesMatchDirectCodebookEntropy(string kind)
    {
        FlowNetwork network = CreateNetwork(kind);
        int[][] partitions =
        [
            new int[network.NodeCount],
            Enumerable.Range(0, network.NodeCount).ToArray(),
            Enumerable.Range(0, network.NodeCount).Select(i => i % 3).ToArray(),
        ];

        foreach (int[] partition in partitions)
        {
            using var equation = new PartitionState(network);
            equation.Initialize((int[])partition.Clone());
            AssertScoreMatchesDirectEntropy(network, equation);

            for (int objectId = 0; objectId < network.NodeCount; objectId++)
            {
                int source = equation.Assignments[objectId];
                double totalFlow = 0.0;
                var flowByModule = new double[equation.ModuleSlotCount];
                foreach (EdgeData edge in network.GetEdges(objectId))
                {
                    if (edge.Target == objectId)
                        continue;
                    totalFlow += edge.Flow;
                    flowByModule[equation.Assignments[edge.Target]] += edge.Flow;
                }

                int[] beforePartition = (int[])equation.Assignments.Clone();
                ModuleState[] beforeModules = equation.Modules.AsSpan(0, equation.ModuleSlotCount).ToArray();
                double beforeScore = equation.Codelength;
                double directBefore = DirectEntropy(network, beforePartition).Total;
                NodeMovePreparation preparation = equation.Equation.PrepareMove(objectId, totalFlow, flowByModule[source]);

                // Reserving a singleton slot after preparing may resize the module array.
                int empty = equation.CreateEmptyModule();
                int[] targets = beforePartition.Distinct().Append(empty).ToArray();
                int chosenTarget = objectId % 2 == 0
                    ? empty
                    : beforePartition[(objectId + 1) % network.NodeCount];
                NodeMoveDelta chosenMove = default;

                foreach (int target in targets)
                {
                    double targetFlow = target < flowByModule.Length ? flowByModule[target] : 0.0;
                    double score = equation.Equation.EvaluateMoveScore(in preparation, target, targetFlow);
                    NodeMoveDelta move = equation.Equation.EvaluateMove(in preparation, target, targetFlow);
                    var stats = new NodeMoveStatistics(totalFlow, flowByModule[source], targetFlow);
                    NodeMoveDelta compatibilityMove = equation.Equation.EvaluateMove(objectId, target, in stats);
                    int[] afterPartition = (int[])beforePartition.Clone();
                    afterPartition[objectId] = target;
                    double directAfter = DirectEntropy(network, afterPartition).Total;

                    Assert.AreEqual(directAfter - directBefore, move.CodelengthDelta, 1e-12,
                        $"{kind}, object {objectId}, source {source}, target {target}");
                    Assert.AreEqual(directAfter - directBefore, score, 1e-12);
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(move.CodelengthDelta), BitConverter.DoubleToInt64Bits(score));
                    Assert.AreEqual(move, compatibilityMove);
                    Assert.AreEqual(objectId, move.ObjectId);
                    Assert.AreEqual(source, move.SourceModule);
                    Assert.AreEqual(target, move.TargetModule);
                    if (target == source)
                    {
                        Assert.IsTrue(move.IsNoOp);
                        Assert.AreEqual(0.0, move.CodelengthDelta);
                        Assert.AreEqual(beforeModules[source], move.SourceModuleAfter);
                        Assert.AreEqual(beforeModules[source], move.TargetModuleAfter);
                    }
                    else
                    {
                        Assert.AreEqual(beforeModules[source].MemberCount - 1, move.SourceModuleAfter.MemberCount);
                        int targetCount = beforePartition.Count(module => module == target);
                        Assert.AreEqual(targetCount + 1, move.TargetModuleAfter.MemberCount);
                        Assert.AreEqual(move.SourceModuleAfter.ModuleEnterPlogp, move.SourceModuleAfter.ModuleExitPlogp);
                        Assert.AreEqual(move.TargetModuleAfter.ModuleEnterPlogp, move.TargetModuleAfter.ModuleExitPlogp);
                    }

                    if (target == chosenTarget)
                        chosenMove = move;
                }

                CollectionAssert.AreEqual(beforePartition, equation.Assignments);
                for (int module = 0; module < beforeModules.Length; module++)
                    Assert.AreEqual(beforeModules[module], equation.Modules[module]);
                Assert.AreEqual(beforeScore, equation.Codelength);

                equation.ApplyMove(chosenMove);
                Assert.AreEqual(beforeScore + chosenMove.CodelengthDelta, equation.Codelength, 1e-12);
                AssertScoreMatchesDirectEntropy(network, equation);
                Assert.AreEqual(equation.Assignments.Distinct().Count(), equation.ActiveModuleCount);
                if (chosenTarget != empty)
                    equation.ReleaseEmptyModule(empty);
            }
        }
    }

    [TestMethod]
    public void MoveValidationAndNoOpBehaviorArePreserved()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(2, [new WeightedEdge(0, 1, 1.0)]));
        using var equation = new PartitionState(network);
        var stats = new NodeMoveStatistics(2.0, 0.0, 2.0);
        NodeMovePreparation preparation = default;

        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.PrepareMove(0, 0.5, 0.0));
        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.EvaluateMove(in preparation, 1, 0.5));
        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.EvaluateMoveScore(in preparation, 1, 0.5));
        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.EvaluateMove(0, 1, in stats));
        equation.Initialize([0, 1]);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.PrepareMove(-1, 0.5, 0.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.PrepareMove(2, 0.5, 0.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMove(-1, 0, in stats));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMove(0, 2, in stats));
        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.PrepareMove(0, 2.0, 0.0));

        preparation = equation.Equation.PrepareMove(0, 0.5, 0.0);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMove(in preparation, -1, 0.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMove(in preparation, 2, 0.0));
        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.EvaluateMove(in preparation, 1, 2.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMoveScore(in preparation, -1, 0.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMoveScore(in preparation, 2, 0.0));
        Assert.ThrowsException<InvalidOperationException>(() => equation.Equation.EvaluateMoveScore(in preparation, 1, 2.0));

        double before = equation.Codelength;
        NodeMoveDelta noOp = equation.Equation.EvaluateMove(0, 0, in stats);
        Assert.IsTrue(noOp.IsNoOp);
        Assert.AreEqual(0.0, noOp.CodelengthDelta);
        Assert.AreEqual(noOp, equation.Equation.EvaluateMove(in preparation, 0, double.NaN));
        Assert.AreEqual(0.0, equation.Equation.EvaluateMoveScore(in preparation, 0, double.NaN));
        equation.ApplyMove(noOp);
        Assert.AreEqual(before, equation.Codelength);

        using var empty = new PartitionState(FlowNetworkBuilder.Build(new UndirectedGraph(0, [])));
        empty.Initialize([]);
        Assert.AreEqual(0.0, empty.Codelength);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => empty.Equation.PrepareMove(0, 0.0, 0.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => empty.Equation.EvaluateMoveScore(default, 0, 0.0));
    }

    [TestMethod]
    public void PooledModulesRespectLogicalSlotsAcrossGrowthAndReinitialization()
    {
        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(3,
        [
            new WeightedEdge(0, 1, 2.0),
            new WeightedEdge(1, 2, 0.5),
        ]));
        using var equation = new PartitionState(network);
        equation.Initialize([0, 1, 2]);
        Assert.AreEqual(3, equation.ModuleSlotCount);
        ModuleState[] initial = equation.Modules.AsSpan(0, 3).ToArray();
        NodeMovePreparation preparation = equation.Equation.PrepareMove(0, 0.4, 0.0);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.ReleaseEmptyModule(3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMove(in preparation, 3, 0.0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => equation.Equation.EvaluateMoveScore(in preparation, 3, 0.0));
        Assert.AreEqual(3, equation.CreateEmptyModule());
        Assert.AreEqual(6, equation.ModuleSlotCount);
        for (int module = 0; module < initial.Length; module++)
            Assert.AreEqual(initial[module], equation.Modules[module]);
        for (int module = 3; module < 6; module++)
            Assert.AreEqual(default(ModuleState), equation.Modules[module]);

        // A reused pool buffer can retain arbitrary data outside the next partition's logical slots.
        equation.Modules[5] = new ModuleState
        {
            ObjectPriorSum = double.NaN,
            ModuleEnterRate = double.NaN,
            ModuleEnterPlogp = double.NaN,
            ModuleExitPlogp = double.NaN,
            CodebookUsePlogp = double.NaN,
            MemberCount = 99,
        };
        ModuleState[] rented = equation.Modules;
        equation.Initialize([0, 0, 0]);
        Assert.AreSame(rented, equation.Modules);
        Assert.AreEqual(3, equation.ModuleSlotCount);
        AssertScoreMatchesDirectEntropy(network, equation);

        // Exhausting the logical slots must clear newly exposed pooled storage.
        for (int module = 1; module <= 40; module++)
        {
            Assert.AreEqual(module, equation.CreateEmptyModule());
            Assert.AreEqual(default(ModuleState), equation.Modules[module]);
            Assert.IsTrue(module < equation.ModuleSlotCount);
        }
        AssertScoreMatchesDirectEntropy(network, equation);
        preparation = equation.Equation.PrepareMove(0, 0.4, 0.4);
        double score = equation.Equation.EvaluateMoveScore(in preparation, 40, 0.0);
        NodeMoveDelta move = equation.Equation.EvaluateMove(in preparation, 40, 0.0);
        Assert.AreEqual(move.CodelengthDelta, score);
        equation.ApplyMove(move);
        AssertScoreMatchesDirectEntropy(network, equation);

        equation.Dispose();
        equation.Dispose();
        Assert.AreEqual(0, equation.ModuleSlotCount);
        Assert.AreEqual(0, equation.Modules.Length);
        Assert.ThrowsException<ObjectDisposedException>(() => equation.Initialize([0, 1, 2]));
        Assert.ThrowsException<ObjectDisposedException>(() => equation.CreateEmptyModule());
        Assert.ThrowsException<ObjectDisposedException>(() => equation.ReleaseEmptyModule(0));
        Assert.ThrowsException<ObjectDisposedException>(() => equation.Equation.PrepareMove(0, 0.4, 0.0));
        Assert.ThrowsException<ObjectDisposedException>(() => equation.Equation.EvaluateMove(in preparation, 1, 0.4));
        Assert.ThrowsException<ObjectDisposedException>(() => equation.Equation.EvaluateMoveScore(in preparation, 1, 0.4));
        Assert.ThrowsException<ObjectDisposedException>(() => equation.ApplyMove(move));
    }

    private static FlowNetwork CreateNetwork(string kind)
    {
        if (kind == "edgeless")
            return FlowNetworkBuilder.Build(new UndirectedGraph(4, []));

        FlowNetwork network = FlowNetworkBuilder.Build(new UndirectedGraph(8,
        [
            new WeightedEdge(0, 0, 0.7),
            new WeightedEdge(0, 1, 2.0),
            new WeightedEdge(0, 1, 0.3),
            new WeightedEdge(0, 2, 0.1),
            new WeightedEdge(1, 2, 3.0),
            new WeightedEdge(2, 3, 0.2),
            new WeightedEdge(2, 5, 0.4),
            new WeightedEdge(3, 4, 4.0),
            new WeightedEdge(3, 5, 0.6),
            new WeightedEdge(4, 5, 1.5),
            new WeightedEdge(5, 6, 0.8),
            new WeightedEdge(6, 6, 0.5),
            new WeightedEdge(6, 7, 0.0),
        ]));

        if (kind == "weighted")
            return network;

        network = InducedNetworkBuilder.Build(network, [0, 1, 2, 3, 4, 6, 7], 0, 7, new InducedNetworkWorkspace(network.NodeCount));
        if (kind == "coarsened")
            return FlowNetworkAggregator.BuildSuperNetwork(network, [0, 0, 1, 1, 2, 2, 3], 4);
        return network;
    }

    private static void AssertScoreMatchesDirectEntropy(FlowNetwork network, PartitionState equation)
    {
        var expected = DirectEntropy(network, equation.Assignments);
        Assert.AreEqual(expected.Index, equation.IndexCodelength, 1e-12);
        Assert.AreEqual(expected.Module, equation.ModuleCodelength, 1e-12);
        Assert.AreEqual(expected.Total, equation.Codelength, 1e-12);
    }

    // Direct sum of codeword costs -p * log2(p / codebook mass), independent of the
    // incremental plogp expansion, cached module summaries, and move formulas.
    private static (double Index, double Module, double Total) DirectEntropy(FlowNetwork network, int[] partition)
    {
        var entryCodewords = new List<double> { network.Nodes.Sum(node => node.ExternalFlow) };
        double moduleEntropy = 0.0;
        foreach (var members in Enumerable.Range(0, network.NodeCount).GroupBy(node => partition[node]))
        {
            double boundary = 0.0;
            var codewords = new List<double>();
            foreach (int node in members)
            {
                codewords.Add(network.Nodes[node].Prior);
                boundary += network.Nodes[node].ExternalFlow;
                foreach (EdgeData edge in network.GetEdges(node))
                {
                    if (partition[edge.Target] != members.Key)
                        boundary += edge.Flow;
                }
            }

            entryCodewords.Add(boundary);
            codewords.Add(boundary);
            moduleEntropy += CodebookEntropy(codewords);
        }

        double indexEntropy = CodebookEntropy(entryCodewords);
        return (indexEntropy, moduleEntropy, indexEntropy + moduleEntropy);
    }

    private static double CodebookEntropy(List<double> codewords)
    {
        double total = codewords.Sum();
        double entropy = 0.0;
        foreach (double mass in codewords)
        {
            if (mass > 0.0)
                entropy -= mass * Math.Log2(mass / total);
        }
        return entropy;
    }
}
