using System.Text;

namespace Disco;

public record DialogueEntry
{
    public required string ArticyId  { get; init; }
    public required string TermType  { get; init; }
    public required string ConvTitle { get; init; }
    public required string Actor     { get; init; }
    public required string En        { get; init; }
    public required string Zh        { get; set;  }
}

public static class TsvIO
{
    public static List<DialogueEntry> ReadDir(string dataDir)
    {
        var entries = new List<DialogueEntry>();
        foreach (var path in Directory.GetFiles(dataDir, "*.tsv"))
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            reader.ReadLine(); // header
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split('\t');
                if (parts.Length < 6)
                    continue;
                entries.Add(new DialogueEntry
                {
                    ArticyId  = parts[0].Trim(),
                    TermType  = parts[1].Trim(),
                    ConvTitle = parts[2].Trim(),
                    Actor     = parts[3].Trim(),
                    En        = parts[4],
                    Zh        = parts[5].Replace("\\n", "\n").Replace("\\t", "\t")
                });
            }
        }
        return entries;
    }
}
