namespace MapMinimizer;

/// <summary>
/// Flow data for one module in a partition.
/// </summary>
internal struct ModuleSummary
{
    /// <summary>
    /// total flow of everything inside that module
    /// </summary>
    internal double Flow;
    /// <summary>
    /// rate at which the module is entered from outside
    /// </summary>
    internal double Prior;
    internal double EnterFlow;
    internal double ExitFlow;
}
