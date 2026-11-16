namespace MapMinimizer;

/// <summary>
/// Parameters controlling hierarchical map-equation optimization.
/// </summary>
public sealed class MapEquationParameters
{
    /// <summary>
    /// Number of independent trials used to find best solution
    /// </summary>
    public int Trials { get; set; } = 20;
    /// <summary>
    /// Deepest level of multilevel merging phase.
    /// </summary>
    public int MaxLevels { get; set; } = 64;
    /// <summary>
    /// Deepest level of multilevel recursive partitioning phase.
    /// </summary>
    public int MaxRefinementDepth { get; set; } = 16;
    /// <summary>
    /// Maximum complete node-moving sweeps per active network, before aggregation.
    /// </summary>
    public int MaxPasses { get; set; } = 10;
    /// <summary>
    /// Maximum number of alternating fine/coarse tuning steps in each flat partition search.
    /// </summary>
    public int TuneIterationLimit { get; set; } = 10;
    /// <summary>
    /// Minimum relative gain needed to continue another fine/coarse tuning cycle.
    /// Set to zero to continue until only the absolute delta tolerance remains.
    /// </summary>
    public double TuneRelativeTolerance { get; set; } = 1e-5;
    public double DeltaTolerance { get; set; } = 1e-12;
    /// <summary>
    /// Optional seed used for random generators across the library
    /// </summary>
    public int? RandomSeed { get; set; }

    internal MapEquationParameters Snapshot()
    {
        Validate();
        return new MapEquationParameters
        {
            Trials = Trials,
            MaxLevels = MaxLevels,
            MaxRefinementDepth = MaxRefinementDepth,
            MaxPasses = MaxPasses,
            TuneIterationLimit = TuneIterationLimit,
            TuneRelativeTolerance = TuneRelativeTolerance,
            DeltaTolerance = DeltaTolerance,
            RandomSeed = RandomSeed,
        };
    }

    private void Validate()
    {
        if (Trials <= 0) throw new ArgumentOutOfRangeException(nameof(Trials));
        if (MaxLevels <= 0) throw new ArgumentOutOfRangeException(nameof(MaxLevels));
        if (MaxRefinementDepth < 0) throw new ArgumentOutOfRangeException(nameof(MaxRefinementDepth));
        if (MaxPasses <= 0) throw new ArgumentOutOfRangeException(nameof(MaxPasses));
        if (TuneIterationLimit <= 0) throw new ArgumentOutOfRangeException(nameof(TuneIterationLimit));
        if (!double.IsFinite(TuneRelativeTolerance) || TuneRelativeTolerance < 0.0)
            throw new ArgumentOutOfRangeException(nameof(TuneRelativeTolerance));
        if (!double.IsFinite(DeltaTolerance) || DeltaTolerance < 0.0)
            throw new ArgumentOutOfRangeException(nameof(DeltaTolerance));
    }
}
