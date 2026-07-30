namespace SolutionMapper.Mapping;

public static class ProjectMapper
{
    public static IReadOnlyList<ProjectMapping> Map(string legacyRoot, string upgradedRoot)
    {
        var legacy = ProjectDiscovery.FindProjects(legacyRoot);
        var upgraded = ProjectDiscovery.FindProjects(upgradedRoot);

        var legacyByName = legacy.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var upgradedByName = upgraded.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var names = legacyByName.Keys.Union(upgradedByName.Keys, StringComparer.OrdinalIgnoreCase);
        var results = new List<ProjectMapping>();

        foreach (var fileName in names)
        {
            legacyByName.TryGetValue(fileName, out var leftList);
            upgradedByName.TryGetValue(fileName, out var rightList);
            leftList ??= [];
            rightList ??= [];

            if (leftList.Count == 1 && rightList.Count == 1)
            {
                results.Add(Create(fileName, leftList[0], rightList[0], MappingStatus.Matched, 1.0, false));
                continue;
            }

            if (leftList.Count == 0)
            {
                foreach (var r in rightList)
                    results.Add(Create(fileName, null, r, MappingStatus.UpgradedOnly, 0, false));
                continue;
            }

            if (rightList.Count == 0)
            {
                foreach (var l in leftList)
                    results.Add(Create(fileName, l, null, MappingStatus.LegacyOnly, 0, false));
                continue;
            }

            // duplicates: greedy strongest pairs
            var leftMeta = leftList.Select(p => (Path: p, Meta: ProjectMetadataReader.TryRead(p))).ToList();
            var rightMeta = rightList.Select(p => (Path: p, Meta: ProjectMetadataReader.TryRead(p))).ToList();
            var usedRight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedLeft = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var pairs = new List<(string L, string R, int Score)>();
            foreach (var l in leftMeta)
            foreach (var r in rightMeta)
                pairs.Add((l.Path, r.Path, ProjectMatcher.Score(l.Meta, r.Meta, fileName)));

            foreach (var pair in pairs.OrderByDescending(p => p.Score))
            {
                if (usedLeft.Contains(pair.L) || usedRight.Contains(pair.R)) continue;
                usedLeft.Add(pair.L);
                usedRight.Add(pair.R);
                var confidence = Math.Min(1.0, pair.Score / 175.0);
                results.Add(Create(fileName, pair.L, pair.R, MappingStatus.Ambiguous, confidence, true));
            }

            foreach (var l in leftList.Where(p => !usedLeft.Contains(p)))
                results.Add(Create(fileName, l, null, MappingStatus.LegacyOnly, 0, false));
            foreach (var r in rightList.Where(p => !usedRight.Contains(p)))
                results.Add(Create(fileName, null, r, MappingStatus.UpgradedOnly, 0, false));
        }

        return results
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static ProjectMapping Create(
        string fileName, string? legacyFile, string? upgradedFile,
        MappingStatus status, double confidence, bool ambiguous) => new()
    {
        Name = Path.GetFileNameWithoutExtension(fileName),
        ProjectFileName = fileName,
        LegacyProjectFile = legacyFile,
        UpgradedProjectFile = upgradedFile,
        LegacyFolder = legacyFile is null ? null : Path.GetDirectoryName(legacyFile),
        UpgradedFolder = upgradedFile is null ? null : Path.GetDirectoryName(upgradedFile),
        Status = status,
        MatchConfidence = confidence,
        IsAmbiguous = ambiguous
    };
}
