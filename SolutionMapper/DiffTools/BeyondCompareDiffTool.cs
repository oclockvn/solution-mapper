using System.Diagnostics;

namespace SolutionMapper.DiffTools;

public sealed class BeyondCompareDiffTool : IDiffTool
{
    public string Name => "Beyond Compare";

    public bool IsAvailable() => FindExecutable() is not null;

    public string? FindExecutable()
    {
        var programFiles = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var root in programFiles)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root, "Beyond Compare*"))
            {
                var exe = Path.Combine(dir, "BCompare.exe");
                if (File.Exists(exe)) return exe;
            }
        }

        return FindOnPath("BCompare.exe");
    }

    public void Open(string leftFolder, string rightFolder)
    {
        var exe = FindExecutable()
            ?? throw new InvalidOperationException("Beyond Compare was selected, but BCompare.exe could not be found.");
        Process.Start(CreateStartInfo(exe, leftFolder, rightFolder));
    }

    public static ProcessStartInfo CreateStartInfo(string exe, string leftFolder, string rightFolder)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        psi.ArgumentList.Add(leftFolder);
        psi.ArgumentList.Add(rightFolder);
        return psi;
    }

    static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim('"'), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
