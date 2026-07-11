using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Disco;

public static class Diff
{
    public static void Run(string rootDir)
    {
        var manager = new AssetsManager();
        var originalZh = Extract.ExtractChinese(manager, Path.Combine(rootDir, "assets", "lockits", "chinese_0"));
        manager.UnloadAll();

        Console.WriteLine($"Original ZH terms: {originalZh.Count:N0}");

        var entries = TsvIO.ReadDir(Path.Combine(rootDir, "data"));

        var diffs = new List<(string ArticyId, string TermType, string ConvTitle, string Actor, string En, string OldZh, string NewZh)>();
        int matched = 0, changed = 0;

        foreach (var e in entries)
        {
            if (!originalZh.TryGetValue((e.ArticyId, e.TermType), out var oldZh))
                continue;
            matched++;

            var oldZhNorm = EscapeTsv(oldZh);
            if (oldZhNorm == EscapeTsv(e.Zh))
                continue;

            changed++;
            diffs.Add((e.ArticyId, e.TermType, e.ConvTitle, e.Actor, e.En, oldZhNorm, EscapeTsv(e.Zh)));
        }

        Console.WriteLine($"  Matched:    {matched:N0}");
        Console.WriteLine($"  Changed:    {changed:N0}");

        var outputPath = Path.Combine(rootDir, "build", "diff.md");
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true));

        writer.WriteLine($"# Translation Diff\n");
        writer.WriteLine($"> Total entries: {matched:N0}  |  Changed: {changed:N0}\n");

        var groups = diffs.GroupBy(d => (d.ConvTitle, d.Actor))
            .OrderBy(g => g.Key.ConvTitle).ThenBy(g => g.Key.Actor);

        foreach (var group in groups)
        {
            var heading = string.IsNullOrEmpty(group.Key.Actor)
                ? group.Key.ConvTitle
                : $"{group.Key.ConvTitle} — {group.Key.Actor}";
            writer.WriteLine($"## {heading}");
            writer.WriteLine();
            writer.WriteLine("| ArticyId | TermType | EN | Original | Revised |");
            writer.WriteLine("|---|---|---|---|---|");

            foreach (var d in group)
            {
                var id = $"`{d.ArticyId}`";
                var tt = EscapeMd(d.TermType);
                var en = EscapeMd(d.En);
                var oldZh = EscapeMd(d.OldZh);
                var newZh = EscapeMd(d.NewZh);
                writer.WriteLine($"| {id} | {tt} | {en} | {oldZh} | {newZh} |");
            }

            writer.WriteLine();
        }

        Console.WriteLine($"\nOutput: {outputPath}");
    }

    static string EscapeTsv(string s)
    {
        return (s ?? "").Replace("\t", " ").Replace("\n", "\\n");
    }

    static string EscapeMd(string s)
    {
        return s.Replace("|", "\\|").Replace("`", "\\`");
    }
}
