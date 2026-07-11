using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Disco;

public static partial class Build
{
    [GeneratedRegex(@"(0x[0-9A-Fa-f]+)")]
    private static partial Regex ArticyIdRegex();

    public static void Run(string rootDir)
    {
        var dataDir = Path.Combine(rootDir, "data");
        var chineseBundlePath = Path.Combine(rootDir, "assets", "lockits", "chinese_0");
        var outputPath = Path.Combine(rootDir, "build", "chinese");

        Console.WriteLine($"Loading translations: {dataDir}");
        var entries = TsvIO.ReadDir(dataDir);
        var trans = new Dictionary<(string ArticyId, string TermType), string>();
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.Zh))
                trans[(e.ArticyId, e.TermType)] = e.Zh;
        Console.WriteLine($"  {trans.Count:N0} entries");

        Console.WriteLine($"Loading original bundle: {chineseBundlePath}");
        var manager = new AssetsManager();
        var bunInst = manager.LoadBundleFile(chineseBundlePath, true);
        var bun = bunInst.file;
        var afileInst = manager.LoadAssetsFileFromBundle(bunInst, 0, false);
        var afile = afileInst.file;

        int updated = 0;
        foreach (var info in afile.Metadata.AssetInfos)
        {
            var bf = manager.GetBaseField(afileInst, info);
            if (bf == null) continue;

            var mName = bf["m_Name"].AsString;
            if (mName != "DialoguesLockitChinese" && mName != "GeneralLockitChinese")
                continue;

            var termsArr = bf["mSource"]["mTerms"]["Array"];
            int localUpdated = 0;

            foreach (var term in termsArr.Children)
            {
                var termKey = term["Term"].AsString;
                var m = ArticyIdRegex().Match(termKey);
                if (!m.Success)
                    continue;

                var articy = m.Groups[1].Value;
                var termType = termKey.Split('/')[0];

                if (!trans.TryGetValue((articy, termType), out var newZh))
                    continue;

                var langs = term["Languages"]["Array"];
                var oldZh = langs.Children.Count > 0 ? langs.Children[0].AsString : "";
                if (oldZh != newZh)
                {
                    langs.Children[0].AsString = newZh;
                    localUpdated++;
                }
            }

            if (localUpdated > 0)
            {
                info.SetNewData(bf.WriteToByteArray(false));
                Console.WriteLine($"  {mName}: {localUpdated} terms updated");
                updated += localUpdated;
            }
        }

        if (updated == 0)
        {
            Console.WriteLine("  No changes.");
            manager.UnloadAll();
            return;
        }

        bun.BlockAndDirInfo.DirectoryInfos[0].SetNewData(afile);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using (var writer = new AssetsFileWriter(outputPath))
        {
            bun.Write(writer);
        }

        var outSize = new FileInfo(outputPath).Length;
        Console.WriteLine($"\nSaved: {outputPath} ({outSize:N0} bytes)");
        Console.WriteLine($"Total terms updated: {updated}");

        manager.UnloadAll();
    }
}
