# Anforderungsspezifikation – Steuerberater-Webapplikation

- **Repository:** `NormanBI/TEST2`
- **Dokumentversion:** 1.0
- **Stand:** 11. Oktober 2026
- **Status:** Anforderungen für den ersten Prototyp dokumentiert
- **Sprache der Anwendung und Dokumentation:** Deutsch

## 1. Zweck und Ziel

Es soll ein Prototyp einer Mehrbenutzer-Webapplikation für Mitarbeitende einer Steuerkanzlei entstehen. Perspektivisch soll die Anwendung Daten aus DATEV auswerten. Im ersten Schritt wird der Anmelde- und Startseitenablauf mit Navigation und einem leeren Kalenderwidget demonstriert.

Diese Datei ist das fortlaufende Anforderungsdokument. Sie hält den vereinbarten Umfang, Annahmen, Abnahmekriterien und spätere Änderungen nachvollziehbar fest.

## 2. Umfang des ersten Prototyps

Enthalten:

- Anmeldung mit Demo-Benutzern für Administration, Steuerberater und Sachbearbeiter.
- Startseite nach erfolgreicher Anmeldung.
- Leeres Kalenderwidget auf der Startseite.
- Menüpunkt **Stammdaten** und Menüpunkt **Auswertungen** für alle im Prototyp unterstützten Rollen.
- Menüpunkt **Einstellungen** ausschließlich für die Rolle Administration.
- Die Menüziele sind zunächst Platzhalterseiten.
- Deutsche und responsive Darstellung für Smartphone und Desktop.
- Standardfarben.
- Keine dauerhafte Datenhaltung; ausschließlich Demo-/flüchtige Daten.

Nicht enthalten:

- Echte DATEV-Anbindung oder DATEV-Datenverarbeitung.
- Funktionale Stammdatenverwaltung, Auswertungen oder Einstellungen.
- Kalendertermine und persistente Kalenderdaten.
- Mandanten-Zugänge bzw. die Rolle Mandant.
- Datenbank-Persistenz von Farben (seit v1.2 änderbar, seit v1.3 dateibasiert als CSS gespeichert, siehe F-009).
- Einsatz mit echten Kanzlei- oder Mandantendaten bzw. produktionsreifer Betrieb.

## 3. Technische Vorgaben

- **Plattform:** .NET 8 / ASP.NET Core
- **Architektur:** MVC
- **UI-Komponentenbibliothek:** Syncfusion Library
- **Datenhaltung:** im ersten Prototyp keine dauerhafte Datenhaltung
- **Lokalisierung:** Deutsch
- **Darstellung:** responsive, einschließlich Smartphone

Die konkreten Syncfusion-Komponenten, Authentifizierungsdetails und Ausführungs-/Hostingvorgaben sind noch nicht festgelegt.

## 4. Rollen und Berechtigungen

| Rolle | Im Prototyp enthalten | Sichtbare Menüpunkte |
|---|---:|---|
| Administration | Ja | Stammdaten, Auswertungen, Einstellungen (Layout, Benutzer) |
| Eigentümer | Ja (Rolle vorhanden, noch keine eigenen Berechtigungen, ab v1.5) | Noch nicht festgelegt |
| Steuerberater | Ja | Stammdaten, Auswertungen |
| Sachbearbeiter | Ja | Stammdaten, Auswertungen |
| Mandant | Nein, für später vorgemerkt | Noch nicht festgelegt |

Mehrbenutzerbetrieb bedeutet im fachlichen Ziel, dass Mitarbeitende derselben Kanzlei die Anwendung verwenden. Der Prototyp verwendet Demo-Benutzer; ein produktiver, persistenter Kanzlei-/Mandanten-Tenantbetrieb ist damit nicht umgesetzt.

## 5. Funktionale Anforderungen

### F-001 – Anmeldung mit Demo-Benutzern

Die Anwendung stellt Demo-Benutzer für Administration, Steuerberater und Sachbearbeiter bereit.

**Abnahmekriterien:**
- Jede der drei Rollen kann mit einem vorbereiteten Demo-Konto angemeldet werden.
- Eine erfolgreiche Anmeldung führt zur Startseite.
- Die konkreten Demo-Benutzernamen und Zugangsdaten werden bei der Implementierung dokumentiert.

### F-002 – Startseite mit Kalenderwidget

Nach der Anmeldung wird eine Startseite mit einem Kalenderwidget angezeigt. Der Kalender ist im ersten Prototyp leer.

**Abnahmekriterien:**
- Das Kalenderwidget ist auf der Startseite sichtbar.
- Es enthält zunächst keine Termine.
- Die Startseite funktioniert in Smartphone- und Desktopansicht.

### F-003 – Grundmenü

Das Hauptmenü enthält die Punkte **Stammdaten** und **Auswertungen**.

**Abnahmekriterien:**
- Beide Menüpunkte sind für Administration, Steuerberater und Sachbearbeiter sichtbar.
- Jeder Menüpunkt öffnet eine Platzhalterseite.

### F-004 – Administrationsmenü

Der Menüpunkt **Einstellungen** ist ausschließlich für Administration vorgesehen.

**Abnahmekriterien:**
- Nach Anmeldung als Administration ist **Einstellungen** im Menü sichtbar.
- Nach Anmeldung als Steuerberater oder Sachbearbeiter ist **Einstellungen** nicht sichtbar.
- Der Menüpunkt öffnet im Prototyp eine Platzhalterseite.

### F-005 – Platzhalterseiten

Die Bereiche Stammdaten, Auswertungen und Einstellungen dienen zunächst nur als Platzhalter.

**Abnahmekriterien:**
- Die Seiten sind über die jeweils sichtbaren Menüpunkte erreichbar.
- Es wird keine fachliche Bearbeitungs- oder Auswertungsfunktion vorausgesetzt.

### F-006 – Responsive, deutsche Benutzeroberfläche

Anmeldung, Startseite, Kalender und Menü sind deutschsprachig und auf Smartphone sowie Desktop nutzbar.

**Abnahmekriterien:**
- Sichtbare Beschriftungen und Meldungen sind auf Deutsch.
- Die genannten Ansichten lassen sich ohne wesentlichen Funktionsverlust auf schmalen und breiten Bildschirmen bedienen.

### F-007 – Keine dauerhafte Datenhaltung

Der erste Prototyp benötigt keine dauerhafte Speicherung.

**Abnahmekriterien:**
- Die Erfüllung des Prototyp-Szenarios hängt nicht von einer Datenbank oder persistenten Nutzerdaten ab.
- Es werden keine echten DATEV-, Kanzlei- oder Mandantendaten verwendet.

### F-008 – Standardfarben

Der erste Prototyp verwendet Standardfarben.

**Abnahmekriterien:**
- Eine Auswahl oder Speicherung eigener Farbthemen ist nicht erforderlich.

### F-009 – Layout-Editor (Farbthema) für Administration

Unter **Einstellungen > Layout** (`/Einstellungen/Layout`) kann die Administration die Farben der Anwendung über semantische CSS-Variablen ändern. Diese Anforderung erweitert F-008 (Standardfarben bleiben die Voreinstellung) und löst die Abgrenzung „Einstellbare Farben nicht enthalten“ ab.

**Abnahmekriterien:**
- Verschachtelter Menüpunkt **Einstellungen > Layout** nur für die Rolle Administration sichtbar. Serverseitig erzwungen: `GET`/`POST` auf `/Einstellungen/Layout` und `/Einstellungen/LayoutReset` sind für Nicht-Administration nicht erreichbar (Weiterleitung zu `/Account/AccessDenied`, HTTP 403), für Anonyme Weiterleitung zur Anmeldung. POST-Aktionen sind per Anti-Forgery-Token geschützt.
- Deutsche, responsive Seite mit Beschriftung, Farbwähler und Hex-Eingabe je Token, sichtbarem Fokus, Live-Vorschau und Kontrasthinweis (Haupttext zu Hintergrund/Fläche, empfohlen ≥ 4,5:1; nur Hinweis, keine Sperre).
- Farbpalette (Token → CSS-Variable, Standardwert): Primär `--app-primary` `#1f4e79`; Sekundär/Akzent `--app-accent` `#2a7f9e`; Seitenhintergrund `--app-background` `#f5f7fa`; Fläche/Karte `--app-surface` `#ffffff`; Haupttext `--app-text` `#212529`; gedämpfter Text `--app-muted` `#5c636a`; Rahmen/Trenner `--app-border` `#dee2e6`; Link `--app-link` `#0b5ed7`; Erfolg `--app-success` `#198754`; Warnung `--app-warning` `#ffc107`; Fehler/Gefahr `--app-danger` `#dc3545`; Fokusmarkierung `--app-focus` `#0d6efd`; Kalender heute/Auswahl `--app-calendar` `#1f4e79`. Die Kontrastfarben auf Primär/Kalender (`--app-primary-contrast`, `--app-calendar-contrast`) werden automatisch berechnet.
- Validierung strikt: nur `#rrggbb` (Hex, 6 Stellen). Ungültige oder fehlende Werte werden abgelehnt (HTTP 400, Fehlermeldung am Feld); das bisherige Thema bleibt vollständig unverändert.
- Auslieferung: Die Farben werden als `:root`-CSS-Variablen über `/theme.css` (dynamisch, nicht gecacht, nur validierte Hex-Werte) für alle Benutzer inkl. Anmeldeseite ausgeliefert. `wwwroot/css/site.css` wird nie zur Laufzeit geschrieben und verwendet nur die Variablen; Syncfusion-Kalender (heute/Auswahl, Fläche, Text) nutzen die Variablen.
- **Dateibasierte Persistenz (seit v1.3):** „Übernehmen“ schreibt das validierte Thema als vollständiges CSS-Dokument (nur bekannte `--app-*`-Variablen, `#rrggbb`) atomar (temporäre Datei, dann Ersetzen) nach `App_Data/theme.css` unter dem Content-Root (außerhalb `wwwroot`); Pfad konfigurierbar über `THEME_CSS_FILE` bzw. `Theme:CssFile`. Beim Start wird die Datei geladen; Einstellungen überstehen daher Neustarts. Fehlt die Datei: Standardfarben. Beschädigte Datei oder unbekannte/unzulässige Deklarationen: Warnung im Log, Standardfarben, kein fremdes CSS wird ausgeliefert. „Zurücksetzen“ speichert die Standardfarben. Schlägt das Schreiben fehl, bleibt das bisherige Thema aktiv (Fehlermeldung). Betrieb: Schreibrechte auf das Zielverzeichnis nötig; nur eine Instanz unterstützt (mehrere Instanzen benötigen gemeinsamen Speicher/Koordination). Keine Datenbank. Details im README.
- **Zurücksetzen:** „Auf Standard zurücksetzen“ stellt sofort alle Standardwerte (siehe Palette) für alle Benutzer wieder her.
- Automatisierte Tests: Autorisierung (inkl. Direktzugriff Nicht-Administration), Farbvalidierung, Anwenden/Zurücksetzen, CSS-/Theme-Auslieferung (`tests/Kanzlei.Web.Tests/ThemeTests.cs`), Persistenz/Neustart/Korruptionsschutz (`ThemePersistenceTests.cs`).

### F-010 – MySQL-Anbindung und Ersteinrichtung

Die Anwendung fragt ohne gültige MySQL-Konfiguration vor der Anmeldung die Verbindungsparameter ab, speichert sie nach erfolgreichem Test verschlüsselt und nutzt MySQL (EF Core 8, Pomelo) als Persistenzgrundlage.

**Abnahmekriterien:**
- Ohne Konfiguration führen alle Seiten (inkl. Anmeldung) auf `/Setup`; Formular auf Deutsch, responsiv, mit Validierung, Verbindungstest, SSL-Option und sicheren Fehlermeldungen ohne Zugangsdaten.
- Gespeichert wird nur nach erfolgreichem Test und erfolgreicher Migration; das Passwort ist mit Data Protection verschlüsselt, der Schlüsselbund ist persistiert; Pfade konfigurierbar (Standard `App_Data`).
- Nach der Einrichtung ist `/Setup` nicht mehr erreichbar; Neukonfiguration nur lokal durch Löschen der Konfigurationsdatei (dokumentiert in README).
- Beim Start: Laden, Verbindungsprüfung, automatische Migrationen. Unlesbare Konfiguration oder nicht erreichbarer Server führen zu einer Status-/Wiederherstellungsseite statt zur Anmeldung und überschreiben nichts.
- Farbthema (F-009) und Demo-Benutzer (F-001) bleiben unverändert und unabhängig von MySQL. Annahmen: Einzelinstanz, lokales Dateisystem.

### F-011 – Benutzerverwaltung mit Datenbankkonten (löst die Demo-Benutzer aus F-001 ab)

Benutzerkonten liegen in MySQL (Tabelle `Benutzer`) und werden unter Einstellungen > Benutzer (nur Administration) verwaltet.

**Abnahmekriterien:**
- Felder: Vorname, Nachname, Emailadresse (Pflicht, eindeutig, groß-/kleinschreibungsunabhängig), Kundennutzer (Standard Nein), Passwort, Administrator, Eigentümer, Steuerberater, Sachbearbeiter, Aktiv (Standard Ja), Zeitstempel.
- Passwörter werden **nicht umkehrbar verschlüsselt**, sondern mit `IPasswordHasher<Benutzer>` gesalzen und adaptiv gehasht (Spalte `PasswortHash`); Hash und Passwort werden nie angezeigt, protokolliert oder erneut ausgegeben. Passwortrichtlinie: 12–128 Zeichen, Groß-/Kleinbuchstaben, Ziffer.
- Erster Administrator: Ohne aktiven Administrator verlangt die Einrichtung (`/Setup/Admin`) Vorname, Nachname, Emailadresse und Passwort; idempotent, keine festen oder committeten Standardpasswörter.
- Anmeldung gegen die Datenbank; Rollen-Claims ergeben sich aus den vier Rollen-Flags, Kundennutzer ist keine Rolle; Benutzer ohne Rolle erhalten keine Rollen-Claims.
- Liste, Anlegen, Aktivieren/Deaktivieren; **kein Löschen** (keine Route, kein Button). Deaktivierte Benutzer können sich nicht anmelden, bestehende Sitzungen enden bei der nächsten Anfrage. Der letzte aktive Administrator kann nicht deaktiviert werden.
- Serverseitige Autorisierung (nur Administration) für alle Aktionen; POSTs mit Anti-Forgery-Token; deutsche, responsive Oberfläche mit deutschen Feldfehlern (Pflichtfelder, E-Mail-Format, Passwortrichtlinie, doppelte Emailadresse).
- Migration `AddBenutzer` (additiv, MySQL/Pomelo). Bekannte Grenzen: kein Bearbeiten/Passwort-Zurücksetzen, keine Rollenänderung nach dem Anlegen, Sperre für die Letzter-Administrator-Regel nur innerhalb einer Instanz.

### F-012 – Mailserver-Einstellungen (Grundlage für späteres Passwort-Zurücksetzen)

SMTP-Einstellungen liegen in MySQL (Tabelle `MailEinstellungen`, Singleton-Datensatz `Id = 1`, Check-Constraint) und werden beim ersten Start sowie unter Einstellungen > Mailserver (nur Administration) gepflegt. Passwort-Zurücksetzen selbst (E-Mails, Endpunkte) ist **nicht** Teil dieser Anforderung.

**Abnahmekriterien:**
- Spalten: `Id`, `SmtpHost`, `SmtpPort`, `Benutzername`, `PasswortVerschluesselt`, `AbsenderEmail`, `AbsenderName`, `SslModus` (`Keine`/`StartTls`/`SslBeiVerbindung`), `Aktiv`, `Konfiguriert`, `LetzterTestAm`, `ErstelltAm`, `GeaendertAm`. Migration `AddMailEinstellungen` (additiv, kein `DROP`).
- Einrichtungsreihenfolge: 1. Datenbank (`/Setup`), 2. Mailserver (`/Setup/Mail`), 3. erster Administrator (`/Setup/Admin`), 4. Anmeldung. Fehlen gültige Mail-Einstellungen, leiten alle Seiten (außer Theme/statische Dateien) auf `/Setup/Mail` um; nach Abschluss ist die Seite nicht mehr erreichbar. Keine Weiterleitungsschleifen.
- Gespeichert wird nur nach erfolgreichem SMTP-Test (Verbindung, TLS, ggf. Anmeldung; es wird keine E-Mail versendet). Fehlgeschlagene Tests speichern nichts; Meldungen sind deutsch und enthalten weder Passwort noch Ausnahmetexte; Logs enthalten nur Fehlerkategorie und Ausnahmetyp.
- Anmeldedaten sind nur mit STARTTLS oder SSL/TLS zulässig. Verbindungsaufbau und Test sind auf 10 Sekunden begrenzt.
- Das Passwort wird mit ASP.NET Core Data Protection (Purpose `Kanzlei.Web.MailSettings.Password.v1`, persistierter Schlüsselbund) verschlüsselt in MySQL gespeichert; Klartext und Chiffrat werden nie an die Oberfläche ausgegeben. Zugriff auf Datenbank **und** Schlüsselbund ermöglicht die Wiederherstellung des Passworts.
- Unauthentifizierte Ersteinrichtung ist ausdrücklich so festgelegt (es gibt noch kein Konto, wie bei der Datenbank-Einrichtung): nur im Status „Mailserver fehlt“, danach 404/Weiterleitung. Anti-Forgery, 8-KB-Größenlimit und Rate-Limit (20/Minute je IP) gelten.
- Bearbeiten unter Einstellungen > Mailserver: serverseitig nur Administration (alle Aktionen), Anti-Forgery; leeres Passwortfeld behält das gespeicherte Passwort; Test vor Speichern.
- Kann das Passwort nicht entschlüsselt werden (z. B. Schlüsselbund fehlt), wird fail-closed gemeldet (Statusseite bzw. HTTP 503 auf der Bearbeitungsseite) und der Datensatz nie überschrieben. Ist der SMTP-Server vorübergehend nicht erreichbar, bleiben die Einstellungen unverändert und die Anwendung läuft weiter; nur der Test schlägt fehl.
- Bekannte Grenzen: Erreichbarkeit des Mailservers wird nicht beim Start geprüft; `Aktiv` wird derzeit immer auf Ja gesetzt; kein Test mit echtem SMTP-/MySQL-Server in CI; nur Einzelinstanzbetrieb.

### F-013 – Navigationstext (Tabelle `Einstellungen`)

Der Text oben links in der Navigation (Standard „Kanzlei-Prototyp“) wird in MySQL (Tabelle `Einstellungen`) gespeichert und unter Einstellungen > Layout (nur Administration) bearbeitet. Weitere Einstellungen sind nicht Teil dieser Anforderung; das Passwort-Zurücksetzen bleibt ausgenommen.

**Abnahmekriterien:**
- Tabelle `Einstellungen`: Singleton-Datensatz (`Id = 1`, Check-Constraint `CK_Einstellungen_Singleton`) mit `Navigationstext` (max. 80 Zeichen), `ErstelltAm`, `GeaendertAm` (UTC). Migration `AddEinstellungen` (additiv, kein `DROP`).
- Ohne Datensatz (oder bei Datenbankfehler) gilt der Standardtext „Kanzlei-Prototyp“. Der Datensatz wird nach den Migrationen idempotent angelegt, ohne die Reihenfolge Datenbank → Mailserver → erster Administrator → Anmeldung zu verändern; ein Fehler dabei blockiert die Einrichtung nicht.
- Validierung serverseitig: Pflichtfeld, Leerraum an den Rändern wird entfernt, höchstens 80 Zeichen, keine Steuerzeichen, kein Markup (`<`, `>`, `&`); deutsche Fehlermeldungen (HTTP 400, nichts wird gespeichert). Die Ausgabe wird HTML-kodiert.
- `POST /Einstellungen/Navigationstext` ist nur für Administration erreichbar (Nicht-Administration: `/Account/AccessDenied`, Anonyme: Anmeldung) und per Anti-Forgery geschützt; es gibt keine öffentliche Bearbeitungsroute.
- Der Wert wird in einem Singleton-Dienst zwischengespeichert (keine Datenbankabfrage je Seitenaufruf), beim Speichern aktualisiert und überlebt Neustarts. Einzelinstanz-Annahme.

## 6. Demo-Abnahmeszenario

1. Demo-Administration anmelden.
2. Prüfen, dass die Startseite ein leeres Kalenderwidget sowie **Stammdaten**, **Auswertungen** und **Einstellungen** zeigt.
3. Die drei Menüpunkte öffnen und jeweils die Platzhalterseite prüfen.
4. Abmelden und als Demo-Steuerberater anmelden.
5. Prüfen, dass **Stammdaten** und **Auswertungen** sichtbar und **Einstellungen** nicht sichtbar ist.
6. Die Prüfung mit dem Demo-Sachbearbeiter wiederholen.
7. Anmeldung, Navigation und Startseite in Smartphone- und Desktopansicht prüfen.

## 7. Annahmen und Abgrenzungen

- „Leere Seite“ bedeutet: Startseite ohne fachliche Inhalte, aber mit dem gewünschten Kalenderwidget und Menü.
- „Keine Datenhaltung“ bedeutet keine dauerhafte Speicherung im ersten Prototyp. Die konkrete technische Behandlung flüchtiger Sitzungs-/Demo-Zustände ist bei der Implementierung festzulegen.
- Die Rolle Mandant wurde als spätere Möglichkeit genannt, ist aber explizit nicht Bestandteil der ersten Version.
- DATEV-Auswertungen sind ein späteres Ziel; Umfang, DATEV-Schnittstelle und benötigte Auswertungstypen sind noch nicht definiert.
- Standardfarben genügen. Konkrete Farbwerte, Logo und Branding sind nicht vorgegeben.
- Der Prototyp ist nicht zur Verarbeitung echter Steuer- oder personenbezogener Daten vorgesehen.

## 8. Noch zu klärende Punkte

Diese Punkte blockieren die dokumentierten Basisanforderungen nicht, sollten aber vor beziehungsweise während der Implementierung entschieden werden:

1. Welche konkreten Demo-Benutzernamen und Passwörter werden verwendet?
2. Wie werden Demo-Authentifizierung und Demo-Rollen technisch umgesetzt?
3. Welche Ansichten und Interaktionen soll das Syncfusion-Kalenderwidget unterstützen? Aktuell ist nur ein leeres Widget gefordert.
4. Welcher genaue Text oder welches Layout soll auf den Platzhalterseiten erscheinen?
5. Welche Standardfarben, welches Logo und welche sonstigen Branding-Vorgaben gelten?
6. Welche DATEV-Daten und Auswertungen sollen in einer späteren Version unterstützt werden?
7. Welche Anforderungen gelten später für Hosting, Sicherheit, Datenschutz und dauerhafte Datenhaltung?

## 9. Fortlaufendes Änderungsprotokoll

Bei jeder Anforderungsänderung wird ein neuer Eintrag mit Datum und Dokumentversion ergänzt. Frühere Entscheidungen werden nicht stillschweigend überschrieben: Änderungen sollen als neue Anforderung, Anpassung oder Ablösung kenntlich gemacht werden. Bei einer Änderung sind betroffene Anforderungs-IDs und Abnahmekriterien zu aktualisieren.

| Version | Datum | Änderung |
|---|---|---|
| 0.1 | 11. Oktober 2026 | Ausgangsziel und technische Vorgaben festgehalten: Mehrbenutzer-Webanwendung für Steuerberater, MVC, .NET 8 und Syncfusion. |
| 0.2 | 11. Oktober 2026 | Demo-Anmeldung, Kanzlei-Mitarbeitende, Rollenentwurf, Menü, DATEV-Ziel, Demodaten und keine Datenhaltung ergänzt. |
| 0.3 | 11. Oktober 2026 | Leeres Kalenderwidget, Platzhalterseiten, Admin-spezifisches Menü, Mandantenrolle für später und Standardfarben bestätigt. |
| 1.0 | 11. Oktober 2026 | Anforderungen zusammengeführt, IDs und Abnahmekriterien ergänzt, Umfang des ersten Prototyps abgegrenzt und Demo-Abnahmeszenario dokumentiert. |

| 1.1 | 2026-10-11 | Prototyp umgesetzt (siehe README). Entscheidungen zu Abschnitt 8: Demo-Zugänge `admin`, `steuerberater`, `sachbearbeiter` (Passwörter im README); Cookie-Authentifizierung mit festen Demo-Benutzern und serverseitiger Rollenprüfung; Kalender = Syncfusion `ejs-calendar` ohne Termine; Platzhaltertext „Platzhalter – diese Funktion ist im ersten Prototyp noch nicht umgesetzt.“; Standardfarben Primär `#1f4e79`, Hintergrund `#f5f7fa`, Text `#212529`. | F-001 bis F-009 |
| 1.2 | 2026-10-11 | Layout-Editor unter Einstellungen > Layout (nur Administration): semantische CSS-Variablen, strikte Hex-Validierung, Übernehmen/Zurücksetzen, gemeinsames Thema ausschließlich im Arbeitsspeicher (Reset bei Neustart, bewusst keine Datenbank, kein Schreiben in wwwroot). Ablösung der Abgrenzung „Einstellbare und gespeicherte Farben“ für den flüchtigen Fall; dauerhafte Speicherung bleibt später. | F-004, F-008, F-009 |
| 1.3 | 2026-10-11 | Dateibasierte Persistenz des Farbthemas: CSS-Datei `App_Data/theme.css` (konfigurierbar über `THEME_CSS_FILE`), atomares Schreiben, strenge Validierung beim Laden mit sicherem Fallback auf Standardfarben; Einstellungen überstehen Neustarts. Nur Einzelinstanzbetrieb. Ersetzt die rein flüchtige Speicherung aus v1.2. | F-009 |
| 1.4 | 2026-10-11 | MySQL-Anbindung: Ersteinrichtung vor der Anmeldung, Test-vor-Speichern, Passwort per Data Protection verschlüsselt in `App_Data/db-settings.json` mit persistiertem Schlüsselbund, EF-Core-Kontext (Pomelo 8.0.3) mit initialer Migration, automatische Migration beim Start, Status-/Wiederherstellungsseite. Ergänzt F-007 (Datenbank für spätere Fachdaten; Demo-Benutzer und Theme-Datei unverändert). Nur Einzelinstanzbetrieb. | F-007, F-010 |
| 1.5 | 2026-10-11 | Benutzerverwaltung: Tabelle `Benutzer` (Migration `AddBenutzer`), Passwort als gesalzener Einweg-Hash statt umkehrbarer Verschlüsselung, Ersteinrichtung des ersten Administrators über `/Setup/Admin` (keine festen Zugangsdaten), Anmeldung gegen die Datenbank, Menü Einstellungen > Benutzer, Deaktivieren statt Löschen, Schutz des letzten Administrators, Sitzungsende bei Deaktivierung. Löst die festen Demo-Benutzer aus v1.1 (F-001) ab; frühere Einträge bleiben unverändert. | F-001, F-004, F-007, F-011 |
| 1.6 | 2026-10-11 | Mailserver-Einstellungen: Tabelle `MailEinstellungen` (Migration `AddMailEinstellungen`), Ersteinrichtung `/Setup/Mail` zwischen Datenbank und Administrator, Test-vor-Speichern (MailKit), Passwort per Data Protection verschlüsselt, Einstellungen > Mailserver (nur Administration), fail-closed bei nicht entschlüsselbarem Passwort. Kein Passwort-Zurücksetzen. | F-010, F-011, F-012 |
| 1.7 | 2026-10-11 | Navigationstext: Tabelle `Einstellungen` (Migration `AddEinstellungen`, Singleton), Standard „Kanzlei-Prototyp“, Bearbeitung unter Einstellungen > Layout (nur Administration), serverseitige Validierung, HTML-Kodierung, zwischengespeicherte Anzeige in der gemeinsamen Navigation, Fallback auf Standardtext. Kein Passwort-Zurücksetzen. | F-009, F-013 |
| 1.8 | 2026-10-11 | Einstellungen-Übersicht als Kachel-Dashboard mit Syncfusion `ejs-dashboardlayout` (Bootstrap-5-Stil, App-Farbvariablen): je eine klickbare Kachel für Layout, Benutzer und Mailserver; Verschieben/Größenändern deaktiviert, 2 Spalten, einspaltig ab 767 px; Platzhaltertext entfernt, Zugriff weiterhin nur Administration (serverseitig). | F-004, F-009 |

### Vorlage für künftige Änderungsprotokolleinträge

| Version | Datum | Änderung | Betroffene IDs |
|---|---|---|---|
| x.y | JJJJ-MM-TT | Kurzbeschreibung der Entscheidung oder Änderung | F-xxx, ... |
