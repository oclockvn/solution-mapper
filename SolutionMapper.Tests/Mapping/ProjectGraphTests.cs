using SolutionMapper.Mapping;
using SolutionMapper.Tests.TestHelpers;

namespace SolutionMapper.Tests.Mapping;

public class ProjectGraphTests
{
    [Fact]
    public async Task Closure_follows_transitive_references()
    {
        using var tree = TempSolutionTree.Create();
        tree.AddProject("Web/Web.csproj",
            TempSolutionTree.MinimalCsproj("Web", projectRefs: ["../Common/Exceptions/Exceptions.csproj"]));
        tree.AddProject("Common/Exceptions/Exceptions.csproj",
            TempSolutionTree.MinimalCsproj("Exceptions", projectRefs: ["../Core/Core.csproj"]));
        tree.AddProject("Common/Core/Core.csproj", TempSolutionTree.MinimalCsproj("Core"));

        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(tree.Root));
        var closure = graph.Closure(Path.Combine(tree.Root, "Web", "Web.csproj"))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.Equal(new[] { "Web", "Exceptions", "Core" }, closure);
    }

    [Fact]
    public async Task Closure_is_cycle_safe()
    {
        using var tree = TempSolutionTree.Create();
        tree.AddProject("A/A.csproj", TempSolutionTree.MinimalCsproj("A", projectRefs: ["../B/B.csproj"]));
        tree.AddProject("B/B.csproj", TempSolutionTree.MinimalCsproj("B", projectRefs: ["../A/A.csproj"]));

        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(tree.Root));
        var closure = graph.Closure(Path.Combine(tree.Root, "A", "A.csproj"));

        Assert.Equal(2, closure.Count);
    }

    [Fact]
    public async Task Closure_deduplicates_diamond_dependencies()
    {
        using var tree = TempSolutionTree.Create();
        tree.AddProject("Top/Top.csproj",
            TempSolutionTree.MinimalCsproj("Top", projectRefs: ["../L/L.csproj", "../R/R.csproj"]));
        tree.AddProject("L/L.csproj", TempSolutionTree.MinimalCsproj("L", projectRefs: ["../Base/Base.csproj"]));
        tree.AddProject("R/R.csproj", TempSolutionTree.MinimalCsproj("R", projectRefs: ["../Base/Base.csproj"]));
        tree.AddProject("Base/Base.csproj", TempSolutionTree.MinimalCsproj("Base"));

        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(tree.Root));
        var closure = graph.Closure(Path.Combine(tree.Root, "Top", "Top.csproj"));

        Assert.Equal(4, closure.Count);
        Assert.Single(closure, f => Path.GetFileNameWithoutExtension(f) == "Base");
    }

    [Fact]
    public async Task Build_resolves_by_filename_when_relative_path_missing()
    {
        using var tree = TempSolutionTree.Create();
        // reference points at a path that does not exist; a unique same-named project does.
        tree.AddProject("Web/Web.csproj",
            TempSolutionTree.MinimalCsproj("Web", projectRefs: ["../../old/layout/Lib.csproj"]));
        tree.AddProject("src/Lib/Lib.csproj", TempSolutionTree.MinimalCsproj("Lib"));

        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(tree.Root));
        var closure = graph.Closure(Path.Combine(tree.Root, "Web", "Web.csproj"))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.Contains("Lib", closure);
    }

    [Fact]
    public async Task Build_leaves_edge_unresolved_when_reference_is_external()
    {
        using var tree = TempSolutionTree.Create();
        tree.AddProject("Web/Web.csproj",
            TempSolutionTree.MinimalCsproj("Web", projectRefs: ["../../../External/Gone.csproj"]));

        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(tree.Root));
        var closure = graph.Closure(Path.Combine(tree.Root, "Web", "Web.csproj"));

        Assert.Single(closure);
        Assert.Empty(graph.DirectDependencies(Path.Combine(tree.Root, "Web", "Web.csproj")));
    }
}
