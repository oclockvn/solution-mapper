namespace SolutionMapper.DiffTools;

public interface IDiffTool
{
    string Name { get; }
    bool IsAvailable();
    string? FindExecutable();
    void Open(string leftFolder, string rightFolder);
}
