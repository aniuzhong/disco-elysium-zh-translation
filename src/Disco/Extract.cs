using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Disco;

public static partial class Extract
{
    const string DataDir = "data";
    const string AssetsDir = "assets";

    [GeneratedRegex(@"(0x[0-9A-Fa-f]+)")]
    private static partial Regex ArticyIdRegex();

    private static readonly string[] TermTypes =
    {
        "Dialogue Text", "Alternate1", "Alternate2", "Alternate3", "Alternate4",
        "tooltip1", "tooltip2", "tooltip3", "tooltip4", "tooltip5",
        "tooltip6", "tooltip7", "tooltip8", "tooltip9", "tooltip10"
    };

    public static void Run(string rootDir)
    {
        var dialogueBundlePath = FindDialogueBundle(Path.Combine(rootDir, AssetsDir));
        var chineseBundlePath = Path.Combine(rootDir, AssetsDir, "lockits", "chinese_0");

        Console.WriteLine($"Dialogue bundle: {Path.GetFileName(dialogueBundlePath)}");
        Console.WriteLine($"Chinese bundle:  {Path.GetFileName(chineseBundlePath)}");

        var manager = new AssetsManager();

        var enEntries = ExtractEnglish(manager, dialogueBundlePath);
        Console.WriteLine($"  EN entries: {enEntries.Count:N0}");

        var zhMap = ExtractChinese(manager, chineseBundlePath);
        Console.WriteLine($"  ZH terms:   {zhMap.Count:N0}");

        // Merge by (ArticyId, TermType)
        int matched = 0;
        foreach (var entry in enEntries)
        {
            if (zhMap.TryGetValue((entry.ArticyId, entry.TermType), out var zh))
            {
                entry.Zh = zh;
                matched++;
            }
        }
        Console.WriteLine($"  Matched:    {matched:N0} / {enEntries.Count:N0} ({matched * 100.0 / enEntries.Count:F1}%)");

        // Group by convTitle prefix
        var groups = enEntries.GroupBy(e => Prefix(e.ConvTitle));

        var outputDir = Path.Combine(rootDir, DataDir);
        Directory.CreateDirectory(outputDir);

        // Remove old TSV files
        foreach (var f in Directory.GetFiles(outputDir, "*.tsv"))
            File.Delete(f);

        int fileCount = 0;
        foreach (var group in groups.OrderBy(g => g.Key))
        {
            var filePath = Path.Combine(outputDir, $"{SanitizeFileName(group.Key)}.tsv");
            using var writer = new StreamWriter(filePath, false, new System.Text.UTF8Encoding(true));
            writer.WriteLine("ArticyId\tTermType\tConvTitle\tActor\tEn\tZh");
            foreach (var e in group.OrderBy(e => e.ConvTitle).ThenBy(e => e.ArticyId).ThenBy(e => e.TermType))
            {
                writer.WriteLine($"{e.ArticyId}\t{e.TermType}\t{e.ConvTitle}\t{e.Actor}\t{EscapeTsv(e.En)}\t{EscapeTsv(e.Zh)}");
            }
            fileCount++;
        }

        Console.WriteLine($"\nOutput: {fileCount} files -> {outputDir}");

        manager.UnloadAll();
    }

    static List<DialogueEntry> ExtractEnglish(AssetsManager manager, string bundlePath)
    {
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var afileInst = manager.LoadAssetsFileFromBundle(bunInst, 0, false);

        AssetTypeValueField? deField = null;
        foreach (var info in afileInst.file.Metadata.AssetInfos)
        {
            var bf = manager.GetBaseField(afileInst, info);
            if (bf != null && bf["m_Name"].AsString == "Disco Elysium")
            {
                deField = bf;
                break;
            }
        }
        if (deField == null)
        {
            manager.UnloadAll();
            throw new InvalidDataException("Disco Elysium MonoBehaviour not found in dialogue bundle");
        }

        var actors = new Dictionary<int, string>();
        foreach (var actor in deField["actors"]["Array"].Children)
        {
            var id = actor["id"].AsInt;
            var name = GetFieldValue(actor["fields"]["Array"], "Name") ?? "";
            actors[id] = name;
        }

        Console.WriteLine($"  Actors: {actors.Count}");

        var entries = new List<DialogueEntry>();
        foreach (var conv in deField["conversations"]["Array"].Children)
        {
            var convTitle = GetFieldValue(conv["fields"]["Array"], "Title") ?? "";

            foreach (var entry in conv["dialogueEntries"]["Array"].Children)
            {
                var ef = entry["fields"]["Array"];
                var articyId = GetFieldValue(ef, "Articy Id");

                if (string.IsNullOrEmpty(articyId) || articyId == "0x0000000000000000")
                    continue;

                var actorIdStr = GetFieldValue(ef, "Actor") ?? "";
                var actorName = "";
                if (int.TryParse(actorIdStr, out var actorId) && actors.TryGetValue(actorId, out var an))
                    actorName = an;

                // Primary: Dialogue Text (with Menu Text fallback for entries lacking dialogue text)
                var dt = GetFieldValue(ef, "Dialogue Text");
                var mt = GetFieldValue(ef, "Menu Text");
                var primary = dt ?? mt;
                if (!string.IsNullOrEmpty(primary))
                {
                    entries.Add(new DialogueEntry
                    {
                        ArticyId = articyId,
                        TermType = "Dialogue Text",
                        ConvTitle = convTitle,
                        Actor = actorName,
                        En = primary,
                        Zh = ""
                    });
                }

                // Alternates and tooltips — each comes from a same-named PixelCrushers field
                for (int i = 1; i <= 4; i++)
                {
                    var alt = GetFieldValue(ef, $"Alternate{i}");
                    if (!string.IsNullOrEmpty(alt))
                    {
                        entries.Add(new DialogueEntry
                        {
                            ArticyId = articyId,
                            TermType = $"Alternate{i}",
                            ConvTitle = convTitle,
                            Actor = actorName,
                            En = alt,
                            Zh = ""
                        });
                    }
                }

                for (int i = 1; i <= 10; i++)
                {
                    var tt = GetFieldValue(ef, $"tooltip{i}");
                    if (!string.IsNullOrEmpty(tt))
                    {
                        entries.Add(new DialogueEntry
                        {
                            ArticyId = articyId,
                            TermType = $"tooltip{i}",
                            ConvTitle = convTitle,
                            Actor = actorName,
                            En = tt,
                            Zh = ""
                        });
                    }
                }
            }
        }

        manager.UnloadAll();
        return entries;
    }

    public static Dictionary<(string ArticyId, string TermType), string> ExtractChinese(
        AssetsManager manager, string bundlePath)
    {
        var bunInst = manager.LoadBundleFile(bundlePath, true);
        var afileInst = manager.LoadAssetsFileFromBundle(bunInst, 0, false);

        var zhMap = new Dictionary<(string, string), string>();

        foreach (var info in afileInst.file.Metadata.AssetInfos)
        {
            var bf = manager.GetBaseField(afileInst, info);
            if (bf == null)
                continue;

            var mSource = bf["mSource"];
            if (mSource.IsDummy || mSource["mTerms"].IsDummy)
                continue;

            foreach (var term in mSource["mTerms"]["Array"].Children)
            {
                var termKey = term["Term"].AsString;
                var m = ArticyIdRegex().Match(termKey);
                if (!m.Success)
                    continue;

                var articy = m.Groups[1].Value;
                var termType = termKey.Split('/')[0];
                var langs = term["Languages"]["Array"];
                var zh = langs.Children.Count > 0 ? langs.Children[0].AsString : "";

                var key = (articy, termType);
                if (!zhMap.ContainsKey(key))
                    zhMap[key] = zh;
            }
        }

        manager.UnloadAll();
        return zhMap;
    }

    /// PixelCrushers field arrays use {title, value} pairs.
    static string? GetFieldValue(AssetTypeValueField fieldsArray, string targetTitle)
    {
        foreach (var f in fieldsArray.Children)
        {
            if (f["title"].AsString == targetTitle)
                return f["value"].AsString;
        }
        return null;
    }

    static string FindDialogueBundle(string assetsDir)
    {
        var bundles = Directory.GetFiles(assetsDir, "dialoguebundle*");
        if (bundles.Length == 0)
            throw new FileNotFoundException($"No dialoguebundle found in {assetsDir}");
        return bundles[0];
    }

    static string EscapeTsv(string s)
    {
        return (s ?? "").Replace("\t", " ").Replace("\n", "\\n");
    }

    static string Prefix(string convTitle)
    {
        var idx = convTitle.IndexOf(" / ", StringComparison.Ordinal);
        return idx > 0 ? convTitle[..idx] : "_other";
    }

    static string SanitizeFileName(string prefix)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = prefix.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        return new string(chars).Trim();
    }
}
