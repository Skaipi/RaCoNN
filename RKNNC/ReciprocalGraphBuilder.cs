using MapMinimizer;

namespace RKNNC;

/// <summary>
/// Connects reciprocal neighbors whose ranks differ by at most the threshold.
/// </summary>
internal class GraphBuilder
{
    private readonly NeighborSearch neighborSearch;

    internal GraphBuilder(NeighborSearch neighborSearch)
    {
        this.neighborSearch = neighborSearch;
    }

    internal UndirectedGraph Build(int k, int t, EdgeWeighting weighting = EdgeWeighting.Unit)
    {
        int n = neighborSearch.Count;
        if (n == 0)
            return new UndirectedGraph(0, []);

        int neighborCount = Math.Min(k, n - 1);
        var edges = new List<WeightedEdge>(checked(n * neighborCount));
        var results = neighborSearch.FindNeighbors(neighborCount);
        var rankByNeighbor = BuildNeighborRanks(results);
        ConnectRankedNeighbors(results, rankByNeighbor, neighborCount, t, weighting, edges);

        return new UndirectedGraph(n, edges.ToArray());
    }

    private static Dictionary<int, int>[] BuildNeighborRanks(int[][] results)
    {
        int n = results.Length;
        var rankByNeighbor = new Dictionary<int, int>[n];
        for (int u = 0; u < n; u++)
        {
            var neighbors = results[u];
            var ranks = new Dictionary<int, int>(neighbors.Length);
            for (int j = 0; j < neighbors.Length; j++)
                ranks[neighbors[j]] = j;
            rankByNeighbor[u] = ranks;
        }

        return rankByNeighbor;
    }

    private static void ConnectRankedNeighbors(
        int[][] results, Dictionary<int, int>[] rankByNeighbor, int k, int t,
        EdgeWeighting weighting, List<WeightedEdge> edges)
    {
        int n = results.Length;
        for (int u = 0; u < n; u++)
        {
            var uNeighbors = results[u];
            for (int j = 0; j < uNeighbors.Length; j++)
            {
                int v = uNeighbors[j];
                if (!rankByNeighbor[v].TryGetValue(u, out int rankVU))
                    continue;

                var diff = Math.Abs(j - rankVU);
                if (diff <= t)
                {
                    // Store one orientation only; the optimizer materializes the reverse edge.
                    if (u < v)
                        continue;

                    // Fraction of stricter (neighborhood size, rank tolerance) graphs
                    // retaining this edge. Neighbor ranks j and rankVU are zero-based.
                    double weight = weighting == EdgeWeighting.RankTaper
                        ? (k - Math.Max(j, rankVU)) / (double)k
                            * (((double)t + 1.0 - diff) / ((double)t + 1.0))
                        : 1.0;
                    edges.Add(new WeightedEdge(u, v, weight));
                }
            }
        }
    }
}
