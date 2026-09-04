namespace SolutionMapper.Mapping;

public sealed record ProjectMetadata(
    string ProjectFile,
    string? AssemblyName,
    string? TargetFramework,
    string? RootNamespace,
    IReadOnlyList<string> ProjectReferenceNames,
    IReadOnlyList<string> ProjectReferencePaths);
