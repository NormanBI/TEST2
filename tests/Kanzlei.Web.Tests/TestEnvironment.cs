using System.Runtime.CompilerServices;

namespace Kanzlei.Web.Tests;

// Verhindert, dass Tests in das echte App_Data-Verzeichnis schreiben; jeder Testlauf nutzt eine eigene Datei.
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Init() =>
        Environment.SetEnvironmentVariable("THEME_CSS_FILE",
            Path.Combine(Path.GetTempPath(), "kanzlei-tests", Guid.NewGuid().ToString("N"), "theme.css"));
}
