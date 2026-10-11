# TEST2 – Steuerberater-Webapplikation

Dieses Repository dokumentiert und entwickelt einen Prototyp einer Mehrbenutzer-Webapplikation für Mitarbeitende einer Steuerkanzlei.

## Dokumentation

- [Anforderungsspezifikation und fortlaufendes Änderungsprotokoll](docs/ANFORDERUNGEN.md)

Die Anforderungsspezifikation ist die maßgebliche Quelle für Umfang und Abnahmekriterien. Änderungen und Ergänzungen werden dort mit Versionsnummer und Datum im Änderungsprotokoll festgehalten.

## Prototyp starten

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (siehe `global.json`) und Internetzugang (NuGet-Pakete sowie Syncfusion-Skripte/-Styles per CDN).

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Kanzlei.Web --urls http://localhost:5258
```

Danach `http://localhost:5258` öffnen.

## Demo-Zugänge (nur fiktiv, fest im Code hinterlegt)

| Rolle | Benutzername | Passwort | Menü |
|---|---|---|---|
| Administration | `admin` | `Demo-Admin-1` | Stammdaten, Auswertungen, Einstellungen |
| Steuerberater | `steuerberater` | `Demo-Steuerberater-1` | Stammdaten, Auswertungen |
| Sachbearbeiter | `sachbearbeiter` | `Demo-Sachbearbeiter-1` | Stammdaten, Auswertungen |

Diese Zugangsdaten sind ausschließlich für die Demo gedacht und öffentlich dokumentiert. Nicht für echte Daten verwenden.

## Architektur

- ASP.NET Core MVC auf .NET 8, Projekt `src/Kanzlei.Web`, Tests in `tests/Kanzlei.Web.Tests` (xUnit, `WebApplicationFactory`).
- Anmeldung über Cookie-Authentifizierung mit festen Demo-Benutzern (`Services/DemoUserStore.cs`); keine Datenbank, keine dauerhafte Speicherung (das Sitzungs-Cookie ist ein reines Session-Cookie).
- Rollenprüfung serverseitig: Standardmäßig erfordert jede Seite eine Anmeldung (Fallback-Policy); `StammdatenController` und `AuswertungenController` erlauben alle drei Rollen, `EinstellungenController` nur `Administration` (nicht berechtigt: Weiterleitung zu `/Account/AccessDenied`, HTTP 403). Das Ausblenden des Menüpunkts ist nur zusätzlich.
- POST-Aktionen sind per Anti-Forgery-Token geschützt.
- Layout-Editor (Administration, `Einstellungen > Layout`): 13 semantische Farb-Tokens als CSS-Variablen, strikt als `#rrggbb` validiert, ausgeliefert über `/theme.css`. Das Thema liegt nur im Arbeitsspeicher (`Services/ThemeService.cs`, Singleton), gilt für alle Benutzer und wird beim Neustart auf die Standardfarben zurückgesetzt; es wird nichts nach `wwwroot` geschrieben. Siehe F-009.
- Standardfarben (CSS-Variablen in `wwwroot/css/site.css`): Primär `#1f4e79`, Hintergrund `#f5f7fa`, Text `#212529`, Primär-Kontrast `#ffffff`.
- Mandantenrolle, DATEV, Persistenz, funktionale Daten und dauerhaft gespeicherte Farbthemen sind ausdrücklich nicht enthalten.

## Abhängigkeiten und Syncfusion-Lizenz

- NuGet: `Syncfusion.EJ2.AspNet.Core` 35.1.39 (Kalender-Widget `ejs-calendar`), `Microsoft.AspNetCore.Mvc.Testing` (nur Tests). Bootstrap/jQuery liegen unter `wwwroot/lib`.
- Syncfusion-Skripte und -Styles (`ej2.min.js`, `bootstrap5.css`) werden von `cdn.syncfusion.com` in derselben Version geladen (bei Paket-Update die Version in `Views/Shared/_Layout.cshtml` mitziehen).
- Syncfusion ist kommerziell lizenziert (ggf. kostenlose Community-Lizenz). Ohne Lizenzschlüssel läuft die App, zeigt aber einen Lizenzhinweis. Schlüssel **nie committen**, sondern per Umgebungsvariable setzen:
  `export SYNCFUSION_LICENSE_KEY="<Ihr Schlüssel>"` (alternativ User-Secrets `Syncfusion:LicenseKey`).
- Bekannte Einschränkung: Der Kalender zeigt englische Monats-/Wochentagsnamen, solange keine deutschen Syncfusion-Lokalisierungsdaten eingebunden sind; Wochenstart ist Montag.
