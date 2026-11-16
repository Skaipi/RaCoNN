namespace MapMinimizer;

/// <summary>
/// Target-independent state for evaluating moves of one object without repeating source updates.
/// </summary>
/// <remarks>
/// Reuse for all candidate targets while the partition is unchanged. Prepare again after applying
/// any move or reinitializing the equation. Reserving or releasing an empty module is safe.
/// </remarks>
internal readonly struct NodeMovePreparation
{
    internal readonly int ObjectId;
    internal readonly int SourceModule;
    internal readonly double ObjectPrior;
    internal readonly double ExternalFlow;
    internal readonly double TotalFlow;
    internal readonly ModuleState SourceModuleAfter;
    internal readonly double TotalModuleEnterRateWithoutSource;
    internal readonly double SourceEnterEntropyDelta;
    internal readonly double SourceExitEntropyDelta;
    internal readonly double SourceUseRateEntropyDelta;

    internal NodeMovePreparation(
        int objectId,
        int sourceModule,
        double objectPrior,
        double externalFlow,
        double totalFlow,
        in ModuleState sourceBefore,
        in ModuleState sourceAfter,
        double totalModuleEnterRate)
    {
        ObjectId = objectId;
        SourceModule = sourceModule;
        ObjectPrior = objectPrior;
        ExternalFlow = externalFlow;
        TotalFlow = totalFlow;
        SourceModuleAfter = sourceAfter;
        TotalModuleEnterRateWithoutSource = totalModuleEnterRate - sourceBefore.ModuleEnterRate;
        SourceEnterEntropyDelta = -sourceBefore.ModuleEnterPlogp + sourceAfter.ModuleEnterPlogp;
        SourceExitEntropyDelta = -sourceBefore.ModuleExitPlogp + sourceAfter.ModuleExitPlogp;
        SourceUseRateEntropyDelta = -sourceBefore.CodebookUsePlogp + sourceAfter.CodebookUsePlogp;
    }
}
