using SolutionMapper.Mapping;
using SolutionMapper.Tests.TestHelpers;

namespace SolutionMapper.Tests.Mapping;

public class ProjectDiscoveryTests
{
    [Fact]
    public void FindProjects_recurses_and_finds_csproj()
    {
        using var tree = TempSolutionTree.Create();
        tree.AddProject("Services/Billing/Billing.csproj");
        tree.AddProject("src/deep/Customer/Customer.csproj");

        var found = ProjectDiscovery.FindProjects(tree.Root);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, p => p.EndsWith("Billing.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(found, p => p.EndsWith("Customer.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureHasProjects_throws_when_empty()
    {
        using var tree = TempSolutionTree.Create();
        var ex = Assert.Throws<InvalidOperationException>(
            () => ProjectDiscovery.EnsureHasProjects(tree.Root, "Legacy"));
        Assert.Contains(tree.Root, ex.Message);
    }
}
