using System.Threading;
using System.Windows.Forms;

namespace TrayLauncher;

internal static class Program
{
    private const string ConfigFileName = "LaunchTools.ini";

    [STAThread]
    private static void Main()
    {
        // 代表アプリ自体の多重起動も防いでおく。
        using var singleInstance = new Mutex(true, "Local\\LaunchTools_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        IniFile ini = IniFile.Load(configPath);
        IReadOnlyList<AppConfig> apps = AppConfig.LoadAll(ini);

        Application.Run(new TrayApplicationContext(apps));
    }
}
