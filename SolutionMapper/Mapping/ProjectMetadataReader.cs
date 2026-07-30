namespace SolutionMapper.Mapping;

public static class ProjectMetadataReader
{
    public static ProjectMetadata? TryRead(string projectFile)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Load(projectFile);
            // ponytail: ignore MSBuild conditions/namespaces; static props only
            string? Prop(string name) =>
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim();

            var assembly = Prop("AssemblyName");
            if (assembly is not null && assembly.Contains("$(")) assembly = null;

            var tfm = Prop("TargetFramework") ?? Prop("TargetFrameworks");
            var rootNs = Prop("RootNamespace");
            if (rootNs is not null && rootNs.Contains("$(")) rootNs = null;

            var refs = doc.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            return new ProjectMetadata(projectFile, assembly, tfm, rootNs, refs);
        }
        catch
        {
            return null;
        }
    }
}
