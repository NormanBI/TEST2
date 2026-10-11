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

## Anmeldung und erster Administrator

Es gibt **keine festen Demo-Zugänge und keine voreingestellten Passwörter** mehr. Konten liegen in MySQL (Tabelle `Benutzer`); die Anmeldung erfolgt mit Emailadresse und Passwort.

1. Datenbank einrichten (`/Setup`, siehe „MySQL-Datenbank“).
2. Mailserver einrichten (`/Setup/Mail`, siehe „Mailserver“); ohne gültige Mail-Einstellungen sind Administrator-Einrichtung und Anmeldung nicht erreichbar.
3. Existiert danach kein **aktiver Administrator**, leitet jede Seite auf `/Setup/Admin` um. Dort Vorname, Nachname, Emailadresse und Passwort (zweimal) für den ersten Administrator festlegen. Die Seite ist nur in diesem Zustand erreichbar (sonst 404); das Anlegen ist idempotent (es entsteht nie ein zweiter „Start“-Administrator). Das Klartext-Passwort wird weder in Konfigurationsdateien noch im Log gespeichert, sondern nur als Hash in MySQL.
4. Anmelden; weitere Benutzer unter **Einstellungen > Benutzer** anlegen.

**Wiederherstellung:** Sind alle Administratoren verloren (nicht möglich über die Oberfläche, da der letzte aktive Administrator nicht deaktiviert werden kann), kann in MySQL `UPDATE Benutzer SET Aktiv = 1 WHERE Administrator = 1` ausgeführt werden. Gibt es gar keinen Administrator mehr, startet beim nächsten Aufruf automatisch `/Setup/Admin`. Vergessene Passwörter: keine Selbstbedienung; ein Administrator legt ein neues Konto an (Passwort-Zurücksetzen ist noch nicht umgesetzt).

## Benutzer, Rollen und Passwörter

- **Tabelle `Benutzer`:** `Id`, `Vorname`, `Nachname`, `Emailadresse` (Pflicht), `EmailNormalisiert` (Großschreibung, eindeutiger Index → Emailadresse ist groß-/kleinschreibungsunabhängig eindeutig), `Kundennutzer` (Standard Nein), `PasswortHash`, `Administrator`, `Eigentuemer` (Anzeige „Eigentümer“), `Steuerberater`, `Sachbearbeiter`, `Aktiv` (Standard Ja), `ErstelltAm`, `GeaendertAm` (UTC). Migration `AddBenutzer` (nur `CREATE TABLE`/`CREATE INDEX`, nicht destruktiv).
- **Passwörter werden nicht verschlüsselt, sondern gehasht:** Gespeichert wird ausschließlich ein gesalzener, adaptiver Einweg-Hash über ASP.NET Core `IPasswordHasher<Benutzer>` (PBKDF2-HMAC-SHA512, Salt pro Passwort). Eine umkehrbare Verschlüsselung würde bei Schlüsselverlust alle Passwörter preisgeben; ein Hash lässt sich nicht zurückrechnen und nur durch Vergleich prüfen. Der Hash wird nie angezeigt, protokolliert oder in Listen ausgegeben; Passwörter werden nie erneut ins Formular gerendert. Richtlinie: 12–128 Zeichen mit Groß-, Kleinbuchstaben und Ziffer.
- **Rollen:** Die Claims leiten sich aus den vier Flags ab (`Administration`, `Eigentuemer`, `Steuerberater`, `Sachbearbeiter`). **Kundennutzer** ist nur eine Klassifizierung und erzeugt keine Rolle. Ein Benutzer ohne Rolle kann sich anmelden, hat aber keinen Zugriff auf rollengeschützte Bereiche. Derzeit erlauben Stammdaten/Auswertungen Administration, Steuerberater und Sachbearbeiter; Einstellungen und Benutzer nur Administration. `Eigentuemer` hat noch keine eigenen Berechtigungen.
- **Deaktivieren statt Löschen:** Es gibt keine Löschfunktion (weder Route noch Button). Deaktivierte Benutzer können sich nicht anmelden; bestehende Sitzungen werden bei der nächsten Anfrage beendet (das Cookie wird bei jeder Anfrage gegen die Datenbank geprüft). Der letzte aktive Administrator kann nicht deaktiviert werden (Schutz gilt auch für den ersten Administrator; Absicherung per Sperre innerhalb einer Instanz, siehe Einzelinstanz-Annahme).
- **Tests:** Benutzerlogik wird mit dem echten `EfBenutzerStore` gegen EF-Core-InMemory getestet; Schema und Migration werden anhand des MySQL-Provider-Skripts geprüft. Ein Test gegen einen echten MySQL-Server (z. B. eindeutiger Index) ist nicht enthalten.

## Architektur

- ASP.NET Core MVC auf .NET 8, Projekt `src/Kanzlei.Web`, Tests in `tests/Kanzlei.Web.Tests` (xUnit, `WebApplicationFactory`).
- Anmeldung über Cookie-Authentifizierung mit Benutzern aus MySQL (`Services/BenutzerStore.cs`, ersetzt den früheren `DemoUserStore`); das Sitzungs-Cookie ist ein reines Session-Cookie und wird bei jeder Anfrage auf aktiven Benutzer geprüft. Ohne MySQL-Konfiguration erscheint zuerst die Datenbank-Einrichtung (siehe „MySQL-Datenbank“).
- Rollenprüfung serverseitig: Standardmäßig erfordert jede Seite eine Anmeldung (Fallback-Policy); `StammdatenController` und `AuswertungenController` erlauben alle drei Rollen, `EinstellungenController` und `BenutzerController` nur `Administration` (nicht berechtigt: Weiterleitung zu `/Account/AccessDenied`, HTTP 403). Das Ausblenden des Menüpunkts ist nur zusätzlich.
- POST-Aktionen sind per Anti-Forgery-Token geschützt.
- Layout-Editor (Administration, `Einstellungen > Layout`): 13 semantische Farb-Tokens als CSS-Variablen, strikt als `#rrggbb` validiert, ausgeliefert über `/theme.css`. Das Thema (`Services/ThemeService.cs`, Singleton) gilt für alle Benutzer und wird als CSS-Datei gespeichert (siehe Abschnitt „Theme-Persistenz“); es wird nichts nach `wwwroot` geschrieben. Siehe F-009.
- Standardfarben (CSS-Variablen in `wwwroot/css/site.css`): Primär `#1f4e79`, Hintergrund `#f5f7fa`, Text `#212529`, Primär-Kontrast `#ffffff`.
- Mandantenrolle, DATEV und funktionale Fachdaten sind ausdrücklich nicht enthalten; dauerhaft gespeichert werden Benutzerkonten (MySQL) und die Theme-CSS-Datei.

## Theme-Persistenz (Farbeinstellungen)

- **Datei:** Standard `App_Data/theme.css` unter dem Content-Root der Anwendung (außerhalb von `wwwroot`). Ausgeliefert wird das aktive Thema weiterhin über `/theme.css`.
- **Konfiguration:** Umgebungsvariable bzw. Konfigurationswert `THEME_CSS_FILE` (alternativ `Theme:CssFile`); relative Pfade gelten relativ zum Content-Root. Beispiel: `export THEME_CSS_FILE=/var/lib/kanzlei/theme.css`.
- **Neustart:** Beim Start wird die Datei geladen. Fehlt sie, gelten die Standardfarben. Ist sie beschädigt, zu groß oder enthält sie unbekannte/unzulässige Deklarationen (nur bekannte `--app-*`-Variablen mit `#rrggbb` sind erlaubt), wird sie nicht ausgewertet; es wird eine Warnung protokolliert und es gelten die Standardfarben (die Datei wird erst beim nächsten Übernehmen/Zurücksetzen überschrieben).
- **Übernehmen/Zurücksetzen:** „Übernehmen“ schreibt das vollständige, validierte Thema, „Zurücksetzen“ schreibt die Standardfarben. Geschrieben wird atomar (temporäre Datei im selben Verzeichnis, danach Ersetzen); das Verzeichnis wird bei Bedarf angelegt. Schlägt das Schreiben fehl, bleibt das bisherige Thema aktiv und es erscheint eine Fehlermeldung.
- **Berechtigungen:** Der Anwendungsprozess braucht Schreibrechte auf das Zielverzeichnis (Anlegen, Schreiben, Umbenennen, Löschen von Dateien). Das Verzeichnis sollte nicht öffentlich ausgeliefert werden; bei Containern/Deployments ein persistentes Volume einbinden.
- **Mehrere Instanzen:** Nicht unterstützt. Das Thema wird nur beim Start gelesen und im Arbeitsspeicher gehalten; mehrere Instanzen bräuchten gemeinsamen Speicher und eine Koordination (z. B. Neuladen/Benachrichtigung). Der Prototyp ist auf eine einzelne Instanz ausgelegt.
- `App_Data/` ist in `.gitignore`; Laufzeit-Theme-Daten gehören nicht ins Repository.

## MySQL-Datenbank (Ersteinrichtung und Persistenz)

- **Ersteinrichtung:** Existiert keine gültige Datenbankkonfiguration, leitet jede Seite (auch die Anmeldung) auf `/Setup` um. Dort Server, Port (Standard 3306), Datenbank/Schema, Benutzer, Passwort und SSL/TLS (Aus/Bevorzugt/Erforderlich) eingeben. „Verbindung testen“ prüft nur; „Testen und speichern“ testet, wendet die EF-Core-Migrationen an und speichert **erst bei Erfolg**. Danach geht es zur Anmeldung bzw. – ohne Administrator – zur Einrichtung des ersten Administrators. Die Datenbank/das Schema muss auf dem Server bereits existieren und der Benutzer benötigt Rechte zum Anlegen von Tabellen (`CREATE`, `ALTER`, `INDEX`, `INSERT`, `SELECT`, `UPDATE`, `DELETE`).
- **Fehlermeldungen:** Sichere deutsche Hinweise (nicht erreichbar, Anmeldung fehlgeschlagen, Datenbank unbekannt, Zeitüberschreitung). Passwort und Verbindungszeichenfolge werden weder angezeigt noch protokolliert (Logs enthalten nur Fehlerkategorie, Ausnahmetyp und MySQL-Fehlercode). Eingaben werden validiert (Host/Datenbank nur Zeichenklassen, Längenlimits, Port 1–65535); die Verbindungszeichenfolge wird mit `MySqlConnectionStringBuilder` gebaut (kein Einschleusen weiterer Optionen). Anti-Forgery-Token, Request-Größenlimit (8 KB) und Rate-Limit (20 Anfragen/Minute je IP) schützen `/Setup`.
- **Speicherort:** `App_Data/db-settings.json` (außerhalb `wwwroot`), konfigurierbar über `Database:ConfigFile` bzw. `DB_CONFIG_FILE`. Gespeichert werden Host, Port, Datenbank, Benutzer, SSL-Modus und das **mit ASP.NET Core Data Protection verschlüsselte Passwort**; nie die Verbindungszeichenfolge. Der Schlüsselbund liegt in `App_Data/keys` (`DataProtection:KeysPath` bzw. `DATAPROTECTION_KEYS_PATH`). Datei und Schlüsselbund werden mit 0600/0700 angelegt (Unix).
- **Schutz und Sicherung:** Der Schlüsselbund wird auf Dateisystemebene **nicht** zusätzlich verschlüsselt (kein Zertifikat/DPAPI konfiguriert). Wer Konfigurationsdatei **und** Schlüsselbund lesen kann, kann das Passwort wiederherstellen. Beide Pfade nur für das Dienstkonto lesbar/schreibbar halten, nicht in dasselbe öffentliche Volume/Repository legen und gemeinsam (getrennt gesichert) sichern: Ohne Schlüsselbund ist die Datei nicht mehr entschlüsselbar. Für Produktion: Schlüsselschutz (z. B. X.509-Zertifikat, Key Vault) und ein Datenbankkonto mit minimalen Rechten einsetzen. `App_Data/` ist in `.gitignore`; nichts davon gehört ins Repository.
- **Start:** Konfiguration wird geladen/entschlüsselt, Verbindung geprüft und ausstehende Migrationen werden automatisch angewendet (Einzelinstanz-Annahme; bei mehreren Instanzen Migrationen separat ausführen). Ist der Server vorübergehend nicht erreichbar, erscheint `/Setup/Status` (HTTP 503, Wiederholung nach höchstens 5 Sekunden) – die gespeicherte Konfiguration bleibt unverändert. Ist die Konfiguration unlesbar/beschädigt oder nicht entschlüsselbar, wird die Anmeldung gesperrt und eine Wiederherstellungsseite gezeigt; die Datei wird nie automatisch überschrieben.
- **Wiederherstellung/Neukonfiguration:** Nach erfolgreicher Einrichtung ist `/Setup` nicht mehr erreichbar (404). Zum Ändern der Zugangsdaten lokal auf dem Server `db-settings.json` löschen (bei verlorenem Schlüsselbund ggf. auch den Ordner `keys`) und die App neu starten; danach beginnt die Einrichtung erneut. Eine Web-Oberfläche zur Neukonfiguration gibt es bewusst nicht.
- **Schema/Migrationen:** `Data/AppDbContext.cs` (Pomelo.EntityFrameworkCore.MySql 8.0.3, passend zu EF Core 8; MySqlConnector 2.3.5) ist die Persistenzgrundlage mit der Entität `Benutzer`; `Data/Migrations/InitialCreate` legt nur `__EFMigrationsHistory` an, `AddBenutzer` die Tabelle `Benutzer`. Neue Migration: `dotnet ef migrations add <Name> -o Data/Migrations` im Ordner `src/Kanzlei.Web` (Tool `dotnet-ef` 8.x). Das Farbthema bleibt in `App_Data/theme.css` und wird nicht in MySQL gespeichert.
- **Tests:** Laufen ohne MySQL mit Fakes (`TestSupport.cs`). Ein echter MySQL-Test ist nicht enthalten.


## Navigationstext (Tabelle `Einstellungen`)

Der Text oben links in der Navigation (Standard `Kanzlei-Prototyp`) ist unter **Einstellungen > Layout** (nur Administration) änderbar und liegt in MySQL.

- **Schema:** Tabelle `Einstellungen` (Migration `AddEinstellungen`, additiv): Singleton-Datensatz `Id = 1` (Check-Constraint `CK_Einstellungen_Singleton`), `Navigationstext` (max. 80 Zeichen), `ErstelltAm`, `GeaendertAm` (UTC). Keine weiteren Einstellungen.
- **Standard/Anlage:** Fehlt der Datensatz oder ist die Datenbank nicht erreichbar, wird `Kanzlei-Prototyp` angezeigt (fail-safe, keine Fehlerseite). Nach den Migrationen legt die Anwendung den Datensatz idempotent an; die Einrichtungsreihenfolge (Datenbank → Mailserver → erster Administrator → Anmeldung) bleibt unverändert.
- **Bearbeiten:** `POST /Einstellungen/Navigationstext` (Administration, Anti-Forgery). Validierung: Pflichtfeld, Leerraum an den Rändern wird entfernt, höchstens 80 Zeichen, keine Steuerzeichen, kein Markup (`<`, `>`, `&`); deutsche Fehlermeldung bei Fehlern. Die Ausgabe im Layout ist HTML-kodiert.
- **Zwischenspeicher:** `Services/BrandingService.cs` (Singleton) hält den Wert im Speicher, aktualisiert ihn beim Speichern und lädt ihn bei Bedarf aus MySQL (bei Fehlern Standardtext, Wiederholung frühestens nach 10 Sekunden). Einzelinstanz-Annahme, wie beim Farbthema.
- **Tests:** `tests/Kanzlei.Web.Tests/EinstellungenTests.cs` mit EF-Core-InMemory und Schema-/Migrationsprüfung über das MySQL-Provider-Skript; ein Test gegen einen echten MySQL-Server ist nicht enthalten.

## Mailserver (SMTP-Einstellungen)

Grundlage für ein späteres Passwort-Zurücksetzen. Es werden **keine** E-Mails versendet und es gibt keine Passwort-Zurücksetzen-Endpunkte.

- **Tabelle `MailEinstellungen`** (Migration `AddMailEinstellungen`, additiv): Singleton-Datensatz (`Id = 1`, per Check-Constraint erzwungen) mit `SmtpHost`, `SmtpPort`, `Benutzername`, `PasswortVerschluesselt`, `AbsenderEmail`, `AbsenderName`, `SslModus` (`Keine`, `StartTls`, `SslBeiVerbindung`), `Aktiv`, `Konfiguriert`, `LetzterTestAm`, `ErstelltAm`, `GeaendertAm` (UTC).
- **Ersteinrichtung:** Reihenfolge Datenbank → Mailserver (`/Setup/Mail`) → erster Administrator (`/Setup/Admin`) → Anmeldung. „Verbindung testen“ prüft nur; „Testen und speichern“ speichert **erst nach erfolgreichem Test**. Diese Seite ist wie die Datenbank-Einrichtung bewusst **unauthentifiziert** (es existiert noch kein Konto), aber nur im Status „Mailserver fehlt“ erreichbar; danach Weiterleitung bzw. 404. Wer die Anwendung vor Abschluss der Ersteinrichtung im Netz erreicht, kann den Mailserver festlegen – die Ersteinrichtung daher nur in vertrauenswürdigem Netz durchführen. Anti-Forgery, Request-Größenlimit (8 KB), Rate-Limit (20/Minute je IP) und Eingabevalidierung gelten.
- **Bearbeiten:** **Einstellungen > Mailserver** (nur Administration, serverseitig für jede Aktion geprüft, POST mit Anti-Forgery). Leeres Passwortfeld behält das gespeicherte Passwort (der Test nutzt es); gespeichertes Passwort/Chiffrat wird nie ausgegeben. Änderungen werden erst nach erfolgreichem Test gespeichert.
- **TLS/Test:** MailKit 4.18.1 (hinter `IMailConnectionTester`, in Tests ersetzt). Modi: STARTTLS (erforderlich, Standard, Port 587), SSL/TLS (Port 465) oder keine Verschlüsselung – Letzteres nur ohne Anmeldedaten. Zertifikate werden vom System geprüft. Verbindungsaufbau/Anmeldung sind auf 10 Sekunden begrenzt. Es wird nur verbunden und ggf. angemeldet. Meldungen sind deutsch; Passwörter, Ausnahmetexte und Verbindungszeichenfolgen werden nie angezeigt oder protokolliert.
- **Verschlüsselung:** Passwort per ASP.NET Core Data Protection (Purpose `Kanzlei.Web.MailSettings.Password.v1`) mit dem bereits persistierten Schlüsselbund (`App_Data/keys`) verschlüsselt in MySQL. Wer Datenbank **und** Schlüsselbund lesen kann, kann das Passwort wiederherstellen; beide getrennt schützen und gemeinsam sichern. Keine Zugangsdaten ins Repository.
- **Wiederherstellung/Grenzen:** Lässt sich das Passwort nicht entschlüsseln (Schlüsselbund fehlt/anderer Schlüsselbund) oder sind die Werte ungültig, wird fail-closed gemeldet: beim Start Statusseite `/Setup/Status` (HTTP 503), in der Einstellungsseite HTTP 503; der Datensatz wird nie überschrieben. Abhilfe: Schlüsselbund wiederherstellen, oder – bewusst – die Zeile in `MailEinstellungen` löschen und die App neu starten, um die Einrichtung neu zu beginnen. Ist der SMTP-Server nur vorübergehend nicht erreichbar, bleiben die Einstellungen unverändert und die App läuft weiter (nur der Test meldet einen Fehler). Die Erreichbarkeit wird nicht beim Start geprüft; `Aktiv` ist derzeit immer Ja; kein Test gegen echten SMTP-/MySQL-Server.

## Abhängigkeiten und Syncfusion-Lizenz

- NuGet: `Syncfusion.EJ2.AspNet.Core` 35.1.39 (Kalender-Widget `ejs-calendar`), `Pomelo.EntityFrameworkCore.MySql` 8.0.3, `MailKit` 4.18.1, `Microsoft.EntityFrameworkCore.Relational`/`.Design` 8.0.31, `Microsoft.AspNetCore.Mvc.Testing` (nur Tests). Bootstrap/jQuery liegen unter `wwwroot/lib`.
- Syncfusion-Skripte und -Styles (`ej2.min.js`, `bootstrap5.css`) werden von `cdn.syncfusion.com` in derselben Version geladen (bei Paket-Update die Version in `Views/Shared/_Layout.cshtml` mitziehen).
- Syncfusion ist kommerziell lizenziert (ggf. kostenlose Community-Lizenz). Ohne Lizenzschlüssel läuft die App, zeigt aber einen Lizenzhinweis. Schlüssel **nie committen**, sondern per Umgebungsvariable setzen:
  `export SYNCFUSION_LICENSE_KEY="<Ihr Schlüssel>"` (alternativ User-Secrets `Syncfusion:LicenseKey`).
- Bekannte Einschränkung: Der Kalender zeigt englische Monats-/Wochentagsnamen, solange keine deutschen Syncfusion-Lokalisierungsdaten eingebunden sind; Wochenstart ist Montag.
