namespace TrayLauncher;

/// <summary>
/// [Section] / Key=Value 形式の最小限のiniパーサー。
/// GetPrivateProfileSectionNames のダブルヌル終端バッファ処理を避けるため自前実装する。
/// </summary>
internal sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> SectionNames => _sections.Keys;

    public static IniFile Load(string path)
    {
        var ini = new IniFile();
        if (!File.Exists(path))
        {
            return ini;
        }

        string? currentSection = null;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                ini._sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            ini._sections[currentSection][key] = value;
        }

        return ini;
    }

    public string? GetValue(string section, string key)
        => _sections.TryGetValue(section, out var kv) && kv.TryGetValue(key, out string? v) ? v : null;
}
