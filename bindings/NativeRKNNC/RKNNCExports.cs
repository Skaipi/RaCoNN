using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NativeRKNNC;

public static unsafe class RKNNCExport
{
    [UnmanagedCallersOnly(EntryPoint = "cluster", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Cluster(double* data, int* outLabels, int n, int dim, int k, int t, int minPoints)
        => ClusterCore(data, outLabels, n, dim, k, t, minPoints, RKNNC.EdgeWeighting.Unit);

    [UnmanagedCallersOnly(EntryPoint = "cluster_weighted", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ClusterWeighted(double* data, int* outLabels, int n, int dim, int k, int t, int minPoints, int weighting)
        => ClusterCore(data, outLabels, n, dim, k, t, minPoints, (RKNNC.EdgeWeighting)weighting);

    private static int ClusterCore(double* data, int* outLabels, int n, int dim, int k, int t, int minPoints,
        RKNNC.EdgeWeighting weighting)
    {
        if (data == null || outLabels == null || n < 0 || dim < 0 || k <= 0 || t < 0 || minPoints <= 0)
            return -1;
        if ((long)n * dim > int.MaxValue)
            return -1;
        if (!Enum.IsDefined(weighting))
            return -1;

        try
        {
            double[][] managedData = new double[n][];
            for (int i = 0; i < n; i++)
            {
                managedData[i] = new double[dim];
                for (int j = 0; j < dim; j++)
                    managedData[i][j] = data[i * dim + j];
            }

            var rknnc = new RKNNC.RKNNC(managedData, k, weighting, t, minPoints);
            int[] assignments = rknnc.Cluster();
            for (int i = 0; i < n; i++)
                outLabels[i] = assignments[i];
            return 0;
        }
        catch
        {
            return -2;
        }
    }
}
