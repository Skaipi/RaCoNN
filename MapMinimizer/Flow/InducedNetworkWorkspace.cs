namespace MapMinimizer;

/// <summary>
/// Reuses membership stamps, local node ids and degree buffers during network induction.
/// </summary>
internal sealed class InducedNetworkWorkspace
{
    private int stampToken;
    private int[] nodeStamp;
    private int[] localIndexByNode;
    private int[] outDegreeBuffer;

    internal int[] NodeStamp => nodeStamp;
    internal int[] LocalIndexByNode => localIndexByNode;
    internal InducedNetworkWorkspace(int nodeCapacity)
    {
        int initialNodeCapacity = Math.Max(1, nodeCapacity);
        nodeStamp = new int[initialNodeCapacity];
        localIndexByNode = new int[initialNodeCapacity];
        outDegreeBuffer = Array.Empty<int>();
    }

    /// <summary>
    /// Returns a fresh membership stamp while reusing the same marker array across refinement calls.
    /// </summary>
    internal int NextNodeStamp(int requiredNodeCapacity)
    {
        EnsureNodeCapacity(requiredNodeCapacity);

        stampToken++;
        if (stampToken != int.MaxValue)
            return stampToken;

        Array.Clear(nodeStamp, 0, nodeStamp.Length);
        stampToken = 1;
        return stampToken;
    }

    /// <summary>
    /// Returns a zeroed degree buffer large enough for one induced subnetwork build.
    /// </summary>
    internal int[] RentOutDegreeBuffer(int requiredLength)
    {
        if (outDegreeBuffer.Length < requiredLength)
            Array.Resize(ref outDegreeBuffer, Math.Max(requiredLength, outDegreeBuffer.Length * 2));

        Array.Clear(outDegreeBuffer, 0, requiredLength);
        return outDegreeBuffer;
    }

    /// <summary>
    /// Ensures the node-indexed scratch arrays can address the requested graph size.
    /// </summary>
    private void EnsureNodeCapacity(int requiredNodeCapacity)
    {
        if (requiredNodeCapacity <= nodeStamp.Length) return;

        int newCapacity = Math.Max(requiredNodeCapacity, nodeStamp.Length * 2);
        Array.Resize(ref nodeStamp, newCapacity);
        Array.Resize(ref localIndexByNode, newCapacity);
    }
}
