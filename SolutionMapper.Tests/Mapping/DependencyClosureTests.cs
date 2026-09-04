using SolutionMapper.Mapping;
using SolutionMapper.Tests.TestHelpers;

namespace SolutionMapper.Tests.Mapping;

public class DependencyClosureTests
{
    [Fact]
    public async Task Expand_translates_legacy_closure_to_mappings()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();

        legacy.AddProject("Web/Web.csproj",
            TempSolutionTree.MinimalCsproj("Web", projectRefs: ["../Common/Exceptions/Exceptions.csproj"]));
        legacy.AddProject("Common/Exceptions/Exceptions.csproj",
            TempSolutionTree.MinimalCsproj("Exceptions", projectRefs: ["../Core/Core.csproj"]));
        legacy.AddProject("Common/Core/Core.csproj", TempSolutionTree.MinimalCsproj("Core"));

        upgraded.AddProject("src/Web/Web.csproj", TempSolutionTree.MinimalCsproj("Web"));
        upgraded.AddProject("src/Exceptions/Exceptions.csproj", TempSolutionTree.MinimalCsproj("Exceptions"));
        upgraded.AddProject("src/Core/Core.csproj", TempSolutionTree.MinimalCsproj("Core"));

        var mappings = await ProjectMapper.MapAsync(legacy.Root, upgraded.Root);
        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(legacy.Root));
        var web = Assert.Single(mappings, m => m.Name == "Web");

        var result = DependencyClosure.Expand(web, graph, mappings);

        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(new[] { "Core", "Exceptions", "Web" },
            result.Nodes.Select(n => n.Mapping.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.True(result.Nodes.Single(n => n.Mapping.Name == "Web").IsRoot);
        Assert.Equal(0, result.Nodes.Single(n => n.Mapping.Name == "Web").Depth);
        Assert.Equal(1, result.Nodes.Single(n => n.Mapping.Name == "Exceptions").Depth);
        Assert.Equal(2, result.Nodes.Single(n => n.Mapping.Name == "Core").Depth);
        Assert.Empty(result.UnresolvedLegacyRefs);
    }

    [Fact]
    public async Task Expand_reports_dependency_missing_from_upgraded_tree()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();

        legacy.AddProject("Web/Web.csproj",
            TempSolutionTree.MinimalCsproj("Web", projectRefs: ["../Dropped/Dropped.csproj"]));
        legacy.AddProject("Dropped/Dropped.csproj", TempSolutionTree.MinimalCsproj("Dropped"));
        upgraded.AddProject("src/Web/Web.csproj", TempSolutionTree.MinimalCsproj("Web"));

        var mappings = await ProjectMapper.MapAsync(legacy.Root, upgraded.Root);
        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(legacy.Root));
        var web = Assert.Single(mappings, m => m.Name == "Web");

        var result = DependencyClosure.Expand(web, graph, mappings);

        var dropped = result.Nodes.Single(n => n.Mapping.Name == "Dropped");
        Assert.Equal(MappingStatus.LegacyOnly, dropped.Mapping.Status);
    }

    [Fact]
    public async Task Expand_respects_max_depth()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();

        legacy.AddProject("Web/Web.csproj",
            TempSolutionTree.MinimalCsproj("Web", projectRefs: ["../L1/L1.csproj"]));
        legacy.AddProject("L1/L1.csproj",
            TempSolutionTree.MinimalCsproj("L1", projectRefs: ["../L2/L2.csproj"]));
        legacy.AddProject("L2/L2.csproj",
            TempSolutionTree.MinimalCsproj("L2", projectRefs: ["../L3/L3.csproj"]));
        legacy.AddProject("L3/L3.csproj", TempSolutionTree.MinimalCsproj("L3"));
        foreach (var n in new[] { "Web", "L1", "L2", "L3" })
            upgraded.AddProject($"src/{n}/{n}.csproj", TempSolutionTree.MinimalCsproj(n));

        var mappings = await ProjectMapper.MapAsync(legacy.Root, upgraded.Root);
        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(legacy.Root));
        var web = Assert.Single(mappings, m => m.Name == "Web");

        var d1 = DependencyClosure.Expand(web, graph, mappings, maxDepth: 1);
        Assert.Equal(new[] { "L1", "Web" }, d1.Nodes.Select(n => n.Mapping.Name).OrderBy(x => x, StringComparer.Ordinal));

        var d2 = DependencyClosure.Expand(web, graph, mappings, maxDepth: 2);
        Assert.Equal(3, d2.Nodes.Count);

        var all = DependencyClosure.Expand(web, graph, mappings, maxDepth: null);
        Assert.Equal(4, all.Nodes.Count);

        var zero = DependencyClosure.Expand(web, graph, mappings, maxDepth: 0);
        Assert.Single(zero.Nodes);
        Assert.True(zero.Nodes[0].IsRoot);
    }

    [Fact]
    public async Task Expand_returns_root_only_when_no_legacy_file()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("Web/Web.csproj", TempSolutionTree.MinimalCsproj("Web"));
        upgraded.AddProject("src/New/New.csproj", TempSolutionTree.MinimalCsproj("New"));

        var mappings = await ProjectMapper.MapAsync(legacy.Root, upgraded.Root);
        var graph = await ProjectGraph.BuildAsync(ProjectDiscovery.FindProjects(legacy.Root));
        var upgradedOnly = Assert.Single(mappings, m => m.Name == "New");

        var result = DependencyClosure.Expand(upgradedOnly, graph, mappings);

        var node = Assert.Single(result.Nodes);
        Assert.True(node.IsRoot);
        Assert.Equal("New", node.Mapping.Name);
    }
}
