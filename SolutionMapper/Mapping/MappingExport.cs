namespace SolutionMapper.Mapping;

public static class MappingExport
{
    public static Task WriteAsync(
        string path, string legacyRoot, string upgradedRoot, IReadOnlyList<ProjectMapping> projects,
        CancellationToken ct = default)
    {
        var payload = new
        {
            legacyRoot,
            upgradedRoot,
            projects
        };
        // serialize to a string (not SerializeAsync to a stream) so the written bytes stay
        // byte-identical to the sync version — the export tests assert exact output.
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        return File.WriteAllTextAsync(path, json, ct);
    }
}
