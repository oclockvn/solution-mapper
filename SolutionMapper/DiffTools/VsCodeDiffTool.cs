using System.Diagnostics;

namespace SolutionMapper.DiffTools;

public sealed class VsCodeDiffTool : IDiffTool
{
    public string Name => "VS Code";

    public bool IsAvailable() => FindExecutable() is not null;

    public string? FindExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "bin", "code.cmd"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        return FindOnPath("code.cmd") ?? FindOnPath("code.exe") ?? FindOnPath("code");
    }

    public void Open(string leftFolder, string rightFolder)
    {
        var exe = FindExecutable()
            ?? throw new InvalidOperationException("VS Code was selected, but code could not be found.");
        Process.Start(CreateStartInfo(exe, leftFolder, rightFolder));
    }

    public static ProcessStartInfo CreateStartInfo(string exe, string leftFolder, string rightFolder)
    {
        // VS Code: open both folders in a new window (stock has no first-class folder-diff CLI)
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        psi.ArgumentList.Add("-n");
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
