namespace MapMinimizer;

/// <summary>
/// Builds normalized flow networks from undirected weighted graphs.
/// </summary>
internal static class FlowNetworkBuilder
{
    /// <summary>
    /// Converts an undirected weighted graph into normalized bidirectional flow.
    /// </summary>
    internal static FlowNetwork Build(UndirectedGraph graph)
    {
        if (graph == null) throw new ArgumentNullException(nameof(graph));

        int nodeCount = graph.NodeCount;
        var nodes = new NodeData[nodeCount];
        var nodeStrength = new double[nodeCount];
        var nodeDegree = new int[nodeCount];
        ReadOnlySpan<WeightedEdge> graphEdges = graph.Edges;

        double totalFlow = MeasureStrengthsAndDegrees(graphEdges, nodeStrength, nodeDegree, out bool hasSelfLoops);
        if (!double.IsFinite(totalFlow))
            throw new ArgumentException("The sum of graph edge weights must be finite.", nameof(graph));

        int[] edgeOffsets = CsrLayout.BuildOffsets(nodeDegree);
        var edges = new EdgeData[edgeOffsets[nodeCount]];

        if (nodeCount == 0) return new FlowNetwork(nodes, edges, edgeOffsets);
        if (totalFlow <= 0.0) return UniformNetwork(nodes, edges, edgeOffsets);

        AssignVisitFlow(nodes, nodeStrength, totalFlow);
        ConnectCsrEdges(graphEdges, totalFlow, edges, edgeOffsets);
        ComputeNodeEnterExit(nodes, edges, edgeOffsets);
        return new FlowNetwork(nodes, edges, edgeOffsets, hasAtomicNodeFlow: !hasSelfLoops);
    }

    private static double MeasureStrengthsAndDegrees(
        ReadOnlySpan<WeightedEdge> graphEdges, double[] nodeStrength, int[] nodeDegree, out bool hasSelfLoops)
    {
        double totalFlow = 0.0;
        hasSelfLoops = false;

        for (int edgeIndex = 0; edgeIndex < graphEdges.Length; edgeIndex++)
        {
            WeightedEdge edge = graphEdges[edgeIndex];
            if (edge.Weight == 0.0)
                continue;

            nodeStrength[edge.Source] += edge.Weight;
            nodeDegree[edge.Source]++;

            if (edge.Source == edge.Target)
            {
                hasSelfLoops = true;
                totalFlow += edge.Weight;
                continue;
            }

            nodeStrength[edge.Target] += edge.Weight;
            nodeDegree[edge.Target]++;
            totalFlow += 2.0 * edge.Weight;
        }

        return totalFlow;
    }

    private static void AssignVisitFlow(NodeData[] nodes, double[] nodeStrength, double totalFlow)
    {
        for (int nodeId = 0; nodeId < nodes.Length; nodeId++)
        {
            double prior = nodeStrength[nodeId] / totalFlow;
            nodes[nodeId].VisitRate = prior;
            nodes[nodeId].Prior = prior;
        }
    }

    private static void ConnectCsrEdges(
        ReadOnlySpan<WeightedEdge> graphEdges, double totalFlow, EdgeData[] edges, int[] edgeOffsets)
    {
        int[] cursor = CsrLayout.CreateCursor(edgeOffsets);
        for (int edgeIndex = 0; edgeIndex < graphEdges.Length; edgeIndex++)
        {
            WeightedEdge graphEdge = graphEdges[edgeIndex];
            if (graphEdge.Weight <= 0.0)
                continue;

            double flow = graphEdge.Weight / totalFlow;
            edges[cursor[graphEdge.Source]++] = new EdgeData
            {
                Target = graphEdge.Target,
                Flow = flow,
            };

            if (graphEdge.Source == graphEdge.Target)
                continue;

            edges[cursor[graphEdge.Target]++] = new EdgeData
            {
                Target = graphEdge.Source,
                Flow = flow,
            };
        }
    }

    /// <summary>
    /// Generate network with uniformal flow on each edge
    /// </summary>
    private static FlowNetwork UniformNetwork(NodeData[] nodes, EdgeData[] edges, int[] edgeOffsets)
    {
        int nodeCount = nodes.Length;
        double uniform = 1.0 / nodeCount;
        for (int nodeId = 0; nodeId < nodeCount; nodeId++)
        {
            nodes[nodeId].VisitRate = uniform;
            nodes[nodeId].Prior = uniform;
        }

        return new FlowNetwork(nodes, edges, edgeOffsets);
    }

    /// <summary>
    /// Computes and stores flow passing through nodes
    /// </summary>
    internal static void ComputeNodeEnterExit(NodeData[] nodes, EdgeData[] edges, int[] edgeOffsets)
    {
        for (int nodeId = 0; nodeId < nodes.Length; nodeId++)
        {
            nodes[nodeId].EnterFlow = nodes[nodeId].ExternalFlow;
            nodes[nodeId].ExitFlow = nodes[nodeId].ExternalFlow;
        }

        for (int nodeId = 0; nodeId < nodes.Length; nodeId++)
        {
            for (int edgeIndex = edgeOffsets[nodeId]; edgeIndex < edgeOffsets[nodeId + 1]; edgeIndex++)
            {
                EdgeData edge = edges[edgeIndex];
                if (edge.Target == nodeId)
                    continue;

                nodes[nodeId].ExitFlow += edge.Flow;
                nodes[edge.Target].EnterFlow += edge.Flow;
            }
        }
    }
}
