namespace MapMinimizer;

/// <summary>
/// Runs independent hierarchy trials and returns the lowest-codelength result.
/// </summary>
internal sealed class HierarchyTrialRunner
{
    private readonly MapEquationParameters parameters;
    private readonly SupermoduleSearch supermoduleSearch;
    private readonly SubmoduleSearch submoduleSearch;

    internal HierarchyTrialRunner(MapEquationParameters parameters)
    {
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        supermoduleSearch = new SupermoduleSearch(parameters);
        submoduleSearch = new SubmoduleSearch(parameters, supermoduleSearch);
    }

    /// <summary>
    /// Run all optimization trials in parallel.
    /// </summary>
    internal TrialResult Run(FlowNetwork baseNetwork)
    {
        int[] trialSeeds = CreateTrialSeeds();
        TrialResult? best = null;
        int bestTrial = int.MaxValue;
        object bestLock = new();

        void RunAndCompare(int trial)
        {
            TrialResult result = RunTrial(baseNetwork, trialSeeds[trial]);
            lock (bestLock)
            {
                if (best == null || result.Score.TotalCodelength < best.Value.Score.TotalCodelength)
                {
                    best = result;
                    bestTrial = trial;
                }
            }
        }

        if (parameters.Trials == 1) RunAndCompare(0);
        else Parallel.For(0, parameters.Trials, RunAndCompare);

        return best ?? throw new InvalidOperationException("No optimization trial produced a result.");
    }

    /// <summary>
    /// Run single optimization trial.
    /// </summary>
    private TrialResult RunTrial(FlowNetwork baseNetwork, int trialSeed)
    {
        var trialRandom = new Random(trialSeed);
        ModuleHierarchy hierarchy = supermoduleSearch.FindHierarchy(baseNetwork, parameters.MaxLevels, trialRandom);

        if (parameters.MaxRefinementDepth > 0)
            submoduleSearch.RefineHierarchy(baseNetwork, hierarchy, parameters.MaxRefinementDepth, trialRandom);

        HierarchicalScore score = HierarchicalScorer.Evaluate(hierarchy.Root);
        return new TrialResult(hierarchy, score);
    }

    private int[] CreateTrialSeeds()
    {
        var seeds = new int[parameters.Trials];
        var random = parameters.RandomSeed.HasValue
            ? new Random(parameters.RandomSeed.Value)
            : new Random();

        for (int trial = 0; trial < seeds.Length; trial++)
            seeds[trial] = random.Next();
        return seeds;
    }

    internal readonly struct TrialResult
    {
        internal readonly ModuleHierarchy Hierarchy;
        internal readonly HierarchicalScore Score;

        internal TrialResult(ModuleHierarchy hierarchy, HierarchicalScore score)
        {
            Hierarchy = hierarchy;
            Score = score;
        }
    }
}
