namespace MapMinimizer;

/// <summary>
/// Local edge-flow totals for evaluating one node move without rescanning the neighborhood.
/// </summary>
internal readonly struct NodeMoveStatistics
{
    internal readonly double TotalFlow;
    internal readonly double FlowToSourceModule;
    internal readonly double FlowToTargetModule;

    internal NodeMoveStatistics(
        double totalFlow,
        double flowToSourceModule,
        double flowToTargetModule)
    {
        TotalFlow = totalFlow;
        FlowToSourceModule = flowToSourceModule;
        FlowToTargetModule = flowToTargetModule;
    }
}
