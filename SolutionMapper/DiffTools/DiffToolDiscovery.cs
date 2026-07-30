namespace SolutionMapper.DiffTools;

public static class DiffToolDiscovery
{
    public static IReadOnlyList<IDiffTool> GetBuiltIn() =>
    [
        new WinMergeDiffTool(),
        new BeyondCompareDiffTool(),
        new VsCodeDiffTool()
    ];

    public static IReadOnlyList<IDiffTool> GetAvailable() =>
        GetBuiltIn().Where(t => t.IsAvailable()).ToList();
}
