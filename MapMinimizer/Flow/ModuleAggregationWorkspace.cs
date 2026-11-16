using System.Buffers;

namespace MapMinimizer;

/// <summary>
/// Owns pooled scratch buffers and the per-thread edge-flow dictionary for one aggregation.
/// </summary>
internal ref struct ModuleAggregationWorkspace
{
    // Retain at most one small dictionary per thread; large coarsenings must not pin their buffers.
    private const int MaxRetainedEdgeCapacity = 4096;

    [ThreadStatic]
    private static Dictionary<long, double>? cachedEdgeFlowByPair;

    private readonly int moduleCount;
    private readonly Dictionary<long, double> edgeFlowByPair;
    private readonly ModuleSummary[]? summaries;
    private readonly double[]? externalFlowByModule;
    private int[]? degree;
    private int[]? cursor;

    internal readonly ModuleSummary[] Summaries => summaries!;
    internal readonly double[] ExternalFlowByModule => externalFlowByModule!;
    internal readonly Dictionary<long, double> EdgeFlowByPair => edgeFlowByPair;

    internal ModuleAggregationWorkspace(int nodeCount, int moduleCount)
    {
        this.moduleCount = moduleCount;
        summaries = null;
        externalFlowByModule = null;
        degree = null;
        cursor = null;
        edgeFlowByPair = RentEdgeFlowByPair(Math.Max(1, nodeCount));
        try
        {
            summaries = ArrayPool<ModuleSummary>.Shared.Rent(moduleCount);
            Array.Clear(summaries, 0, moduleCount);
            externalFlowByModule = ArrayPool<double>.Shared.Rent(moduleCount);
            Array.Clear(externalFlowByModule, 0, moduleCount);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Rents zeroed degree counts for the logical module range.
    /// </summary>
    internal int[] RentDegree()
    {
        degree = ArrayPool<int>.Shared.Rent(moduleCount);
        Array.Clear(degree, 0, moduleCount);
        return degree;
    }

    /// <summary>
    /// Rents mutable cursors initialized from the module offsets.
    /// </summary>
    internal int[] RentCursor(int[] edgeOffsets)
    {
        cursor = ArrayPool<int>.Shared.Rent(moduleCount);
        Array.Copy(edgeOffsets, cursor, moduleCount);
        return cursor;
    }

    internal readonly void Dispose()
    {
        // Match the nested edge-builder and aggregation cleanup, including exceptional exits.
        try
        {
            ReturnEdgeBuffers();
        }
        finally
        {
            ReturnModuleBuffers();
            ReturnEdgeFlowByPair(edgeFlowByPair);
        }
    }

    private readonly void ReturnEdgeBuffers()
    {
        if (degree != null)
        {
            Array.Clear(degree, 0, moduleCount);
            ArrayPool<int>.Shared.Return(degree);
        }
        if (cursor != null)
        {
            Array.Clear(cursor, 0, moduleCount);
            ArrayPool<int>.Shared.Return(cursor);
        }
    }

    private readonly void ReturnModuleBuffers()
    {
        if (summaries != null)
        {
            Array.Clear(summaries, 0, moduleCount);
            ArrayPool<ModuleSummary>.Shared.Return(summaries);
        }
        if (externalFlowByModule != null)
        {
            Array.Clear(externalFlowByModule, 0, moduleCount);
            ArrayPool<double>.Shared.Return(externalFlowByModule);
        }
    }

    private static Dictionary<long, double> RentEdgeFlowByPair(int capacity)
    {
        Dictionary<long, double>? dictionary = cachedEdgeFlowByPair;
        cachedEdgeFlowByPair = null;
        if (dictionary == null)
            return new Dictionary<long, double>(capacity);

        dictionary.EnsureCapacity(capacity);
        return dictionary;
    }

    private static void ReturnEdgeFlowByPair(Dictionary<long, double> dictionary)
    {
        if (dictionary.EnsureCapacity(0) > MaxRetainedEdgeCapacity)
            return;

        // Clear resets insertion slots as well as keys, preserving fresh-dictionary iteration order.
        dictionary.Clear();
        cachedEdgeFlowByPair = dictionary;
    }
}
