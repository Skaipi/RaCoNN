namespace MapMinimizer;

/// <summary>
/// Constructs CSR offsets and mutable edge cursors.
/// </summary>
internal static class CsrLayout
{
    /// <summary>
    /// Computes offsets that allow querying for adjacency of given node.
    /// </summary>
    internal static int[] BuildOffsets(int[] degreeCounts, int? rowCount = null)
    {
        if (degreeCounts == null) throw new ArgumentNullException(nameof(degreeCounts));

        int rows = rowCount ?? degreeCounts.Length;
        if (rows < 0 || rows > degreeCounts.Length)
            throw new ArgumentOutOfRangeException(nameof(rowCount));

        var offsets = new int[rows + 1];
        for (int row = 0; row < rows; row++)
            offsets[row + 1] = checked(offsets[row] + degreeCounts[row]);
        return offsets;
    }

    /// <summary>
    /// Create a cursor that allows querying edges on given node. This cursor is mutable.
    /// </summary>
    internal static int[] CreateCursor(int[] offsets)
    {
        if (offsets == null) throw new ArgumentNullException(nameof(offsets));

        var cursor = new int[offsets.Length - 1];
        Array.Copy(offsets, cursor, cursor.Length);
        return cursor;
    }
}
