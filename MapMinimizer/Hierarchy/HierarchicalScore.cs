namespace MapMinimizer;

/// <summary>
/// Decomposed hierarchical map-equation score for one hierarchy tree.
/// </summary>
internal class HierarchicalScore
{
    internal double TotalCodelength { get; }
    internal double IndexCodelength { get; }
    internal double ModuleCodelength { get; }

    internal HierarchicalScore(double totalCodelength, double indexCodelength, double moduleCodelength)
    {
        TotalCodelength = totalCodelength;
        IndexCodelength = indexCodelength;
        ModuleCodelength = moduleCodelength;
    }
}
