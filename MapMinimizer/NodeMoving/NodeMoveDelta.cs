using System;

namespace MapMinimizer;

/// <summary>
/// Snapshot of the exact state change induced by moving one object between two modules.
/// </summary>
internal readonly struct NodeMoveDelta
{
    internal readonly int ObjectId;
    internal readonly int SourceModule;
    internal readonly int TargetModule;
    internal readonly ModuleState SourceModuleAfter;
    internal readonly ModuleState TargetModuleAfter;
    internal readonly double TotalModuleEnterRateAfter;
    internal readonly double TotalModuleEnterRateAfterPlogp;
    internal readonly double CodelengthDelta;

    /// <summary>
    /// Indicates that the move keeps the object in its current module.
    /// </summary>
    internal bool IsNoOp => SourceModule == TargetModule;

    /// <summary>
    /// Creates a move delta containing structural change and codelength change.
    /// </summary>
    internal NodeMoveDelta(
        int objectId,
        int sourceModule,
        int targetModule,
        ModuleState sourceModuleAfter,
        ModuleState targetModuleAfter,
        double totalModuleEnterRateAfter,
        double totalModuleEnterRateAfterPlogp,
        double codelengthDelta)
    {
        ObjectId = objectId;
        SourceModule = sourceModule;
        TargetModule = targetModule;
        SourceModuleAfter = sourceModuleAfter;
        TargetModuleAfter = targetModuleAfter;
        TotalModuleEnterRateAfter = totalModuleEnterRateAfter;
        TotalModuleEnterRateAfterPlogp = totalModuleEnterRateAfterPlogp;
        CodelengthDelta = codelengthDelta;
    }
}
