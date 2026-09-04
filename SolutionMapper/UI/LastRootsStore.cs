using System.Text.Json;

namespace SolutionMapper.UI;

public sealed record LastRoots(string LegacyRoot, string UpgradedRoot);

public static class LastRootsStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SolutionMapper",
        "last-roots.json");

    public static async Task<LastRoots?> TryLoadAsync(
        string? storePath = null, CancellationToken ct = default)
    {
        var path = storePath ?? DefaultPath;
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var dto = JsonSerializer.Deserialize<LastRootsDto>(json, JsonOptions);
            if (dto is null
                || string.IsNullOrWhiteSpace(dto.LegacyRoot)
                || string.IsNullOrWhiteSpace(dto.UpgradedRoot))
                return null;

            var legacy = Path.GetFullPath(dto.LegacyRoot);
            var upgraded = Path.GetFullPath(dto.UpgradedRoot);
            if (!Directory.Exists(legacy) || !Directory.Exists(upgraded))
                return null;

            return new LastRoots(legacy, upgraded);
        }
        catch
        {
            return null;
        }
    }

    public static Task SaveAsync(
        string legacyRoot, string upgradedRoot, string? storePath = null, CancellationToken ct = default)
    {
        var path = storePath ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dto = new LastRootsDto
        {
            LegacyRoot = Path.GetFullPath(legacyRoot),
            UpgradedRoot = Path.GetFullPath(upgradedRoot)
        };
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(dto, JsonOptions), ct);
    }

    sealed class LastRootsDto
    {
        public string? LegacyRoot { get; set; }
        public string? UpgradedRoot { get; set; }
    }
}
