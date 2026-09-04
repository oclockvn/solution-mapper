using System.Text.Json;
using SolutionMapper.Mapping;
using SolutionMapper.Tests.TestHelpers;

namespace SolutionMapper.Tests.Mapping;

public class MappingExportTests
{
    [Fact]
    public async Task Write_emits_roots_and_projects()
    {
        using var dir = TempSolutionTree.Create();
        var outPath = Path.Combine(dir.Root, "mapping.json");
        var projects = new[]
        {
            new ProjectMapping
            {
                Name = "Billing",
                ProjectFileName = "Billing.csproj",
                LegacyFolder = @"C:\L\Billing",
                UpgradedFolder = @"C:\N\Billing",
                Status = MappingStatus.Matched,
                MatchConfidence = 1.0,
                IsAmbiguous = false
            }
        };

        await MappingExport.WriteAsync(outPath, @"C:\L", @"C:\N", projects);

        using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
        Assert.Equal(@"C:\L", doc.RootElement.GetProperty("legacyRoot").GetString());
        Assert.Equal("Billing", doc.RootElement.GetProperty("projects")[0].GetProperty("name").GetString());
    }
}
