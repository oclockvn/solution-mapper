namespace SolutionMapper.Mapping;

public static class MappingExport
{
    public static void Write(
        string path, string legacyRoot, string upgradedRoot, IReadOnlyList<ProjectMapping> projects)
    {
        var payload = new
        {
            legacyRoot,
            upgradedRoot,
            projects
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
    }
}
