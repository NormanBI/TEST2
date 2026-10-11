using System.Security.Cryptography;
using System.Text;

namespace Kanzlei.Web.Services;

public sealed record DemoUser(string UserName, string Password, string DisplayName, string Role);

/// <summary>
/// Feste, ausschließlich für die Demo gedachte Benutzer. Keine Persistenz, keine echten Daten.
/// </summary>
public static class DemoUserStore
{
    public static IReadOnlyList<DemoUser> Users { get; } = new[]
    {
        new DemoUser("admin", "Demo-Admin-1", "Demo Administration", Roles.Administration),
        new DemoUser("steuerberater", "Demo-Steuerberater-1", "Demo Steuerberater", Roles.Steuerberater),
        new DemoUser("sachbearbeiter", "Demo-Sachbearbeiter-1", "Demo Sachbearbeiter", Roles.Sachbearbeiter),
    };

    public static DemoUser? Validate(string? userName, string? password)
    {
        if (string.IsNullOrEmpty(userName) || password is null)
        {
            return null;
        }

        var user = Users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            return null;
        }

        var expected = Encoding.UTF8.GetBytes(user.Password);
        var actual = Encoding.UTF8.GetBytes(password);
        return CryptographicOperations.FixedTimeEquals(expected, actual) ? user : null;
    }
}
