using System.Buffers;

namespace MapMinimizer;

/// <summary>
/// Mutable partition, pooled module storage, and cached entropy totals for one flow network.
/// </summary>
internal sealed class PartitionState : IDisposable
{
    private readonly FlowNetwork network;
    private readonly double networkExitFlowPlogp;

    /// <summary>
    /// Lookup table for module id based on graph node id. Main part of mutable state for optimizer.
    /// </summary>
    private int[] moduleOfObject = Array.Empty<int>();
    private ModuleState[] moduleData = Array.Empty<ModuleState>();
    private int moduleSlotCount;
    private bool isDisposed;
    private readonly Stack<int> freeModuleIds = new();
    private int nextUnusedModuleId;
    private int activeModuleCount;

    private double objectPriorEntropy;
    private double moduleUseRateEntropy;
    private double moduleEntryRateEntropy;
    private double moduleExitRateEntropy;
    private double totalEntryRate;
    private double totalEntryRatePlogp;

    internal double IndexCodelength { get; set; }
    internal double ModuleCodelength { get; set; }
    internal double Codelength { get; set; }

    internal FlowNetwork Network => network;
    internal MapEquation Equation { get; }
    internal double NetworkExitFlowPlogp => networkExitFlowPlogp;
    internal double ObjectPriorEntropy => objectPriorEntropy;
    internal double ModuleUseRateEntropy => moduleUseRateEntropy;
    internal double ModuleEntryRateEntropy => moduleEntryRateEntropy;
    internal double ModuleExitRateEntropy => moduleExitRateEntropy;
    internal double TotalEntryRate => totalEntryRate;
    internal double TotalEntryRatePlogp => totalEntryRatePlogp;

    internal int[] Assignments => moduleOfObject;
    internal ModuleState[] Modules => moduleData;
    internal int ActiveModuleCount => activeModuleCount;
    internal int ModuleSlotCount => moduleSlotCount;

    /// <summary>
    /// Attaches the state machine to one immutable flow network.
    /// </summary>
    internal PartitionState(FlowNetwork activeNetwork)
    {
        network = activeNetwork ?? throw new ArgumentNullException(nameof(activeNetwork));
        networkExitFlowPlogp = MapEquation.Plogp(network.ExitFlow);
        Equation = new MapEquation(this);
    }

    /// <summary>
    /// Rebuilds all module aggregates and codelength terms from an explicit partition.
    /// </summary>
    internal void Initialize(int[] initialModuleOfObject)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (initialModuleOfObject == null)
            throw new ArgumentNullException(nameof(initialModuleOfObject));
        if (initialModuleOfObject.Length != network.NodeCount)
            throw new ArgumentException("initialModuleOfObject must have one module id per object.");

        moduleOfObject = initialModuleOfObject;
        Labeling.Compact(moduleOfObject, out int moduleCount);
        ResizeModuleData(Math.Max(moduleCount, network.NodeCount), preserveModules: false);
        freeModuleIds.Clear();
        nextUnusedModuleId = moduleCount;
        activeModuleCount = moduleCount;

        objectPriorEntropy = 0.0;

        for (int u = 0; u < network.NodeCount; u++)
        {
            int m = moduleOfObject[u];
            double p = network.Nodes[u].Prior;

            objectPriorEntropy += MapEquation.Plogp(p);
            moduleData[m].ObjectPriorSum += p;
            moduleData[m].MemberCount++;
        }

        ComputeModuleFlow();
        ComputeModuleEntropy();
        ComputeEntropyTotals();
    }

    /// <summary>
    /// Reserves an empty module slot for trying singleton moves.
    /// </summary>
    internal int CreateEmptyModule()
    {
        EnsureInitialized();

        if (freeModuleIds.Count > 0)
            return freeModuleIds.Pop();

        if (nextUnusedModuleId < moduleSlotCount)
            return nextUnusedModuleId++;

        ResizeModuleData(Math.Max(1, moduleSlotCount * 2), preserveModules: true);
        return nextUnusedModuleId++;
    }

    /// <summary>
    /// Returns an unused module slot to the free list when a speculative singleton move was rejected.
    /// </summary>
    internal void ReleaseEmptyModule(int moduleId)
    {
        EnsureInitialized();

        if ((uint)moduleId >= (uint)moduleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(moduleId));
        if (moduleData[moduleId].MemberCount != 0)
            throw new InvalidOperationException("Only empty modules can be released.");

        freeModuleIds.Push(moduleId);
    }

    /// <summary>
    /// Commits a previously evaluated move and updates the incremental score terms in place.
    /// </summary>
    internal void ApplyMove(NodeMoveDelta move)
    {
        EnsureInitialized();

        if ((uint)move.ObjectId >= (uint)network.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(move));
        if ((uint)move.SourceModule >= (uint)moduleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(move));
        if ((uint)move.TargetModule >= (uint)moduleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(move));
        if (moduleOfObject[move.ObjectId] != move.SourceModule)
            throw new InvalidOperationException("Move is stale: object is no longer in move.SourceModule.");
        if (move.IsNoOp)
            return;

        var oldBeforeMove = moduleData[move.SourceModule];
        var newBeforeMove = moduleData[move.TargetModule];

        moduleEntryRateEntropy +=
            -oldBeforeMove.ModuleEnterPlogp
            - newBeforeMove.ModuleEnterPlogp
            + move.SourceModuleAfter.ModuleEnterPlogp
            + move.TargetModuleAfter.ModuleEnterPlogp;

        moduleExitRateEntropy +=
            -oldBeforeMove.ModuleExitPlogp
            - newBeforeMove.ModuleExitPlogp
            + move.SourceModuleAfter.ModuleExitPlogp
            + move.TargetModuleAfter.ModuleExitPlogp;

        moduleUseRateEntropy +=
            -oldBeforeMove.CodebookUsePlogp
            - newBeforeMove.CodebookUsePlogp
            + move.SourceModuleAfter.CodebookUsePlogp
            + move.TargetModuleAfter.CodebookUsePlogp;

        totalEntryRate = move.TotalModuleEnterRateAfter;
        totalEntryRatePlogp = move.TotalModuleEnterRateAfterPlogp;

        // Remove empty module
        if (oldBeforeMove.MemberCount == 1)
        {
            activeModuleCount--;
            freeModuleIds.Push(move.SourceModule);
        }

        // New module created
        if (newBeforeMove.MemberCount == 0)
        {
            activeModuleCount++;
        }

        moduleData[move.SourceModule] = move.SourceModuleAfter;
        moduleData[move.TargetModule] = move.TargetModuleAfter;
        moduleOfObject[move.ObjectId] = move.TargetModule;

        Equation.UpdateCodelength();
    }

    /// <summary>
    /// Recomputes all entropy-like terms from the module aggregates.
    /// </summary>
    private void ComputeEntropyTotals()
    {
        moduleUseRateEntropy = 0.0;
        moduleEntryRateEntropy = 0.0;
        moduleExitRateEntropy = 0.0;
        totalEntryRate = network.ExitFlow;

        for (int m = 0; m < moduleSlotCount; m++)
        {
            moduleUseRateEntropy += moduleData[m].CodebookUsePlogp;
            moduleEntryRateEntropy += moduleData[m].ModuleEnterPlogp;
            moduleExitRateEntropy += moduleData[m].ModuleExitPlogp;
            totalEntryRate += moduleData[m].ModuleEnterRate;
        }

        totalEntryRatePlogp = MapEquation.Plogp(totalEntryRate);

        Equation.UpdateCodelength();
    }

    /// <summary>
    /// Guards methods that require an explicit partition to have been loaded first.
    /// </summary>
    internal void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (moduleOfObject.Length != network.NodeCount)
            throw new InvalidOperationException("Call Initialize(...) before using the delta engine.");
    }

    /// <summary>
    /// Resizes the logical module slots, reusing a rented buffer when it is already large enough.
    /// </summary>
    private void ResizeModuleData(int newSlotCount, bool preserveModules)
    {
        if (moduleData.Length < newSlotCount)
        {
            ModuleState[] expanded = ArrayPool<ModuleState>.Shared.Rent(newSlotCount);
            if (preserveModules)
                Array.Copy(moduleData, expanded, moduleSlotCount);

            if (moduleData.Length > 0)
                ArrayPool<ModuleState>.Shared.Return(moduleData, clearArray: false);
            moduleData = expanded;
        }

        // A rented buffer may contain old module summaries, including beyond the logical slots.
        // Clear every slot that becomes visible, even when no new buffer was needed.
        int clearStart = preserveModules ? moduleSlotCount : 0;
        Array.Clear(moduleData, clearStart, newSlotCount - clearStart);
        moduleSlotCount = newSlotCount;
    }

    /// <summary>
    /// Returns the module buffer to the pool. Disposing more than once is safe.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        ModuleState[] rented = moduleData;
        moduleData = Array.Empty<ModuleState>();
        moduleSlotCount = 0;
        moduleOfObject = Array.Empty<int>();
        freeModuleIds.Clear();
        nextUnusedModuleId = 0;
        activeModuleCount = 0;
        if (rented.Length > 0)
            ArrayPool<ModuleState>.Shared.Return(rented, clearArray: false);
    }

    /// <summary>
    /// Compute flow between modules
    /// </summary>
    private void ComputeModuleFlow()
    {
        for (int u = 0; u < network.NodeCount; u++)
        {
            int mu = moduleOfObject[u];
            moduleData[mu].ModuleEnterRate += network.Nodes[u].ExternalFlow;
            moduleData[mu].ModuleExitRate += network.Nodes[u].ExternalFlow;
            ReadOnlySpan<EdgeData> edges = network.GetEdges(u);
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                EdgeData edge = edges[edgeIndex];
                int mv = moduleOfObject[edge.Target];
                if (mu == mv) continue;

                moduleData[mu].ModuleExitRate += edge.Flow;
                moduleData[mv].ModuleEnterRate += edge.Flow;
            }
        }
    }

    /// <summary>
    /// Refreshes the cached entropy terms for all module slots after a full rebuild.
    /// </summary>
    private void ComputeModuleEntropy()
    {
        for (int m = 0; m < moduleSlotCount; m++)
            MapEquation.RefreshEntropyTerms(ref moduleData[m]);
    }
}
