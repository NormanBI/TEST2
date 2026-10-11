using System.Runtime.CompilerServices;

namespace Kanzlei.Web.Tests;

// Verhindert, dass Tests in das echte App_Data-Verzeichnis schreiben; jeder Testlauf nutzt eine eigene Datei.
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Init()
    {
        var root = Path.Combine(Path.GetTempPath(), "kanzlei-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("THEME_CSS_FILE", Path.Combine(root, "theme.css"));
        Environment.SetEnvironmentVariable("DB_CONFIG_FILE", Path.Combine(root, "db-settings.json"));
        Environment.SetEnvironmentVariable("DATAPROTECTION_KEYS_PATH", Path.Combine(root, "keys"));
    }
}
