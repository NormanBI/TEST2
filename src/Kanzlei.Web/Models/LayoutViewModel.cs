using Kanzlei.Web.Services;

namespace Kanzlei.Web.Models;

public class LayoutViewModel
{
    public LayoutViewModel(IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string?>? submitted = null,
        IReadOnlyDictionary<string, string>? errors = null)
    {
        Values = ThemeService.TokenList.ToDictionary(
            t => t.Key,
            t => submitted != null && submitted.TryGetValue(t.Key, out var v) ? v ?? "" : current[t.Key]);
        Errors = errors ?? new Dictionary<string, string>();
    }

    public string Navigationstext { get; init; } = Data.Einstellungen.StandardNavigationstext;
    public string? NavigationstextError { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; }
    public IReadOnlyDictionary<string, string> Errors { get; }
}
