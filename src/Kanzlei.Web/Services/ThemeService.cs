using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kanzlei.Web.Services;

public sealed record ThemeToken(string Key, string Label, string CssVariable, string Default, string Hint);

// Gemeinsames, nur im Arbeitsspeicher gehaltenes Farbthema (keine Persistenz; Reset beim Neustart).
public interface IThemeService
{
    IReadOnlyList<ThemeToken> Tokens { get; }
    IReadOnlyDictionary<string, string> Current { get; }
    bool TryApply(IReadOnlyDictionary<string, string?> values, out Dictionary<string, string> errors);
    void Reset();
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

    private readonly object _lock = new();
    private IReadOnlyDictionary<string, string> _current = Defaults();

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
        lock (_lock) { _current = next; }
        return true;
    }

    public void Reset()
    {
        lock (_lock) { _current = Defaults(); }
    }

    public string BuildCss()
    {
        var theme = Current;
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
