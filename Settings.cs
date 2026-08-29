using System.Text;
using System.Windows.Forms;

namespace PinchZoomInjector;

/// <summary>
/// exe と同じディレクトリの PinchZoomInjector.ini から設定を読み込む。
/// ファイルが無い/項目が無い/値が不正な場合は既定値にフォールバックする。
/// </summary>
internal sealed class Settings
{
    private const string FileName = "PinchZoomInjector.ini";
    private const string Section = "Settings";
    private const Keys DefaultTriggerKey = Keys.F13;
    private const bool DefaultShowTouchMarkers = false;

    public int TriggerVirtualKeyCode { get; private init; }
    public Keys TriggerKey { get; private init; }
    public bool ShowTouchMarkers { get; private init; }

    public static Settings Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, FileName);

        Keys triggerKey = DefaultTriggerKey;
        bool showMarkers = DefaultShowTouchMarkers;

        if (File.Exists(path))
        {
            string keyName = ReadString(path, "TriggerKey", DefaultTriggerKey.ToString());
            if (Enum.TryParse(keyName, ignoreCase: true, out Keys parsedKey) && parsedKey != Keys.None)
            {
                triggerKey = parsedKey;
            }
            else
            {
                Console.WriteLine($"[Settings] TriggerKey の値 '{keyName}' を認識できませんでした。既定値 {DefaultTriggerKey} を使用します。");
            }

            showMarkers = ReadBool(path, "ShowTouchMarkers", DefaultShowTouchMarkers);
        }

        return new Settings
        {
            TriggerKey = triggerKey,
            TriggerVirtualKeyCode = (int)triggerKey,
            ShowTouchMarkers = showMarkers,
        };
    }

    private static string ReadString(string path, string key, string defaultValue)
    {
        var buffer = new StringBuilder(256);
        NativeMethods.GetPrivateProfileString(Section, key, defaultValue, buffer, (uint)buffer.Capacity, path);
        return buffer.ToString();
    }

    private static bool ReadBool(string path, string key, bool defaultValue)
    {
        string raw = ReadString(path, key, defaultValue.ToString());
        return bool.TryParse(raw, out bool result) ? result : defaultValue;
    }
}
