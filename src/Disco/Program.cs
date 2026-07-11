if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- <command>");
    Console.WriteLine("  extract   Extract EN+ZH to data/<area>.tsv");
    Console.WriteLine("  build     Build build/chinese from data/*.tsv");
    Console.WriteLine("  diff      Compare current translations with original, write build/diff.md");
    return;
}
var r = TryFindRoot() ?? throw new("Root not found");
switch (args[0].ToLowerInvariant())
{
    case "extract": Disco.Extract.Run(r); break;
    case "build":   Disco.Build.Run(r);   break;
    case "diff":    Disco.Diff.Run(r);    break;
    default: Console.WriteLine($"Unknown: {args[0]}"); break;
}
static string? TryFindRoot()
{
    var d = Environment.CurrentDirectory;
    while (d != null) {
        if (File.Exists(Path.Combine(d, "assets", "lockits", "chinese_0")))
            return d; var p = Path.GetDirectoryName(d);
        if (p == d)
            break;
        d = p;
    }
    return null;
}
