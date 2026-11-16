namespace MapMinimizer;

/// <summary>
/// Greedy node-moving step of Infomap's two-level phase.
/// Each optimizer owns its mutable partition and reusable traversal workspace.
/// </summary>
internal sealed class NodeMoveOptimizer : IDisposable
{
    private readonly FlowNetwork network;
    private readonly PartitionState partition;
    private readonly MapEquation equation;
    private NodeMoveWorkspace workspace;
    private bool disposed;

    internal int ProcessedObjectCount { get; private set; }

    internal NodeMoveOptimizer(FlowNetwork activeNetwork, int seed)
    {
        network = activeNetwork ?? throw new ArgumentNullException(nameof(activeNetwork));
        partition = new PartitionState(network);
        equation = partition.Equation;
        workspace = new NodeMoveWorkspace(partition, seed);
    }

    /// <summary>
    /// Runs repeated move passes starting from the finest possible partition.
    /// </summary>
    internal PartitionResult OptimizeFromSingletons(int maxPasses, double deltaTolerance)
        => Optimize(Enumerable.Range(0, network.NodeCount).ToArray(), maxPasses, deltaTolerance);

    /// <summary>
    /// Resumes node moves from current partition without changing the supplied labels.
    /// </summary>
    internal PartitionResult Optimize(int[] initialPartition, int maxPasses, double deltaTolerance)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ProcessedObjectCount = 0;
        if (initialPartition == null) throw new ArgumentNullException(nameof(initialPartition));
        if (initialPartition.Length != network.NodeCount) throw new ArgumentException("Partition must have one label per object.", nameof(initialPartition));
        if (network.NodeCount == 0) return new PartitionResult([], 0, 0.0);

        workspace.Reset();
        partition.Initialize((int[])initialPartition.Clone());
        double initialCodelength = partition.Codelength;
        workspace.EnsureModuleWorkspaceCapacity(partition.ModuleSlotCount);
        workspace.SeedAllNodes();

        for (int pass = 0; pass < maxPasses; pass++)
        {
            double codelengthBeforePass = partition.Codelength;
            int acceptedThisPass = RunPass(deltaTolerance);

            if (acceptedThisPass == 0) break;

            if (codelengthBeforePass - partition.Codelength <= deltaTolerance) break;
            if (pass + 1 >= maxPasses) continue;

            // Pending affected nodes lead the next pass. Complete the sweep with the remaining objects, as the global index rate also affects deltas.
            workspace.SeedAllNodes();
        }

        return CapturePartition(initialCodelength);
    }

    /// <summary>
    /// Processes the current worklist once and returns the number of accepted moves.
    /// </summary>
    private int RunPass(double deltaTolerance)
    {
        int acceptedThisPass = 0;

        // Freeze the worklist for this pass. Accepted moves reactivate nodes
        // for the next pass; draining those immediately made MaxPasses an
        // ineffective limit and repeatedly rescanned the same neighborhoods.
        int remainingThisPass = workspace.QueuedNodeCount;
        while (remainingThisPass-- > 0 && workspace.TryDequeueNode(out int objectId))
        {
            ProcessedObjectCount++;
            if (!TryFindBestMove(objectId, deltaTolerance, out NodeMoveDelta bestMove))
                continue;

            partition.ApplyMove(bestMove);
            workspace.EnsureModuleWorkspaceCapacity(partition.ModuleSlotCount);

            acceptedThisPass++;

            workspace.EnqueueAffectedNodes(bestMove);
        }

        return acceptedThisPass;
    }

    /// <summary>
    /// Copies the final partition into compact labels and captures its codelength.
    /// </summary>
    private PartitionResult CapturePartition(double initialCodelength)
    {
        int[] finalPartition = Labeling.Compact((int[])partition.Assignments.Clone(), out int moduleCount);

        return new PartitionResult(
            finalPartition,
            moduleCount,
            partition.Codelength,
            initialCodelength);
    }

    /// <summary>
    /// Finds the best improving move for one object by scanning its neighborhood once.
    /// </summary>
    private bool TryFindBestMove(int objectId, double deltaTolerance, out NodeMoveDelta bestMove)
    {
        bestMove = default;

        int[] candidateModules = workspace.CandidateModules;
        int oldModule = partition.Assignments[objectId];
        int candidateCount = workspace.ScanCandidateModules(objectId, out double totalFlow, out int stamp);
        double flowToSourceModule = workspace.GetScannedModuleFlow(oldModule, stamp);
        if (candidateCount == 0 && partition.Modules[oldModule].MemberCount == 1) return false;
        // For an atomic node with no external or inter-module links, the only
        // alternative is a singleton. Its delta is at least twice its visit rate,
        // so no improving move exists. Contracted objects need the general test.
        if (network.HasAtomicNodeFlow && network.Nodes[objectId].ExternalFlow == 0.0 &&
            (candidateCount == 0 || (candidateCount == 1 && candidateModules[0] == oldModule)))
            return false;
        var preparation = equation.PrepareMove(objectId, totalFlow, flowToSourceModule);

        bool foundImprovingMove = false;
        double bestDelta = -deltaTolerance;
        int bestTarget = -1;
        double bestFlow = 0.0;

        for (int i = 0; i < candidateCount; i++)
        {
            int candidateModule = candidateModules[i];
            if (candidateModule == oldModule)
                continue;

            double flow = workspace.GetScannedModuleFlow(candidateModule, stamp);
            double delta = equation.EvaluateMoveScore(preparation, candidateModule, flow);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestTarget = candidateModule;
                bestFlow = flow;
                foundImprovingMove = true;
            }
        }

        if (partition.Modules[oldModule].MemberCount > 1)
        {
            int emptyModule = partition.CreateEmptyModule();
            workspace.EnsureModuleWorkspaceCapacity(partition.ModuleSlotCount);

            double singletonDelta = equation.EvaluateMoveScore(preparation, emptyModule, 0.0);
            if (singletonDelta < bestDelta)
            {
                bestTarget = emptyModule;
                bestFlow = 0.0;
                foundImprovingMove = true;
            }
            else
            {
                partition.ReleaseEmptyModule(emptyModule);
            }
        }

        if (foundImprovingMove)
            bestMove = equation.EvaluateMove(preparation, bestTarget, bestFlow);
        return foundImprovingMove;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        partition.Dispose();
        workspace.Dispose();
    }
}
