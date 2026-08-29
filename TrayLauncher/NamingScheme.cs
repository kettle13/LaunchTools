namespace TrayLauncher;

/// <summary>
/// 代表アプリと機能アプリの間で共有する命名規則。
/// 機能アプリ側は同じ規則を各アプリのソースに個別実装するため、
/// この文字列フォーマットを変更する場合は機能アプリ側も合わせて直すこと。
/// </summary>
internal static class NamingScheme
{
    public static string ExitSignalName(string appName) => $"Local\\LaunchTools_{appName}_ExitSignal";
}
