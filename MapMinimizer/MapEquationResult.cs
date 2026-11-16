namespace MapMinimizer;

/// <summary>
/// Hierarchical map-equation result and its module assignments.
/// </summary>
public sealed class MapEquationResult
{
    private readonly int[][] assignments;

    public int LevelCount => assignments.Length;
    public double Codelength { get; }
    public double IndexCodelength { get; }
    public double ModuleCodelength { get; }

    internal MapEquationResult(int[][] assignments, HierarchicalScore score)
    {
        if (assignments == null) throw new ArgumentNullException(nameof(assignments));
        if (assignments.Length == 0) throw new ArgumentException("Hierarchy must contain at least one level.", nameof(assignments));

        this.assignments = assignments;
        Codelength = score?.TotalCodelength ?? throw new ArgumentNullException(nameof(score));
        IndexCodelength = score.IndexCodelength;
        ModuleCodelength = score.ModuleCodelength;
    }

    /// <summary>
    /// Returns assignments at a zero-based module level, where zero is the coarsest partition.
    /// </summary>
    public int[] GetAssignments(int level = 0)
    {
        if ((uint)level >= (uint)assignments.Length)
            throw new ArgumentOutOfRangeException(nameof(level));
        return (int[])assignments[level].Clone();
    }

    /// <summary>
    /// Returns assignments at the finest meaningful module level.
    /// </summary>
    public int[] GetFinestAssignments() => (int[])assignments[^1].Clone();

    /// <summary>
    /// Returns assignments at the coarsest meaningful module level.
    /// </summary>
    public int[] GetCoarsestAssignments() => GetAssignments();
}
