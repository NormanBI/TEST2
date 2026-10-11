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
- Layout-Editor (Administration, `Einstellungen > Layout`): 13 semantische Farb-Tokens als CSS-Variablen, strikt als `#rrggbb` validiert, ausgeliefert über `/theme.css`. Das Thema (`Services/ThemeService.cs`, Singleton) gilt für alle Benutzer und wird als CSS-Datei gespeichert (siehe Abschnitt „Theme-Persistenz“); es wird nichts nach `wwwroot` geschrieben. Siehe F-009.
- Standardfarben (CSS-Variablen in `wwwroot/css/site.css`): Primär `#1f4e79`, Hintergrund `#f5f7fa`, Text `#212529`, Primär-Kontrast `#ffffff`.
- Mandantenrolle, DATEV, Datenbank/sonstige Persistenz und funktionale Daten sind ausdrücklich nicht enthalten; einzige dauerhafte Daten sind die Theme-CSS-Datei.

## Theme-Persistenz (Farbeinstellungen)

- **Datei:** Standard `App_Data/theme.css` unter dem Content-Root der Anwendung (außerhalb von `wwwroot`). Ausgeliefert wird das aktive Thema weiterhin über `/theme.css`.
- **Konfiguration:** Umgebungsvariable bzw. Konfigurationswert `THEME_CSS_FILE` (alternativ `Theme:CssFile`); relative Pfade gelten relativ zum Content-Root. Beispiel: `export THEME_CSS_FILE=/var/lib/kanzlei/theme.css`.
- **Neustart:** Beim Start wird die Datei geladen. Fehlt sie, gelten die Standardfarben. Ist sie beschädigt, zu groß oder enthält sie unbekannte/unzulässige Deklarationen (nur bekannte `--app-*`-Variablen mit `#rrggbb` sind erlaubt), wird sie nicht ausgewertet; es wird eine Warnung protokolliert und es gelten die Standardfarben (die Datei wird erst beim nächsten Übernehmen/Zurücksetzen überschrieben).
- **Übernehmen/Zurücksetzen:** „Übernehmen“ schreibt das vollständige, validierte Thema, „Zurücksetzen“ schreibt die Standardfarben. Geschrieben wird atomar (temporäre Datei im selben Verzeichnis, danach Ersetzen); das Verzeichnis wird bei Bedarf angelegt. Schlägt das Schreiben fehl, bleibt das bisherige Thema aktiv und es erscheint eine Fehlermeldung.
- **Berechtigungen:** Der Anwendungsprozess braucht Schreibrechte auf das Zielverzeichnis (Anlegen, Schreiben, Umbenennen, Löschen von Dateien). Das Verzeichnis sollte nicht öffentlich ausgeliefert werden; bei Containern/Deployments ein persistentes Volume einbinden.
- **Mehrere Instanzen:** Nicht unterstützt. Das Thema wird nur beim Start gelesen und im Arbeitsspeicher gehalten; mehrere Instanzen bräuchten gemeinsamen Speicher und eine Koordination (z. B. Neuladen/Benachrichtigung). Der Prototyp ist auf eine einzelne Instanz ausgelegt.
- `App_Data/` ist in `.gitignore`; Laufzeit-Theme-Daten gehören nicht ins Repository.

## Abhängigkeiten und Syncfusion-Lizenz

- NuGet: `Syncfusion.EJ2.AspNet.Core` 35.1.39 (Kalender-Widget `ejs-calendar`), `Microsoft.AspNetCore.Mvc.Testing` (nur Tests). Bootstrap/jQuery liegen unter `wwwroot/lib`.
- Syncfusion-Skripte und -Styles (`ej2.min.js`, `bootstrap5.css`) werden von `cdn.syncfusion.com` in derselben Version geladen (bei Paket-Update die Version in `Views/Shared/_Layout.cshtml` mitziehen).
- Syncfusion ist kommerziell lizenziert (ggf. kostenlose Community-Lizenz). Ohne Lizenzschlüssel läuft die App, zeigt aber einen Lizenzhinweis. Schlüssel **nie committen**, sondern per Umgebungsvariable setzen:
  `export SYNCFUSION_LICENSE_KEY="<Ihr Schlüssel>"` (alternativ User-Secrets `Syncfusion:LicenseKey`).
- Bekannte Einschränkung: Der Kalender zeigt englische Monats-/Wochentagsnamen, solange keine deutschen Syncfusion-Lokalisierungsdaten eingebunden sind; Wochenstart ist Montag.
