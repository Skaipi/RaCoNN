namespace MapMinimizer;

/// <summary>
/// Objective and node-move math for one mutable partition.
/// Candidate evaluation reads the partition without changing it.
/// </summary>
internal readonly struct MapEquation
{
    private const double InvLog2 = 1.4426950408889634;

    private readonly PartitionState partition;
    private readonly FlowNetwork network;

    internal MapEquation(PartitionState partition)
    {
        this.partition = partition ?? throw new ArgumentNullException(nameof(partition));
        network = partition.Network;
    }

    /// <summary>
    /// Precomputes the source update shared by all candidate targets for one object.
    /// </summary>
    /// <remarks>Valid until the partition changes; empty module reservations do not invalidate it.</remarks>
    internal NodeMovePreparation PrepareMove(int objectId, double totalFlow, double flowToSourceModule)
    {
        partition.EnsureInitialized();

        if ((uint)objectId >= (uint)network.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(objectId));

        int oldModule = partition.Assignments[objectId];
        var oldBeforeMove = partition.Modules[oldModule];
        double p = network.Nodes[objectId].Prior;
        double externalFlow = network.Nodes[objectId].ExternalFlow;
        double oldBoundaryAfter = CleanNonNegative(oldBeforeMove.ModuleExitRate + flowToSourceModule - (totalFlow - flowToSourceModule) - externalFlow);

        var oldAfterMove = oldBeforeMove;
        oldAfterMove.ObjectPriorSum = CleanNonNegative(oldBeforeMove.ObjectPriorSum - p);
        oldAfterMove.ModuleEnterRate = oldBoundaryAfter;
        oldAfterMove.ModuleExitRate = oldBoundaryAfter;
        oldAfterMove.MemberCount = oldBeforeMove.MemberCount - 1;
        RefreshEntropyTerms(ref oldAfterMove);

        return new NodeMovePreparation(
            objectId,
            oldModule,
            p,
            externalFlow,
            totalFlow,
            oldBeforeMove,
            oldAfterMove,
            partition.TotalEntryRate);
    }

    /// <summary>
    /// Scores a target without constructing or copying a move snapshot for a losing candidate.
    /// </summary>
    internal double EvaluateMoveScore(in NodeMovePreparation preparation, int newModule, double flowToTargetModule)
    {
        partition.EnsureInitialized();

        if ((uint)preparation.ObjectId >= (uint)network.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(preparation));
        if ((uint)newModule >= (uint)partition.ModuleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(newModule));

        if (preparation.SourceModule == newModule) return 0.0;

        ref readonly ModuleState newBeforeMove = ref partition.Modules[newModule];
        double newBoundaryAfter = CleanNonNegative(newBeforeMove.ModuleExitRate + (preparation.TotalFlow - flowToTargetModule) - flowToTargetModule + preparation.ExternalFlow);
        double newObjectPriorSum = CleanNonNegative(newBeforeMove.ObjectPriorSum + preparation.ObjectPrior);
        double newBoundaryPlogp = Plogp(newBoundaryAfter);
        double newCodebookUsePlogp = Plogp(newObjectPriorSum + newBoundaryAfter);

        double qEnterAfter =
            preparation.TotalModuleEnterRateWithoutSource
            - newBeforeMove.ModuleEnterRate
            + preparation.SourceModuleAfter.ModuleEnterRate
            + newBoundaryAfter;

        double qEnterAfterPlogp = Plogp(qEnterAfter);
        double deltaEnterRateEntropy = qEnterAfterPlogp - partition.TotalEntryRatePlogp;

        double deltaEnterEntropy =
            preparation.SourceEnterEntropyDelta
            - newBeforeMove.ModuleEnterPlogp
            + newBoundaryPlogp;

        double deltaExitEntropy =
            preparation.SourceExitEntropyDelta
            - newBeforeMove.ModuleExitPlogp
            + newBoundaryPlogp;

        double deltaCodebookUseRate =
            preparation.SourceUseRateEntropyDelta
            - newBeforeMove.CodebookUsePlogp
            + newCodebookUsePlogp;

        return deltaEnterRateEntropy
            - deltaEnterEntropy
            - deltaExitEntropy
            + deltaCodebookUseRate;
    }

    /// <summary>
    /// Computes the exact codelength change for a target using a shared source preparation.
    /// </summary>
    internal NodeMoveDelta EvaluateMove(in NodeMovePreparation preparation, int newModule, double flowToTargetModule)
    {
        partition.EnsureInitialized();

        if ((uint)preparation.ObjectId >= (uint)network.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(preparation));
        if ((uint)newModule >= (uint)partition.ModuleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(newModule));

        int oldModule = preparation.SourceModule;
        if (oldModule == newModule)
            return CreateNoOpMove(preparation.ObjectId, oldModule);

        var newBeforeMove = partition.Modules[newModule];
        double newBoundaryAfter = CleanNonNegative(newBeforeMove.ModuleExitRate + (preparation.TotalFlow - flowToTargetModule) - flowToTargetModule + preparation.ExternalFlow);

        var newAfterMove = newBeforeMove;
        newAfterMove.ObjectPriorSum = CleanNonNegative(newBeforeMove.ObjectPriorSum + preparation.ObjectPrior);
        newAfterMove.ModuleEnterRate = newBoundaryAfter;
        newAfterMove.ModuleExitRate = newBoundaryAfter;
        newAfterMove.MemberCount = newBeforeMove.MemberCount + 1;
        RefreshEntropyTerms(ref newAfterMove);

        double qEnterAfter =
            preparation.TotalModuleEnterRateWithoutSource
            - newBeforeMove.ModuleEnterRate
            + preparation.SourceModuleAfter.ModuleEnterRate
            + newAfterMove.ModuleEnterRate;

        double qEnterAfterPlogp = Plogp(qEnterAfter);
        double deltaEnterRateEntropy = qEnterAfterPlogp - partition.TotalEntryRatePlogp;

        double deltaEnterEntropy =
            preparation.SourceEnterEntropyDelta
            - newBeforeMove.ModuleEnterPlogp
            + newAfterMove.ModuleEnterPlogp;

        double deltaExitEntropy =
            preparation.SourceExitEntropyDelta
            - newBeforeMove.ModuleExitPlogp
            + newAfterMove.ModuleExitPlogp;

        double deltaCodebookUseRate =
            preparation.SourceUseRateEntropyDelta
            - newBeforeMove.CodebookUsePlogp
            + newAfterMove.CodebookUsePlogp;

        double deltaCodelength =
            deltaEnterRateEntropy
            - deltaEnterEntropy
            - deltaExitEntropy
            + deltaCodebookUseRate;

        return new NodeMoveDelta(
            preparation.ObjectId,
            oldModule,
            newModule,
            preparation.SourceModuleAfter,
            newAfterMove,
            qEnterAfter,
            qEnterAfterPlogp,
            deltaCodelength);
    }

    /// <summary>
    /// Evaluates one target directly from the object's local flow totals.
    /// </summary>
    internal NodeMoveDelta EvaluateMove(int objectId, int newModule, in NodeMoveStatistics stats)
    {
        partition.EnsureInitialized();

        if ((uint)objectId >= (uint)network.NodeCount)
            throw new ArgumentOutOfRangeException(nameof(objectId));
        if ((uint)newModule >= (uint)partition.ModuleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(newModule));

        int oldModule = partition.Assignments[objectId];
        // No-op ignore the supplied flow.
        if (oldModule == newModule)
            return CreateNoOpMove(objectId, oldModule);

        NodeMovePreparation preparation = PrepareMove(objectId, stats.TotalFlow, stats.FlowToSourceModule);
        return EvaluateMove(in preparation, newModule, stats.FlowToTargetModule);
    }

    private NodeMoveDelta CreateNoOpMove(int objectId, int module)
    {
        return new NodeMoveDelta(
            objectId,
            module,
            module,
            partition.Modules[module],
            partition.Modules[module],
            partition.TotalEntryRate,
            partition.TotalEntryRatePlogp,
            0.0);
    }

    /// <summary>
    /// Reassembles the objective from the already-maintained summary terms.
    /// </summary>
    internal void UpdateCodelength()
    {
        partition.IndexCodelength = partition.TotalEntryRatePlogp - partition.NetworkExitFlowPlogp - partition.ModuleEntryRateEntropy;
        partition.ModuleCodelength = -partition.ModuleExitRateEntropy + partition.ModuleUseRateEntropy - partition.ObjectPriorEntropy;
        partition.Codelength = partition.IndexCodelength + partition.ModuleCodelength;
    }

    /// <summary>
    /// Refreshes the cached entropy terms stored in module.
    /// </summary>
    internal static void RefreshEntropyTerms(ref ModuleState module)
    {
        module.ModuleEnterPlogp = Plogp(module.ModuleEnterRate);
        module.ModuleExitPlogp = module.ModuleEnterRate == module.ModuleExitRate
            ? module.ModuleEnterPlogp
            : Plogp(module.ModuleExitRate);
        module.CodebookUsePlogp = Plogp(module.CodebookUseRate);
    }

    /// <summary>
    /// Returns the map-equation entropy helper
    /// </summary>
    internal static double Plogp(double x)
    {
        return x <= 0.0 ? 0.0 : x * Math.Log(x) * InvLog2;
    }

    /// <summary>
    /// Clamps tiny negative roundoff to zero or throw on invalid flow change.
    /// </summary>
    private static double CleanNonNegative(double x)
    {
        const double eps = 1e-10;
        if (x < -eps)
            throw new InvalidOperationException($"Encountered negative flow quantity: {x}");
        return x < 0.0 ? 0.0 : x;
    }
}
