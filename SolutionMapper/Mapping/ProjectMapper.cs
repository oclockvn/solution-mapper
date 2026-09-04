using SolutionMapper.UI;

namespace SolutionMapper.Mapping;

public static class ProjectMapper
{
    public static IReadOnlyList<ProjectMapping> Map(string legacyRoot, string upgradedRoot) =>
        Map(SolutionScan.Create(legacyRoot), SolutionScan.Create(upgradedRoot));

    public static IReadOnlyList<ProjectMapping> Map(SolutionScan legacyScan, SolutionScan upgradedScan)
    {
        using var _ = Metrics.Measure("ProjectMapper.Map (total)");
        var legacy = legacyScan.ProjectFiles;
        var upgraded = upgradedScan.ProjectFiles;

        var legacyByName = legacy.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var upgradedByName = upgraded.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var names = legacyByName.Keys.Union(upgradedByName.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var results = new List<ProjectMapping>();

        // M×N groups need metadata for every project on both sides; warm the cache in one
        // parallel pass so the sequential group loop below only ever hits cached reads.
        WarmMxNMetadata(names, legacyByName, upgradedByName);

        foreach (var fileName in names)
        {
            legacyByName.TryGetValue(fileName, out var leftList);
            upgradedByName.TryGetValue(fileName, out var rightList);
            leftList ??= [];
            rightList ??= [];

            if (leftList.Count == 1 && rightList.Count == 1)
            {
                results.Add(Create(fileName, leftList[0], rightList[0], MappingStatus.Matched, 1.0, false, false));
                continue;
            }

            if (leftList.Count == 0)
            {
                foreach (var r in rightList)
                    results.Add(Create(fileName, null, r, MappingStatus.UpgradedOnly, 0, false, false));
                continue;
            }

            if (rightList.Count == 0)
            {
                foreach (var l in leftList)
                    results.Add(Create(fileName, l, null, MappingStatus.LegacyOnly, 0, false, false));
                continue;
            }

            // ponytail: shared project copied into many solutions → fan out 1×N / N×1
            if (leftList.Count == 1 && rightList.Count > 1)
            {
                foreach (var r in rightList)
                    results.Add(Create(fileName, leftList[0], r, MappingStatus.Ambiguous, 1.0, true, true));
                continue;
            }

            if (rightList.Count == 1 && leftList.Count > 1)
            {
                foreach (var l in leftList)
                    results.Add(Create(fileName, l, rightList[0], MappingStatus.Ambiguous, 1.0, true, true));
                continue;
            }

            // M×N duplicates: greedy strongest pairs
            using var mxn = Metrics.Measure("ProjectMapper M×N group (cached meta + pairs + sort)");
            Metrics.Count("M×N groups");
            Metrics.Count("M×N pairs scored", (long)leftList.Count * rightList.Count);
            var leftMeta = leftList.Select(p => (Path: p, Meta: ProjectMetadataReader.Read(p))).ToList();
            var rightMeta = rightList.Select(p => (Path: p, Meta: ProjectMetadataReader.Read(p))).ToList();
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
                results.Add(Create(fileName, pair.L, pair.R, MappingStatus.Ambiguous, confidence, true, false));
            }

            foreach (var l in leftList.Where(p => !usedLeft.Contains(p)))
                results.Add(Create(fileName, l, null, MappingStatus.LegacyOnly, 0, false, false));
            foreach (var r in rightList.Where(p => !usedRight.Contains(p)))
                results.Add(Create(fileName, null, r, MappingStatus.UpgradedOnly, 0, false, false));
        }

        return results
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.UpgradedFolder ?? m.LegacyFolder, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static void WarmMxNMetadata(
        IReadOnlyList<string> names,
        Dictionary<string, List<string>> legacyByName,
        Dictionary<string, List<string>> upgradedByName)
    {
        var toRead = new List<string>();
        foreach (var fileName in names)
        {
            var l = legacyByName.TryGetValue(fileName, out var ll) ? ll.Count : 0;
            var r = upgradedByName.TryGetValue(fileName, out var rl) ? rl.Count : 0;
            // only the true M×N branch reads metadata (see group loop); mirror its guard
            if (l >= 2 && r >= 2)
            {
                toRead.AddRange(legacyByName[fileName]);
                toRead.AddRange(upgradedByName[fileName]);
            }
        }
        if (toRead.Count == 0) return;

        using var _ = Metrics.Measure("ProjectMapper.WarmMxNMetadata (parallel pre-read)");
        Metrics.Count("M×N metadata files pre-read", toRead.Count);
        Parallel.ForEach(toRead, p => ProjectMetadataReader.Read(p));
    }

    static ProjectMapping Create(
        string fileName, string? legacyFile, string? upgradedFile,
        MappingStatus status, double confidence, bool ambiguous, bool oneToMany) => new()
    {
        Name = Path.GetFileNameWithoutExtension(fileName),
        ProjectFileName = fileName,
        LegacyProjectFile = legacyFile,
        UpgradedProjectFile = upgradedFile,
        LegacyFolder = legacyFile is null ? null : Path.GetDirectoryName(legacyFile),
        UpgradedFolder = upgradedFile is null ? null : Path.GetDirectoryName(upgradedFile),
        Status = status,
        MatchConfidence = confidence,
        IsAmbiguous = ambiguous,
        IsOneToMany = oneToMany
    };
}
