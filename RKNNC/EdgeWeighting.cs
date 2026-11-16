namespace RKNNC;

/// <summary>
/// Edge strengths used by the reciprocal-rank graph.
/// </summary>
public enum EdgeWeighting
{
    /// <summary>Every accepted edge has weight one.</summary>
    Unit = 0,

    /// <summary>Weight by survival frequency across stricter graph-construction limits.</summary>
    RankTaper = 1,
}
