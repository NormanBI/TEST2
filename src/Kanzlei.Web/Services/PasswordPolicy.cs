namespace Kanzlei.Web.Services;

public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    public const string Hint = "Mindestens 12 Zeichen mit Groß- und Kleinbuchstaben sowie einer Ziffer.";

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "Bitte ein Passwort angeben.";
        if (password.Length < MinLength) return $"Das Passwort muss mindestens {MinLength} Zeichen lang sein.";
        if (password.Length > MaxLength) return $"Das Passwort darf höchstens {MaxLength} Zeichen lang sein.";
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            return "Das Passwort muss Groß- und Kleinbuchstaben sowie mindestens eine Ziffer enthalten.";
        return null;
    }
}
