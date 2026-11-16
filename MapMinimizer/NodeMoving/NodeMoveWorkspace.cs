using System.Buffers;

namespace MapMinimizer;

/// <summary>
/// Candidate-node worklist and stamped neighborhood-flow buffers for one move optimizer.
/// Owned in-place by NodeMoveOptimizer; do not copy it after construction.
/// Internal queue is realised by circular buffer of nodes.
/// </summary>
internal struct NodeMoveWorkspace : IDisposable
{
    private readonly FlowNetwork network;
    private readonly PartitionState partition;
    private readonly Random rng;
    private readonly int[] visitOrder;
    private readonly bool[] isNodeQueued;
    private int[] moduleStamp;
    private readonly int[] candidateModules;
    private double[] flowByModule;
    private readonly int[] activeNodeQueue;
    private int queueHead, queueTail, queueCount;
    private bool disposed;
    private int stampToken;

    internal int QueuedNodeCount => queueCount;
    internal int[] CandidateModules => candidateModules;

    internal NodeMoveWorkspace(PartitionState partition, int seed)
    {
        this.partition = partition;
        network = partition.Network;
        rng = new Random(seed);

        visitOrder = ArrayPool<int>.Shared.Rent(Math.Max(1, network.NodeCount));
        isNodeQueued = ArrayPool<bool>.Shared.Rent(Math.Max(1, network.NodeCount));
        moduleStamp = ArrayPool<int>.Shared.Rent(Math.Max(1, network.NodeCount));
        Array.Clear(moduleStamp);

        candidateModules = ArrayPool<int>.Shared.Rent(Math.Max(1, network.NodeCount + 1));
        flowByModule = ArrayPool<double>.Shared.Rent(Math.Max(1, network.NodeCount));
        activeNodeQueue = ArrayPool<int>.Shared.Rent(Math.Max(1, network.NodeCount));
    }

    /// <summary>
    /// Restarts node visitation and the active worklist for a new optimization.
    /// </summary>
    internal void Reset()
    {
        for (int i = 0; i < network.NodeCount; i++)
            visitOrder[i] = i;

        queueHead = queueTail = queueCount = 0;
        Array.Clear(isNodeQueued, 0, network.NodeCount);
    }

    /// <summary>
    /// Collects distinct neighboring modules and accumulates the edge flow touching each of them.
    /// </summary>
    internal int ScanCandidateModules(int objectId, out double totalFlow, out int stamp)
    {
        EnsureModuleWorkspaceCapacity(partition.ModuleSlotCount);

        int count = 0;
        stamp = NextStamp();
        totalFlow = 0.0;
        int[] moduleOfObject = partition.Assignments;

        ReadOnlySpan<EdgeData> edges = network.GetEdges(objectId);
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            EdgeData edge = edges[edgeIndex];
            if (edge.Target == objectId) continue;

            totalFlow += edge.Flow;
            count = AccumulateCandidateFlow(moduleOfObject[edge.Target], edge.Flow, stamp, count);
        }

        return count;
    }

    /// <summary>
    /// Records one module-level flow contribution for the current node scan.
    /// </summary>
    private int AccumulateCandidateFlow(int module, double flow, int stamp, int count)
    {
        if (moduleStamp[module] != stamp)
        {
            moduleStamp[module] = stamp;
            flowByModule[module] = 0.0;
            candidateModules[count++] = module;
        }

        flowByModule[module] += flow;
        return count;
    }

    /// <summary>
    /// Returns the flow accumulated for one module during the current node scan.
    /// </summary>
    internal double GetScannedModuleFlow(int module, int stamp)
    {
        return (uint)module < (uint)moduleStamp.Length && moduleStamp[module] == stamp
            ? flowByModule[module]
            : 0.0;
    }

    /// <summary>
    /// Initializes the list of nodes to be processed with every node in randomized order.
    /// </summary>
    internal void SeedAllNodes()
    {
        Shuffle(visitOrder);
        for (int i = 0; i < network.NodeCount; i++)
            EnqueueNode(visitOrder[i]);
    }

    /// <summary>
    /// Reactivates the nodes whose local move scores changed after one accepted move.
    /// </summary>
    internal void EnqueueAffectedNodes(NodeMoveDelta move)
    {
        EnqueueNode(move.ObjectId);
        EnqueueAdjacentNodes(move.ObjectId);
    }

    /// <summary>
    /// Enqueues the graph neighbors of one node.
    /// </summary>
    private void EnqueueAdjacentNodes(int objectId)
    {
        ReadOnlySpan<EdgeData> edges = network.GetEdges(objectId);
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            EdgeData edge = edges[edgeIndex];
            if (edge.Target != objectId) EnqueueNode(edge.Target);
        }
    }

    /// <summary>
    /// Adds a node to the list of nodes that need processing if it is not already scheduled.
    /// </summary>
    private void EnqueueNode(int objectId)
    {
        if (isNodeQueued[objectId]) return;

        activeNodeQueue[queueTail] = objectId;
        if (++queueTail == activeNodeQueue.Length) queueTail = 0;
        queueCount++;
        isNodeQueued[objectId] = true;
    }

    /// <summary>
    /// Removes the next node from the worklist.
    /// </summary>
    internal bool TryDequeueNode(out int objectId)
    {
        if (queueCount == 0)
        {
            objectId = -1;
            return false;
        }

        objectId = activeNodeQueue[queueHead];
        if (++queueHead == activeNodeQueue.Length) queueHead = 0;
        queueCount--;
        isNodeQueued[objectId] = false;
        return true;
    }

    /// <summary>
    /// Ensures the per-module scratch buffers are large enough for the current sparse module ids.
    /// </summary>
    internal void EnsureModuleWorkspaceCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= moduleStamp.Length)
            return;

        int newCapacity = Math.Max(requiredCapacity, moduleStamp.Length * 2);
        var newStamps = ArrayPool<int>.Shared.Rent(newCapacity);
        Array.Clear(newStamps);
        Array.Copy(moduleStamp, newStamps, moduleStamp.Length);
        var newFlows = ArrayPool<double>.Shared.Rent(newStamps.Length);
        Array.Copy(flowByModule, newFlows, flowByModule.Length);
        ArrayPool<int>.Shared.Return(moduleStamp);
        ArrayPool<double>.Shared.Return(flowByModule);
        moduleStamp = newStamps;
        flowByModule = newFlows;
    }

    /// <summary>
    /// Returns a new visitation stamp used to deduplicate candidate modules without clearing the whole array.
    /// </summary>
    private int NextStamp()
    {
        stampToken++;
        if (stampToken != int.MaxValue)
            return stampToken;

        Array.Clear(moduleStamp, 0, moduleStamp.Length);
        stampToken = 1;
        return stampToken;
    }

    /// <summary>
    /// Randomizes node visitation order so repeated trials do not follow the same deterministic descent path.
    /// </summary>
    private void Shuffle(int[] values)
    {
        for (int i = network.NodeCount - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ArrayPool<int>.Shared.Return(visitOrder);
        ArrayPool<bool>.Shared.Return(isNodeQueued);
        ArrayPool<int>.Shared.Return(moduleStamp);
        ArrayPool<int>.Shared.Return(candidateModules);
        ArrayPool<double>.Shared.Return(flowByModule);
        ArrayPool<int>.Shared.Return(activeNodeQueue);
    }
}
