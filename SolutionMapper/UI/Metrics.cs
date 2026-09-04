using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Spectre.Console;

namespace SolutionMapper.UI;

/// <summary>
/// Lightweight perf profiler for the mapping pipeline: named timers with call counts
/// and aggregate durations. Enable with environment variable
/// <c>SOLUTIONMAPPER_METRICS=1</c> (or <c>true</c>/<c>yes</c>). When enabled it also
/// appends a run summary to <c>solutionmapper-metrics.log</c> in the working directory
/// (override the path with <c>SOLUTIONMAPPER_METRICS_FILE</c>).
///
/// This exists purely to measure the current hot paths before optimizing them; it is
/// expected to be removed or trimmed once the numbers are captured.
/// </summary>
public static class Metrics
{
    public static bool Enabled { get; } =
        (Environment.GetEnvironmentVariable("SOLUTIONMAPPER_METRICS") ?? "")
            .Trim() is "1" or "true" or "TRUE" or "yes";

    static readonly string? FilePath =
        Environment.GetEnvironmentVariable("SOLUTIONMAPPER_METRICS_FILE") is { Length: > 0 } p
            ? p
            : Enabled ? "solutionmapper-metrics.log" : null;

    sealed class Bucket
    {
        long _calls;
        long _ticks;
        public long Calls => Interlocked.Read(ref _calls);
        public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref _ticks));
        public void Add(long ticks, long calls = 1)
        {
            Interlocked.Add(ref _calls, calls);
            Interlocked.Add(ref _ticks, ticks);
        }
    }

    static readonly ConcurrentDictionary<string, Bucket> Buckets = new();
    static readonly Stopwatch Wall = Stopwatch.StartNew();

    /// <summary>Time a scope: <c>using var _ = Metrics.Measure("name");</c></summary>
    public static IDisposable Measure(string name)
    {
        if (!Enabled) return NullScope.Instance;
        return new Scope(name);
    }

    /// <summary>Record a single sample (ticks) for an already-measured operation.</summary>
    public static void Record(string name, long elapsedTicks)
    {
        if (!Enabled) return;
        Buckets.GetOrAdd(name, _ => new Bucket()).Add(elapsedTicks);
    }

    /// <summary>Bump a counter with no timing (e.g. "csproj files discovered").</summary>
    public static void Count(string name, long n = 1)
    {
        if (!Enabled) return;
        Buckets.GetOrAdd("#" + name, _ => new Bucket()).Add(0, n);
    }

    public static void Dump(string header = "metrics")
    {
        if (!Enabled || Buckets.IsEmpty) return;

        var rows = Buckets
            .OrderByDescending(kv => kv.Value.Elapsed)
            .ThenByDescending(kv => kv.Value.Calls)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"=== {header} @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} (wall {Wall.Elapsed.TotalMilliseconds:F0} ms) ===");
        sb.AppendLine($"{"operation",-40} {"calls",8} {"total ms",12} {"avg µs",12}");
        sb.AppendLine(new string('-', 76));
        foreach (var (name, b) in rows)
        {
            var totalMs = b.Elapsed.TotalMilliseconds;
            var avgUs = b.Calls > 0 ? b.Elapsed.TotalMilliseconds * 1000.0 / b.Calls : 0;
            sb.AppendLine($"{name,-40} {b.Calls,8} {totalMs,12:F1} {avgUs,12:F1}");
        }

        var text = sb.ToString();

        foreach (var line in text.Split('\n'))
            AnsiConsole.MarkupLine($"[grey]{line.TrimEnd('\r').EscapeMarkup()}[/]");

        if (FilePath is not null)
        {
            try { File.AppendAllText(FilePath, text); }
            catch (Exception ex) { Trace.Log($"metrics: could not write {FilePath}: {ex.Message}"); }
        }
    }

    sealed class Scope(string name) : IDisposable
    {
        readonly long _start = Stopwatch.GetTimestamp();
        public void Dispose() =>
            Record(name, ToTicks(Stopwatch.GetTimestamp() - _start));

        static long ToTicks(long swTicks) =>
            (long)(swTicks * (10_000_000.0 / Stopwatch.Frequency));
    }

    sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
