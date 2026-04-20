# NessiBooking

Kursbuchungs- und Anfrageanwendung fuer Schwimmkurse mit oeffentlichem Kundenbereich und einem bewusst einfachen Adminbereich fuer den taeglichen Betrieb.

## Produktstand

Die Anwendung ist auf zwei Zielgruppen ausgerichtet:

- Eltern und Kunden, die Kurse schnell finden und anfragen wollen
- Betreiber, die Kurse, Stammdaten und Anfragen ohne technische Huerden pflegen muessen

Der Schwerpunkt liegt auf einer ruhigen, klaren und wartbaren Verwaltungsoberflaeche.

## Technischer Stack

- .NET 9 / ASP.NET Core
- Blazor Server
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- FluentValidation
- MudBlazor
- Serilog

## Projektstruktur

- `CourseBooking.Web`: UI, Routing, Accountseiten, Export-Endpunkte
- `CourseBooking.Application`: DTOs, Service-Vertraege, Validierung, Konstanten
- `CourseBooking.Domain`: Fachmodelle und Enums
- `CourseBooking.Infrastructure`: Datenbank, Seeder, Migrationen, Services

## Funktionsumfang

### Kundenbereich

- Kursuebersicht mit Filtern fuer Bereich, Unterkategorie, Ort, Turnus, Wochentag und Buchbarkeit
- Kursdetailseite mit lesbaren Namen statt internen IDs
- Anfrageformular mit Prioritaeten, klarer Eingabefolge und verbesserter Uebersicht
- Bestaetigungsseite fuer gesendete Anfragen mit sauberer Rueckfuehrung zur Kursuebersicht
- Fachlich saubere Auswahl: Unterkategorien und Kurse passen sich an den gewaehlten Bereich an
- Robuste Formularlogik mit benutzerfreundlichen Fehlermeldungen statt roher Technik- oder Datenbankfehler

### Adminbereich

- `Dashboard` mit Kennzahlen, offenen Anfragen und schnellen Einstiegen
- `Kurse` fuer Suche, Filter, Bearbeitung, Aktiv/Inaktiv und Duplizieren
- `Neueingabe Kurs` als eigene Seite fuer das bewusste Anlegen neuer Kurse
- `Stammdaten` als zentraler Pflegeort fuer Dropdown-Vorlagen
- `Anfragen` fuer Eingang, Statuspflege und Detailansicht
- `Inhalte / Einstellungen` fuer Mailtexte und feste Hinweise
- Listen und Dashboards mit konsistenter Tabellenlogik, ruhigerem Umbruch und horizontalem Scrollen statt abgeschnittener Inhalte
- Bearbeitungsseiten mit staerkeren Busy-/Fehlerzustaenden und MudBlazor-orientierten Karten- und Formularmustern

### Stammdaten

Der Stammdatenbereich bildet die Grundbausteine fuer Kursformulare ab:

- Bereiche
- Unterkategorien
- Orte / Baeder
- Turnusse
- Altersregeln
- Kursleitungen

Alle Eintraege koennen zentral angelegt, bearbeitet, aktiviert/deaktiviert und kontrolliert geloescht werden.

## Fachliche Logik

- Unterkategorien sind fachlich an ihren Bereich gebunden
- Diese Zuordnung wird im Frontend und Backend validiert
- Fehlerhafte Kombinationen werden beim Anlegen und Bearbeiten von Kursen verhindert
- Bestehende Daten werden beim Start ueber Seeder- und Migrationslogik auf den aktuellen Stand gebracht
- Inaktive Stammdaten werden im Kundenbereich nicht mehr angeboten und im Adminbereich eindeutig gekennzeichnet
- Persistenzfehler werden defensiv abgefangen und als verstaendliche Hinweistexte zurueckgegeben

## Lokaler Start

1. PostgreSQL starten

```bash
docker compose up -d db
```

2. Loesung bauen

```bash
dotnet build CourseBooking.sln -v minimal
```

3. Anwendung starten

```bash
dotnet run --project CourseBooking.Web
```

4. Danach die im Terminal ausgegebene URL oeffnen

## Demo-Login fuer Admins

- E-Mail: `admin@coursebooking.local`
- Passwort: `Admin1234`

Die Seed-Daten lassen sich in `CourseBooking.Web/appsettings.json` anpassen.

## Wichtige Seiten

- `/`
- `/courses/{id}`
- `/register`
- `/admin`
- `/admin/courses`
- `/admin/courses/new`
- `/admin/catalog`
- `/admin/registrations`
- `/admin/templates`

## Datenbank und Migrationen

- Provider: PostgreSQL
- Migrationspfad: `CourseBooking.Infrastructure/Persistence/Migrations`
- Der Startprozess fuehrt Migrationen und Seed-Daten automatisch aus

## Hinweise fuer den Betrieb

- E-Mails werden lokal als Dateien unter `CourseBooking.Web/App_Data/SentEmails` abgelegt
- Der Adminbereich ist auf moeglichst einfache Standardablaeufe ausgelegt
- Fehlende Dropdown-Werte werden in `Stammdaten` gepflegt, nicht direkt in einzelnen Kursformularen
- Die UI orientiert sich bewusst an MudBlazor-Standardmustern statt an individuellen Sonderloesungen
