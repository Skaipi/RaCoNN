using System.Buffers;

namespace MapMinimizer;

/// <summary>
/// Small helpers for normalizing label arrays into dense id ranges.
/// </summary>
internal static class Labeling
{
    /// <summary>
    /// Remaps labels to consecutive ids in first-seen order.
    /// </summary>
    internal static int[] Compact(int[] labels, out int labelCount)
    {
        if (labels == null)
            throw new ArgumentNullException(nameof(labels));

        if (labels.Length == 0)
        {
            labelCount = 0;
            return labels;
        }

        int maxLabel = GetMaxLabel(labels, nameof(labels));
        int[] remap = ArrayPool<int>.Shared.Rent(maxLabel + 1);

        try
        {
            Array.Fill(remap, -1, 0, maxLabel + 1);

            int next = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                int label = labels[i];
                int compactLabel = remap[label];
                if (compactLabel == -1)
                {
                    compactLabel = next++;
                    remap[label] = compactLabel;
                }

                labels[i] = compactLabel;
            }

            labelCount = next;
            return labels;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(remap, clearArray: false);
        }
    }

    /// <summary>
    /// Validates a non-negative label array and returns its maximum value.
    /// </summary>
    private static int GetMaxLabel(int[] labels, string paramName)
    {
        int maxLabel = -1;
        for (int i = 0; i < labels.Length; i++)
        {
            int label = labels[i];
            if (label < 0)
                throw new ArgumentException("Labels must be non-negative.", paramName);
            if (label > maxLabel)
                maxLabel = label;
        }

        return maxLabel;
    }
}
