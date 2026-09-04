using SolutionMapper.Mapping;
using SolutionMapper.Tests.TestHelpers;

namespace SolutionMapper.Tests.Mapping;

public class ProjectMetadataReaderTests
{
    [Fact]
    public async Task TryRead_extracts_properties_and_refs()
    {
        using var tree = TempSolutionTree.Create();
        var path = tree.AddProject(
            "Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("FooAsm", "net10.0", "Foo.Root", ["../Bar/Bar.csproj"]));

        var meta = await ProjectMetadataReader.TryReadAsync(path);

        Assert.NotNull(meta);
        Assert.Equal("FooAsm", meta!.AssemblyName);
        Assert.Equal("net10.0", meta.TargetFramework);
        Assert.Equal("Foo.Root", meta.RootNamespace);
        Assert.Contains("Bar", meta.ProjectReferenceNames);
    }

    [Fact]
    public async Task TryRead_returns_null_on_corrupt_xml()
    {
        using var tree = TempSolutionTree.Create();
        var path = tree.AddProject("Bad/Bad.csproj", "not xml <<<");
        Assert.Null(await ProjectMetadataReader.TryReadAsync(path));
    }
}
