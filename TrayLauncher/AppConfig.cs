namespace TrayLauncher;

/// <summary>
/// LaunchTools.ini の [App:Name] セクション1件分の設定。
/// Name は Mutex/終了シグナルの命名に使われるため、機能アプリ側の実行ファイル名
/// (拡張子なし)と一致させる運用とする。
/// </summary>
internal sealed class AppConfig
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Path { get; init; }
    public string Args { get; init; } = string.Empty;

    public static IReadOnlyList<AppConfig> LoadAll(IniFile ini)
    {
        var result = new List<AppConfig>();
        const string prefix = "App:";

        foreach (string section in ini.SectionNames)
        {
            if (!section.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = section[prefix.Length..].Trim();
            string? path = ini.GetValue(section, "Path");
            if (name.Length == 0 || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            result.Add(new AppConfig
            {
                Name = name,
                DisplayName = ini.GetValue(section, "DisplayName") ?? name,
                Path = path,
                Args = ini.GetValue(section, "Args") ?? string.Empty,
            });
        }

        return result;
    }
}
