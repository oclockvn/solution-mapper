using System.Diagnostics;

namespace SolutionMapper.DiffTools;

public sealed class WinMergeDiffTool : IDiffTool
{
    public string Name => "WinMerge";

    public bool IsAvailable() => FindExecutable() is not null;

    public string? FindExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        [
            Path.Combine(localAppData, "Programs", "WinMerge", "WinMergeU.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinMerge", "WinMergeU.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinMerge", "WinMergeU.exe"),
            "WinMergeU.exe"
        ];
        foreach (var c in candidates)
        {
            if (c is "WinMergeU.exe")
            {
                var onPath = FindOnPath(c);
                if (onPath is not null) return onPath;
            }
            else if (File.Exists(c)) return c;
        }
        return null;
    }

    public bool SupportsSingleWindow => true;

    public void Open(string leftFolder, string rightFolder)
    {
        var exe = FindExecutable()
            ?? throw new InvalidOperationException("WinMerge was selected, but WinMergeU.exe could not be found.");
        Process.Start(CreateStartInfo(exe, leftFolder, rightFolder));
    }

    public void OpenMany(IReadOnlyList<DiffPair> pairs)
    {
        if (pairs.Count == 0) return;
        if (pairs.Count == 1)
        {
            Open(pairs[0].LeftFolder, pairs[0].RightFolder);
            return;
        }

        var exe = FindExecutable()
            ?? throw new InvalidOperationException("WinMerge was selected, but WinMergeU.exe could not be found.");
        var filter = WinMergeFilter.Resolve(exe);
        // Project <filter> takes a name; the CLI /f below carries the resolved value
        // (a name or a generated .flt path) and wins when they differ.
        var projectFile = WinMergeProjectFile.Write(pairs);
        Process.Start(CreateProjectStartInfo(exe, projectFile, filter));
    }

    public static ProcessStartInfo CreateProjectStartInfo(string exe, string projectFile, string? filter = null)
    {
        // /r recurse, /s single-instance so every tab lands in one window.
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        psi.ArgumentList.Add("/r");
        psi.ArgumentList.Add("/s");
        if (!string.IsNullOrEmpty(filter))
        {
            psi.ArgumentList.Add("/f");
            psi.ArgumentList.Add(filter);
        }
        psi.ArgumentList.Add(projectFile);
        return psi;
    }

    public static ProcessStartInfo CreateStartInfo(string exe, string leftFolder, string rightFolder)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        psi.ArgumentList.Add("/r");
        psi.ArgumentList.Add("/ul");
        psi.ArgumentList.Add("/ur");
        psi.ArgumentList.Add("/f");
        psi.ArgumentList.Add(WinMergeFilter.Resolve(exe));
        psi.ArgumentList.Add("/dl");
        psi.ArgumentList.Add("Legacy");
        psi.ArgumentList.Add("/dr");
        psi.ArgumentList.Add(".NET 10");
        psi.ArgumentList.Add(leftFolder);
        psi.ArgumentList.Add(rightFolder);
        return psi;
    }

    internal static string? FindOnPath(string fileName)
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
