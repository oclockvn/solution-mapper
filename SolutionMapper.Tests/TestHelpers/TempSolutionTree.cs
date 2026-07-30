namespace SolutionMapper.Tests.TestHelpers;

public sealed class TempSolutionTree : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"));

    public static TempSolutionTree Create()
    {
        var t = new TempSolutionTree();
        Directory.CreateDirectory(t.Root);
        return t;
    }

    public string AddProject(string relativePath, string? contents = null)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents ?? MinimalCsproj());
        return full;
    }

    public static string MinimalCsproj(
        string? assemblyName = null,
        string? targetFramework = "net10.0",
        string? rootNamespace = null,
        IEnumerable<string>? projectRefs = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("""<Project Sdk="Microsoft.NET.Sdk">""");
        sb.AppendLine("<PropertyGroup>");
        if (assemblyName is not null) sb.AppendLine($"<AssemblyName>{assemblyName}</AssemblyName>");
        if (targetFramework is not null) sb.AppendLine($"<TargetFramework>{targetFramework}</TargetFramework>");
        if (rootNamespace is not null) sb.AppendLine($"<RootNamespace>{rootNamespace}</RootNamespace>");
        sb.AppendLine("</PropertyGroup>");
        if (projectRefs is not null)
        {
            sb.AppendLine("<ItemGroup>");
            foreach (var r in projectRefs)
                sb.AppendLine($"""<ProjectReference Include="{r}" />""");
            sb.AppendLine("</ItemGroup>");
        }
        sb.AppendLine("</Project>");
        return sb.ToString();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { /* ponytail: best-effort temp cleanup */ }
    }
}
