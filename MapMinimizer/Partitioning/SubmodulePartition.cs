namespace MapMinimizer;

/// <summary>Submodule assignments together with their incumbent parent module ids.</summary>
internal readonly struct SubmodulePartition
{
    internal readonly int[] Assignments;
    internal readonly int[] ParentModules;

    internal SubmodulePartition(int[] assignments, int[] parentModules)
    {
        Assignments = assignments;
        ParentModules = parentModules;
    }

    internal int ModuleCount => ParentModules.Length;
}
