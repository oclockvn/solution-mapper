using System.Text;
using System.Xml.Linq;

namespace SolutionMapper.DiffTools;

/// <summary>
/// Builds a WinMerge <c>.WinMerge</c> project file. Each <c>&lt;paths&gt;</c> element opens as
/// its own tab in a single WinMerge window (multi-tab projects: WinMerge 2.16.4+).
/// </summary>
public static class WinMergeProjectFile
{
  /// <param name="filter">
  /// WinMerge filter to apply to every tab — a built-in filter name (e.g. "Visual C# loose")
  /// or a path to a <c>.flt</c> file. Defaults to the "Visual C# loose" name.
  /// </param>
  public static string Build(IReadOnlyList<DiffPair> pairs, string? filter = null)
  {
    filter ??= WinMergeFilter.BuiltInName;

    // WinMerge reads repeated <paths> siblings under <project>.
    var root = new XElement("project");
    foreach (var p in pairs)
    {
      root.Add(new XElement("paths",
          new XElement("left", p.LeftFolder),
          new XElement("right", p.RightFolder),
          new XElement("filter", filter),
          new XElement("subfolders", 1),
          new XElement("left-desc", $"Legacy: {p.Label}"),
          new XElement("right-desc", $".NET 10: {p.Label}")));
    }

    var doc = new XDocument(
        new XDeclaration("1.0", "UTF-8", null),
        root);

    // StringWriter always reports utf-16 in the declaration; write through a
    // UTF-8 encoding so the declaration matches how Write() persists the file.
    using var stream = new MemoryStream();
    var settings = new System.Xml.XmlWriterSettings
    {
      Encoding = new UTF8Encoding(false),
      Indent = true,
    };
    using (var xw = System.Xml.XmlWriter.Create(stream, settings))
      doc.Save(xw);
    return new UTF8Encoding(false).GetString(stream.ToArray());
  }

  /// <summary>Writes the project file to a temp path and returns it.</summary>
  public static async Task<string> WriteAsync(IReadOnlyList<DiffPair> pairs, string? filter = null)
  {
    var dir = Path.Combine(Path.GetTempPath(), "SolutionMapper");
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, $"closure-{DateTime.Now:yyyyMMdd-HHmmss}.WinMerge");
    await File.WriteAllTextAsync(path, Build(pairs, filter), new UTF8Encoding(false));
    return path;
  }
}
