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
- Anmeldung über Cookie-Authentifizierung mit festen Demo-Benutzern (`Services/DemoUserStore.cs`); keine Benutzer in der Datenbank (das Sitzungs-Cookie ist ein reines Session-Cookie). Ohne MySQL-Konfiguration erscheint zuerst die Datenbank-Einrichtung (siehe „MySQL-Datenbank“).
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

## MySQL-Datenbank (Ersteinrichtung und Persistenz)

- **Ersteinrichtung:** Existiert keine gültige Datenbankkonfiguration, leitet jede Seite (auch die Anmeldung) auf `/Setup` um. Dort Server, Port (Standard 3306), Datenbank/Schema, Benutzer, Passwort und SSL/TLS (Aus/Bevorzugt/Erforderlich) eingeben. „Verbindung testen“ prüft nur; „Testen und speichern“ testet, wendet die EF-Core-Migrationen an und speichert **erst bei Erfolg**. Danach geht es zur Anmeldung. Die Datenbank/das Schema muss auf dem Server bereits existieren und der Benutzer benötigt Rechte zum Anlegen von Tabellen (`CREATE`, `ALTER`, `INDEX`, `INSERT`, `SELECT`, `UPDATE`, `DELETE`).
- **Fehlermeldungen:** Sichere deutsche Hinweise (nicht erreichbar, Anmeldung fehlgeschlagen, Datenbank unbekannt, Zeitüberschreitung). Passwort und Verbindungszeichenfolge werden weder angezeigt noch protokolliert (Logs enthalten nur Fehlerkategorie, Ausnahmetyp und MySQL-Fehlercode). Eingaben werden validiert (Host/Datenbank nur Zeichenklassen, Längenlimits, Port 1–65535); die Verbindungszeichenfolge wird mit `MySqlConnectionStringBuilder` gebaut (kein Einschleusen weiterer Optionen). Anti-Forgery-Token, Request-Größenlimit (8 KB) und Rate-Limit (20 Anfragen/Minute je IP) schützen `/Setup`.
- **Speicherort:** `App_Data/db-settings.json` (außerhalb `wwwroot`), konfigurierbar über `Database:ConfigFile` bzw. `DB_CONFIG_FILE`. Gespeichert werden Host, Port, Datenbank, Benutzer, SSL-Modus und das **mit ASP.NET Core Data Protection verschlüsselte Passwort**; nie die Verbindungszeichenfolge. Der Schlüsselbund liegt in `App_Data/keys` (`DataProtection:KeysPath` bzw. `DATAPROTECTION_KEYS_PATH`). Datei und Schlüsselbund werden mit 0600/0700 angelegt (Unix).
- **Schutz und Sicherung:** Der Schlüsselbund wird auf Dateisystemebene **nicht** zusätzlich verschlüsselt (kein Zertifikat/DPAPI konfiguriert). Wer Konfigurationsdatei **und** Schlüsselbund lesen kann, kann das Passwort wiederherstellen. Beide Pfade nur für das Dienstkonto lesbar/schreibbar halten, nicht in dasselbe öffentliche Volume/Repository legen und gemeinsam (getrennt gesichert) sichern: Ohne Schlüsselbund ist die Datei nicht mehr entschlüsselbar. Für Produktion: Schlüsselschutz (z. B. X.509-Zertifikat, Key Vault) und ein Datenbankkonto mit minimalen Rechten einsetzen. `App_Data/` ist in `.gitignore`; nichts davon gehört ins Repository.
- **Start:** Konfiguration wird geladen/entschlüsselt, Verbindung geprüft und ausstehende Migrationen werden automatisch angewendet (Einzelinstanz-Annahme; bei mehreren Instanzen Migrationen separat ausführen). Ist der Server vorübergehend nicht erreichbar, erscheint `/Setup/Status` (HTTP 503, Wiederholung nach höchstens 5 Sekunden) – die gespeicherte Konfiguration bleibt unverändert. Ist die Konfiguration unlesbar/beschädigt oder nicht entschlüsselbar, wird die Anmeldung gesperrt und eine Wiederherstellungsseite gezeigt; die Datei wird nie automatisch überschrieben.
- **Wiederherstellung/Neukonfiguration:** Nach erfolgreicher Einrichtung ist `/Setup` nicht mehr erreichbar (404). Zum Ändern der Zugangsdaten lokal auf dem Server `db-settings.json` löschen (bei verlorenem Schlüsselbund ggf. auch den Ordner `keys`) und die App neu starten; danach beginnt die Einrichtung erneut. Eine Web-Oberfläche zur Neukonfiguration gibt es bewusst nicht.
- **Schema/Migrationen:** `Data/AppDbContext.cs` (Pomelo.EntityFrameworkCore.MySql 8.0.3, passend zu EF Core 8; MySqlConnector 2.3.5) ist die Persistenzgrundlage ohne Entitäten; `Data/Migrations/InitialCreate` legt nur `__EFMigrationsHistory` an. Neue Migration: `dotnet ef migrations add <Name> -o Data/Migrations` im Ordner `src/Kanzlei.Web` (Tool `dotnet-ef` 8.x). Die Demo-Benutzer (`DemoUserStore`) bleiben unverändert im Code; das Farbthema bleibt in `App_Data/theme.css` und wird nicht in MySQL gespeichert.
- **Tests:** Laufen ohne MySQL mit Fakes (`TestSupport.cs`). Ein echter MySQL-Test ist nicht enthalten.

## Abhängigkeiten und Syncfusion-Lizenz

- NuGet: `Syncfusion.EJ2.AspNet.Core` 35.1.39 (Kalender-Widget `ejs-calendar`), `Pomelo.EntityFrameworkCore.MySql` 8.0.3, `Microsoft.EntityFrameworkCore.Relational`/`.Design` 8.0.31, `Microsoft.AspNetCore.Mvc.Testing` (nur Tests). Bootstrap/jQuery liegen unter `wwwroot/lib`.
- Syncfusion-Skripte und -Styles (`ej2.min.js`, `bootstrap5.css`) werden von `cdn.syncfusion.com` in derselben Version geladen (bei Paket-Update die Version in `Views/Shared/_Layout.cshtml` mitziehen).
- Syncfusion ist kommerziell lizenziert (ggf. kostenlose Community-Lizenz). Ohne Lizenzschlüssel läuft die App, zeigt aber einen Lizenzhinweis. Schlüssel **nie committen**, sondern per Umgebungsvariable setzen:
  `export SYNCFUSION_LICENSE_KEY="<Ihr Schlüssel>"` (alternativ User-Secrets `Syncfusion:LicenseKey`).
- Bekannte Einschränkung: Der Kalender zeigt englische Monats-/Wochentagsnamen, solange keine deutschen Syncfusion-Lokalisierungsdaten eingebunden sind; Wochenstart ist Montag.
