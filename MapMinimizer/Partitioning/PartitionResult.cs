namespace MapMinimizer;

/// <summary>
/// Result of one flat optimization run on a single active network.
/// </summary>
internal readonly struct PartitionResult
{
    internal readonly int[] Assignments;
    internal readonly int ModuleCount;
    internal readonly double Codelength;
    internal readonly double InitialCodelength;

    internal PartitionResult(
        int[] assignments,
        int moduleCount,
        double codelength,
        double? initialCodelength = null)
    {
        Assignments = assignments;
        ModuleCount = moduleCount;
        Codelength = codelength;
        InitialCodelength = initialCodelength ?? codelength;
    }
}
