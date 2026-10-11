using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kanzlei.Web.Services;

public sealed record ThemeToken(string Key, string Label, string CssVariable, string Default, string Hint);

// Gemeinsames Farbthema; wird (falls ein Dateipfad konfiguriert ist) als CSS-Datei außerhalb von wwwroot gespeichert.
public interface IThemeService
{
    IReadOnlyList<ThemeToken> Tokens { get; }
    IReadOnlyDictionary<string, string> Current { get; }
    bool TryApply(IReadOnlyDictionary<string, string?> values, out Dictionary<string, string> errors);
    bool Reset();
    string BuildCss();
}

public sealed partial class ThemeService : IThemeService
{
    public static readonly IReadOnlyList<ThemeToken> TokenList = new[]
    {
        new ThemeToken("primary", "Primärfarbe (Marke)", "--app-primary", "#1f4e79", "Navigationsleiste, Hauptschaltflächen"),
        new ThemeToken("accent", "Sekundär-/Akzentfarbe", "--app-accent", "#2a7f9e", "Sekundäre Hervorhebungen"),
        new ThemeToken("background", "Seitenhintergrund", "--app-background", "#f5f7fa", "Hintergrund der Seite"),
        new ThemeToken("surface", "Fläche/Karte", "--app-surface", "#ffffff", "Karten, Kalender, Formulare"),
        new ThemeToken("text", "Haupttext", "--app-text", "#212529", "Standardtext"),
        new ThemeToken("muted", "Gedämpfter Text", "--app-muted", "#5c636a", "Hinweise, sekundäre Texte"),
        new ThemeToken("border", "Rahmen/Trennlinie", "--app-border", "#dee2e6", "Rahmen und Trenner"),
        new ThemeToken("link", "Link", "--app-link", "#0b5ed7", "Textlinks"),
        new ThemeToken("success", "Erfolg", "--app-success", "#198754", "Erfolgsmeldungen"),
        new ThemeToken("warning", "Warnung", "--app-warning", "#ffc107", "Warnhinweise"),
        new ThemeToken("danger", "Fehler/Gefahr", "--app-danger", "#dc3545", "Fehlermeldungen"),
        new ThemeToken("focus", "Fokusmarkierung", "--app-focus", "#0d6efd", "Tastaturfokus"),
        new ThemeToken("calendar", "Kalender: heute/Auswahl", "--app-calendar", "#1f4e79", "Heutiger Tag und gewählter Tag"),
    };

    public const string PersistErrorKey = "_persist";
    private const int MaxFileBytes = 16 * 1024;
    private static readonly ThemeToken[] DerivedTokens =
    {
        new("primary-contrast", "", "--app-primary-contrast", "", ""),
        new("calendar-contrast", "", "--app-calendar-contrast", "", ""),
    };

    private readonly object _lock = new();
    private readonly string? _filePath;
    private readonly ILogger? _logger;
    private IReadOnlyDictionary<string, string> _current = Defaults();

    // Ohne Dateipfad: rein flüchtig (Arbeitsspeicher).
    public ThemeService() { }

    public ThemeService(string? filePath, ILogger? logger = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        _logger = logger;
        if (_filePath != null) _current = LoadOrDefaults();
    }

    public string? FilePath => _filePath;

    public IReadOnlyList<ThemeToken> Tokens => TokenList;

    public IReadOnlyDictionary<string, string> Current
    {
        get { lock (_lock) { return _current; } }
    }

    public static Dictionary<string, string> Defaults() => TokenList.ToDictionary(t => t.Key, t => t.Default);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexPattern();

    public static bool IsValidHex(string? value) => value is not null && HexPattern().IsMatch(value);

    // Alle-oder-nichts: bei einem ungültigen Wert bleibt das bisherige Thema unverändert.
    public bool TryApply(IReadOnlyDictionary<string, string?> values, out Dictionary<string, string> errors)
    {
        errors = new Dictionary<string, string>();
        var next = new Dictionary<string, string>();
        foreach (var token in TokenList)
        {
            values.TryGetValue(token.Key, out var raw);
            var value = raw?.Trim();
            if (!IsValidHex(value))
            {
                errors[token.Key] = "Bitte einen Hex-Farbwert im Format #rrggbb angeben.";
                continue;
            }
            next[token.Key] = value!.ToLowerInvariant();
        }
        if (errors.Count > 0) return false;
        lock (_lock)
        {
            if (!TryPersist(next))
            {
                errors[PersistErrorKey] = "Die Farben konnten nicht dauerhaft gespeichert werden und wurden nicht übernommen.";
                return false;
            }
            _current = next;
        }
        return true;
    }

    // Persistiert die Standardfarben; bei Schreibfehler bleibt das bisherige Thema aktiv und false wird geliefert.
    public bool Reset()
    {
        lock (_lock)
        {
            var defaults = Defaults();
            if (!TryPersist(defaults)) return false;
            _current = defaults;
            return true;
        }
    }

    private bool TryPersist(IReadOnlyDictionary<string, string> theme)
    {
        if (_filePath == null) return true;
        string? tmp = null;
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            tmp = Path.Combine(dir, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tmp, BuildDocument(theme), new UTF8Encoding(false));
            File.Move(tmp, _filePath, overwrite: true);
            tmp = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger?.LogError(ex, "Theme-Datei {Path} konnte nicht geschrieben werden.", _filePath);
            return false;
        }
        finally
        {
            if (tmp != null)
            {
                try { File.Delete(tmp); } catch (Exception) { /* Aufräumen nach Fehler, best effort */ }
            }
        }
    }

    private Dictionary<string, string> LoadOrDefaults()
    {
        try
        {
            var info = new FileInfo(_filePath!);
            if (!info.Exists)
            {
                _logger?.LogInformation("Keine Theme-Datei unter {Path}; Standardfarben werden verwendet.", _filePath);
                return Defaults();
            }
            if (info.Length > MaxFileBytes)
            {
                _logger?.LogWarning("Theme-Datei {Path} ist zu groß; Standardfarben werden verwendet.", _filePath);
                return Defaults();
            }
            if (TryParse(File.ReadAllText(_filePath!), out var theme, out var problem))
            {
                _logger?.LogInformation("Theme aus {Path} geladen.", _filePath);
                return theme;
            }
            _logger?.LogWarning("Theme-Datei {Path} ist ungültig ({Problem}); Standardfarben werden verwendet.", _filePath, problem);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "Theme-Datei {Path} nicht lesbar; Standardfarben werden verwendet.", _filePath);
        }
        return Defaults();
    }

    // Strenger Parser: erlaubt nur ":root {", "}", einzeilige Kommentare und bekannte "--app-*: #rrggbb;"-Deklarationen.
    public static bool TryParse(string css, out Dictionary<string, string> theme, out string problem)
    {
        theme = new Dictionary<string, string>();
        problem = "";
        var byVar = TokenList.ToDictionary(t => t.CssVariable, t => t.Key);
        var derived = DerivedTokens.ToDictionary(t => t.CssVariable);
        var seen = new HashSet<string>();
        var open = false;
        var closed = false;
        foreach (var raw in css.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (closed) { problem = "Inhalt nach Blockende"; return false; }
            if (line.StartsWith("/*", StringComparison.Ordinal) && line.EndsWith("*/", StringComparison.Ordinal)
                && line.Length >= 4 && !line.AsSpan(2, line.Length - 4).Contains("*/", StringComparison.Ordinal))
                continue;
            if (!open)
            {
                if (line != ":root {") { problem = "Unerwarteter Selektor"; return false; }
                open = true;
                continue;
            }
            if (line == "}") { closed = true; continue; }
            var m = DeclarationPattern().Match(line);
            if (!m.Success) { problem = "Unbekannte oder ungültige Deklaration"; return false; }
            var name = m.Groups[1].Value;
            var value = m.Groups[2].Value.ToLowerInvariant();
            if (!seen.Add(name)) { problem = $"Doppelte Variable {name}"; return false; }
            if (byVar.TryGetValue(name, out var key)) theme[key] = value;
            else if (!derived.ContainsKey(name)) { problem = $"Unbekannte Variable {name}"; return false; }
        }
        if (!open || !closed) { problem = "Unvollständiger Block"; return false; }
        var found = theme;
        var missing = TokenList.FirstOrDefault(t => !found.ContainsKey(t.Key));
        if (missing != null) { problem = $"Variable {missing.CssVariable} fehlt"; return false; }
        return true;
    }

    [GeneratedRegex(@"^(--app-[a-z-]+):\s*(#[0-9a-fA-F]{6});$")]
    private static partial Regex DeclarationPattern();

    private static string BuildDocument(IReadOnlyDictionary<string, string> theme) =>
        "/* Generiert vom Layout-Editor (Einstellungen > Layout). Nicht manuell bearbeiten. */\n" + BuildCss(theme);

    public string BuildCss() => BuildCss(Current);

    private static string BuildCss(IReadOnlyDictionary<string, string> theme)
    {
        var sb = new StringBuilder(":root {\n");
        foreach (var token in TokenList)
        {
            sb.Append("  ").Append(token.CssVariable).Append(": ").Append(theme[token.Key]).Append(";\n");
        }
        sb.Append("  --app-primary-contrast: ").Append(ContrastColor(theme["primary"])).Append(";\n");
        sb.Append("  --app-calendar-contrast: ").Append(ContrastColor(theme["calendar"])).Append(";\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    public static string ContrastColor(string hex) =>
        ContrastRatio(hex, "#ffffff") >= ContrastRatio(hex, "#000000") ? "#ffffff" : "#000000";

    public static double ContrastRatio(string hexA, string hexB)
    {
        var a = Luminance(hexA);
        var b = Luminance(hexB);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            var c = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
