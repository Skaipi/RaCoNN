namespace MapMinimizer;

/// <summary>
/// Aggregated flow quantities for one module under the two-level map equation.
/// </summary>
/// <remarks>
/// The delta engine updates only source and target modules for a candidate move, so keeping these
/// quantities together makes the move formulas local and cheap to evaluate.
/// </remarks>
internal struct ModuleState
{
    internal double ObjectPriorSum;
    internal double ModuleEnterRate;
    internal double ModuleExitRate;
    internal double ModuleEnterPlogp;
    internal double ModuleExitPlogp;
    internal double CodebookUsePlogp;
    internal int MemberCount;

    /// <summary>
    /// Total probability mass encoded by the module codebook: staying on its objects plus using its exit codeword.
    /// </summary>
    internal double CodebookUseRate => ObjectPriorSum + ModuleExitRate;
}
