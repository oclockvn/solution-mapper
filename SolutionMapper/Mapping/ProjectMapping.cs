namespace SolutionMapper.Mapping;

public enum MappingStatus
{
    Matched,
    LegacyOnly,
    UpgradedOnly,
    Ambiguous
}

public sealed record ProjectMapping
{
    public required string Name { get; init; }
    public required string ProjectFileName { get; init; }
    public string? LegacyProjectFile { get; init; }
    public string? UpgradedProjectFile { get; init; }
    public string? LegacyFolder { get; init; }
    public string? UpgradedFolder { get; init; }
    public MappingStatus Status { get; init; }
    public double MatchConfidence { get; init; }
    public bool IsAmbiguous { get; init; }
    /// <summary>True when one side has a single project and the other has many same-named copies.</summary>
    public bool IsOneToMany { get; init; }
}
