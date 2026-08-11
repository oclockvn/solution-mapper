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

    public void Open(string leftFolder, string rightFolder)
    {
        var exe = FindExecutable()
            ?? throw new InvalidOperationException("WinMerge was selected, but WinMergeU.exe could not be found.");
        Process.Start(CreateStartInfo(exe, leftFolder, rightFolder));
    }

    public static ProcessStartInfo CreateStartInfo(string exe, string leftFolder, string rightFolder)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        psi.ArgumentList.Add("/r");
        psi.ArgumentList.Add("/ul");
        psi.ArgumentList.Add("/ur");
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
