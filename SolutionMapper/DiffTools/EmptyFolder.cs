namespace SolutionMapper.DiffTools;

public static class EmptyFolder
{
    public static string GetPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "SolutionMapper", "empty");
        Directory.CreateDirectory(path);
        return path;
    }
}
