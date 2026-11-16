namespace MapMinimizer;

/// <summary>
/// Immutable weighted undirected graph consumed by the map-equation optimizer.
/// </summary>
public sealed class UndirectedGraph
{
    private readonly WeightedEdge[] edges;

    public int NodeCount { get; }

    internal ReadOnlySpan<WeightedEdge> Edges => edges;

    public UndirectedGraph(int nodeCount, WeightedEdge[] edges)
    {
        if (nodeCount < 0) throw new ArgumentOutOfRangeException(nameof(nodeCount));
        if (edges == null) throw new ArgumentNullException(nameof(edges));

        NodeCount = nodeCount;
        this.edges = (WeightedEdge[])edges.Clone();

        for (int edgeIndex = 0; edgeIndex < this.edges.Length; edgeIndex++)
        {
            WeightedEdge edge = this.edges[edgeIndex];
            if ((uint)edge.Source >= (uint)nodeCount)
                throw new ArgumentOutOfRangeException(nameof(edges), $"Edge {edgeIndex}: source {edge.Source} is out of range.");
            if ((uint)edge.Target >= (uint)nodeCount)
                throw new ArgumentOutOfRangeException(nameof(edges), $"Edge {edgeIndex}: target {edge.Target} is out of range.");
            if (!double.IsFinite(edge.Weight) || edge.Weight < 0.0)
                throw new ArgumentOutOfRangeException(nameof(edges), $"Edge {edgeIndex}: weight must be finite and non-negative.");
        }
    }
}
