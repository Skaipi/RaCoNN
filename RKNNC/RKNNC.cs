using MapMinimizer;

namespace RKNNC;

public class RKNNC
{
    private readonly int minPoints;
    private readonly UndirectedGraph graph;

    public RKNNC(double[][] data, int k, int t = 2, int minPoints = 20)
        : this(data, k, EdgeWeighting.Unit, t, minPoints)
    {
    }

    public RKNNC(double[][] data, int k, EdgeWeighting weighting, int t = 2, int minPoints = 20)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));
        if (t < 0) throw new ArgumentOutOfRangeException(nameof(t));
        if (minPoints <= 0) throw new ArgumentOutOfRangeException(nameof(minPoints));
        if (!Enum.IsDefined(weighting)) throw new ArgumentOutOfRangeException(nameof(weighting));

        this.minPoints = minPoints;

        var neighborSearch = new NeighborSearch(data);
        var graphBuilder = new GraphBuilder(neighborSearch);
        graph = graphBuilder.Build(k, t, weighting);
    }

    /// <summary>
    /// Runs map-equation optimization and labels clusters smaller than minPoints as noise.
    /// </summary>
    public int[] Cluster()
    {
        var optimizer = new MapEquationOptimizer();
        var assignments = optimizer.Optimize(graph).GetCoarsestAssignments();
        return ClusterFilter.Apply(assignments, minPoints);
    }

    /// <summary>
    /// Compatibility entry point for <see cref="Cluster"/>.
    /// </summary>
    public int[] GetClusterAssignments() => Cluster();
}
