namespace MapMinimizer;

/// <summary>Groups object ids into contiguous module ranges while preserving their order.</summary>
internal readonly struct ModuleMembers
{
    internal readonly int[] ObjectIds;
    private readonly int[] offsets;

    internal ModuleMembers(int[] assignments, int moduleCount)
    {
        offsets = new int[moduleCount + 1];
        for (int i = 0; i < assignments.Length; i++) offsets[assignments[i] + 1]++;
        for (int m = 1; m < offsets.Length; m++) offsets[m] += offsets[m - 1];
        var cursors = (int[])offsets.Clone();
        ObjectIds = new int[assignments.Length];
        for (int i = 0; i < assignments.Length; i++) ObjectIds[cursors[assignments[i]]++] = i;
    }

    internal int GetStart(int module) => offsets[module];
    internal int GetCount(int module) => offsets[module + 1] - offsets[module];
}
