namespace MapMinimizer;

/// <summary>
/// Reusable scratch buffers for turning flat labelings into an explicit hierarchy tree.
/// </summary>
internal sealed class HierarchyBuildWorkspace
{
    private int labelStampToken;
    private int[] labelStamp;
    private int[] labelCounts;
    private int[] encounteredLabels;
    private int[] labelCursor;
    private int[] leafScratch;

    internal int[] LabelStamp => labelStamp;
    internal int[] LabelCounts => labelCounts;
    internal int[] EncounteredLabels => encounteredLabels;
    internal int[] LabelCursor => labelCursor;

    internal HierarchyBuildWorkspace(int initialLabelCapacity, int initialLeafCapacity)
    {
        int labelCapacity = Math.Max(1, initialLabelCapacity);
        labelStamp = new int[labelCapacity];
        labelCounts = new int[labelCapacity];
        encounteredLabels = new int[labelCapacity];
        labelCursor = new int[labelCapacity];
        leafScratch = new int[Math.Max(1, initialLeafCapacity)];
    }

    /// <summary>
    /// Returns a fresh stamp token valid for labels in the requested range
    /// </summary>
    internal int NextLabelStamp(int requiredLabelCapacity)
    {
        EnsureCapacity(requiredLabelCapacity);

        labelStampToken++;
        if (labelStampToken != int.MaxValue) return labelStampToken;

        Array.Clear(labelStamp, 0, labelStamp.Length);
        labelStampToken = 1;
        return labelStampToken;
    }

    /// <summary>
    /// Ensures all workspace arrays can address the requested label range
    /// </summary>
    private void EnsureCapacity(int requiredLabelCapacity)
    {
        if (requiredLabelCapacity <= labelStamp.Length) return;

        int newCapacity = Math.Max(requiredLabelCapacity, labelStamp.Length * 2);
        Array.Resize(ref labelStamp, newCapacity);
        Array.Resize(ref labelCounts, newCapacity);
        Array.Resize(ref encounteredLabels, newCapacity);
        Array.Resize(ref labelCursor, newCapacity);
    }

    /// <summary>
    /// Returns a scratch array large enough to temporarily reorder one subtree's leaves
    /// </summary>
    internal int[] RentLeafScratch(int requiredLength)
    {
        if (requiredLength > leafScratch.Length)
            Array.Resize(ref leafScratch, Math.Max(requiredLength, leafScratch.Length * 2));

        return leafScratch;
    }
}
