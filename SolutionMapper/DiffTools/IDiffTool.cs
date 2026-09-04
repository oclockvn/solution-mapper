namespace SolutionMapper.DiffTools;

/// <summary>A left/right folder pair to compare, with a short label for the tab/window.</summary>
public readonly record struct DiffPair(string LeftFolder, string RightFolder, string Label);

public interface IDiffTool
{
    string Name { get; }
    bool IsAvailable();
    string? FindExecutable();
    void Open(string leftFolder, string rightFolder);

    /// <summary>
    /// Open several folder pairs. Tools that support a single multi-tab window (WinMerge)
    /// override this; the default opens each pair in its own window.
    /// </summary>
    void OpenMany(IReadOnlyList<DiffPair> pairs)
    {
        foreach (var p in pairs)
            Open(p.LeftFolder, p.RightFolder);
    }

    /// <summary>True when <see cref="OpenMany"/> opens one window instead of one per pair.</summary>
    bool SupportsSingleWindow => false;
}
