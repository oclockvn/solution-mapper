using System.Xml.Linq;
using SolutionMapper.DiffTools;

namespace SolutionMapper.Tests.DiffTools;

public class WinMergeProjectFileTests
{
    [Fact]
    public void Build_emits_one_paths_element_per_pair()
    {
        var xml = WinMergeProjectFile.Build(
        [
            new DiffPair(@"C:\legacy\A", @"C:\up\A", "A"),
            new DiffPair(@"C:\legacy\B", @"C:\up\B", "B"),
        ]);

        var doc = XDocument.Parse(xml);
        var paths = doc.Root!.Elements("paths").ToList();

        Assert.Equal("project", doc.Root.Name.LocalName);
        Assert.Equal(2, paths.Count);
        Assert.Equal(@"C:\legacy\A", paths[0].Element("left")!.Value);
        Assert.Equal(@"C:\up\A", paths[0].Element("right")!.Value);
        Assert.Equal("1", paths[0].Element("left-readonly")!.Value);
        Assert.Equal("1", paths[0].Element("right-readonly")!.Value);
        Assert.Equal("1", paths[0].Element("subfolders")!.Value);
        Assert.Contains("A", paths[0].Element("left-desc")!.Value);
    }

    [Fact]
    public void Build_defaults_filter_to_visual_csharp_loose()
    {
        var xml = WinMergeProjectFile.Build([new DiffPair(@"C:\l", @"C:\r", "x")]);
        var doc = XDocument.Parse(xml);
        Assert.Equal("Visual C# loose", doc.Root!.Element("paths")!.Element("filter")!.Value);
    }

    [Fact]
    public void Build_honours_an_explicit_filter()
    {
        var xml = WinMergeProjectFile.Build([new DiffPair(@"C:\l", @"C:\r", "x")], "My Filter");
        var doc = XDocument.Parse(xml);
        Assert.Equal("My Filter", doc.Root!.Element("paths")!.Element("filter")!.Value);
    }

    [Fact]
    public void Resolve_falls_back_to_a_generated_flt_when_builtin_is_missing()
    {
        var value = WinMergeFilter.Resolve(@"C:\does\not\exist\WinMergeU.exe");
        Assert.EndsWith(".flt", value);
        Assert.True(File.Exists(value));
        var body = File.ReadAllText(value);
        Assert.Contains("d: \\\\bin$", body);
        Assert.Contains("d: \\\\obj$", body);
        Assert.Contains("def: include", body);
    }

    [Fact]
    public void CreateStartInfo_applies_the_csharp_filter()
    {
        var psi = WinMergeDiffTool.CreateStartInfo(@"C:\WM\WinMergeU.exe", @"C:\L", @"C:\R");
        var fIndex = psi.ArgumentList.IndexOf("/f");
        Assert.True(fIndex >= 0);
        Assert.NotEmpty(psi.ArgumentList[fIndex + 1]);
    }

    [Fact]
    public void Write_creates_a_winmerge_file_on_disk()
    {
        var path = WinMergeProjectFile.Write([new DiffPair(@"C:\l", @"C:\r", "x")]);
        try
        {
            Assert.True(File.Exists(path));
            Assert.EndsWith(".WinMerge", path);
            Assert.Contains("<paths>", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WinMerge_project_start_info_uses_single_instance_and_recurse()
    {
        var psi = WinMergeDiffTool.CreateProjectStartInfo(
            @"C:\WM\WinMergeU.exe", @"C:\tmp\x.WinMerge", "Visual C# loose");
        Assert.Contains("/r", psi.ArgumentList);
        Assert.Contains("/s", psi.ArgumentList);
        Assert.Contains("/f", psi.ArgumentList);
        Assert.Contains("Visual C# loose", psi.ArgumentList);
        Assert.Equal(@"C:\tmp\x.WinMerge", psi.ArgumentList[^1]);
    }

    [Fact]
    public void Default_OpenMany_falls_back_to_one_open_per_pair()
    {
        var fake = new RecordingDiffTool();
        ((IDiffTool)fake).OpenMany(
        [
            new DiffPair(@"C:\a", @"C:\b", "1"),
            new DiffPair(@"C:\c", @"C:\d", "2"),
        ]);

        Assert.Equal(2, fake.Opened.Count);
        Assert.False(((IDiffTool)fake).SupportsSingleWindow);
    }

    sealed class RecordingDiffTool : IDiffTool
    {
        public List<(string, string)> Opened { get; } = [];
        public string Name => "Recording";
        public bool IsAvailable() => true;
        public string? FindExecutable() => "x";
        public void Open(string leftFolder, string rightFolder) => Opened.Add((leftFolder, rightFolder));
    }
}
