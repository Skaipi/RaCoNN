namespace MapMinimizer;

/// <summary>
/// Node in the flow network
/// </summary>
internal struct NodeData
{
    internal double VisitRate;
    internal double Prior; // Mass encoded for this object by its module codebook.
    internal double ExternalFlow; // Symmetric flow to objects outside the current network scope.
    internal double EnterFlow;
    internal double ExitFlow;
}
