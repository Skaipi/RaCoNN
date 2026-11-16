namespace MapMinimizer;

/// <summary>
/// Weighted undirected edge between two graph nodes.
/// </summary>
public readonly struct WeightedEdge
{
    public int Source { get; }
    public int Target { get; }
    public double Weight { get; }

    public WeightedEdge(int source, int target, double weight)
    {
        Source = source;
        Target = target;
        Weight = weight;
    }
}
