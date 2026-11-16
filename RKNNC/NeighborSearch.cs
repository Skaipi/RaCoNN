using HNSWIndex;
using Supercluster.KDTree;

namespace RKNNC;

/// <summary>
/// Finds neighbors in search order, using original data indices as point IDs.
/// </summary>
internal sealed class NeighborSearch
{
    private readonly double[][] data;
    private readonly int dimensions;

    internal int Count => data.Length;

    internal NeighborSearch(double[][] data)
    {
        this.data = data;
        if (data.Length == 0)
            return;

        if (data[0] == null) throw new ArgumentException("Data rows cannot be null.", nameof(data));
        dimensions = data[0].Length;
        if (dimensions == 0) throw new ArgumentException("Data rows must contain at least one coordinate.", nameof(data));
        for (int i = 1; i < data.Length; i++)
        {
            if (data[i] == null || data[i].Length != dimensions)
                throw new ArgumentException("Data rows must be non-null and have equal dimensions.", nameof(data));
        }
    }

    internal int[][] FindNeighbors(int neighborCount)
    {
        return dimensions <= 2
            ? FindKDTreeNeighbors(neighborCount)
            : FindHNSWNeighbors(neighborCount);
    }

    private int[][] FindKDTreeNeighbors(int neighborCount)
    {
        var tree = new KDTree<double, int>(
            dimensions,
            data,
            Enumerable.Range(0, data.Length).ToArray(),
            L2,
            double.MinValue, // avoid reflections inside KDTree
            double.MaxValue // avoid reflections inside KDTree
        );
        var results = new int[data.Length][];
        for (int i = 0; i < data.Length; i++)
        {
            var point = data[i];
            // Equal coordinates can put another point before the query point.
            results[i] = tree.NearestNeighbors(point, neighborCount + 1)
                .Where(candidate => candidate.Item2 != i)
                .Take(neighborCount)
                .Select(candidate => candidate.Item2)
                .ToArray();
        }
        return results;
    }

    private int[][] FindHNSWNeighbors(int neighborCount)
    {
        var parameters = new HNSWParameters<double> { CollectionSize = data.Length, MinNN = 50, MaxCandidates = 200 };
        var index = new HNSWIndex<double[], double>(L2, parameters);
        // Single-threaded insertion keeps IDs in original data order.
        for (int i = 0; i < data.Length; i++)
        {
            var point = data[i];
            index.Add(point);
        }
        var results = new int[data.Length][];
        for (int i = 0; i < data.Length; i++)
        {
            var point = data[i];
            results[i] = index.KnnQuery(point, neighborCount + 1)
                .Where(candidate => candidate.Id != i)
                .Take(neighborCount)
                .Select(candidate => candidate.Id)
                .ToArray();
        }
        return results;
    }

    private static double L2(double[] a, double[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
}
