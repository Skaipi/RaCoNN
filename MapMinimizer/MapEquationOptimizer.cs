namespace MapMinimizer;

/// <summary>
/// Public entry point for running hierarchical map-equation optimization.
/// </summary>
public sealed class MapEquationOptimizer
{
    private readonly MapEquationParameters parameters;

    public MapEquationOptimizer(MapEquationParameters? parameters = null)
    {
        this.parameters = (parameters ?? new MapEquationParameters()).Snapshot();
    }

    /// <summary>
    /// Computes the best hierarchy found for the weighted graph.
    /// </summary>
    public MapEquationResult Optimize(UndirectedGraph graph)
    {
        if (graph == null) throw new ArgumentNullException(nameof(graph));
        var flow = FlowNetworkBuilder.Build(graph);
        var best = new HierarchyTrialRunner(parameters).Run(flow);
        return new MapEquationResult(best.Hierarchy.GetAssignments(graph.NodeCount), best.Score);
    }
}
