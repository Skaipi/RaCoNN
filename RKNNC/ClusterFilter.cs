namespace RKNNC;

internal static class ClusterFilter
{
    internal static int[] Apply(int[] labels, int minPoints)
    {
        if (minPoints <= 1)
            return labels;

        var counts = new Dictionary<int, int>();

        foreach (var label in labels)
        {
            if (label < 0) continue; // preserve existing noise labels
            counts[label] = counts.TryGetValue(label, out var c) ? c + 1 : 1;
        }

        var filtered = (int[])labels.Clone();

        for (int i = 0; i < filtered.Length; i++)
        {
            var label = filtered[i];
            if (label >= 0 && counts[label] < minPoints)
                filtered[i] = -1;
        }

        return filtered;
    }
}
