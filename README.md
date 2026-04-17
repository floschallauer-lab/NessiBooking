# CourseBooking MVP

Schlanke Blazor-Server-Anwendung für Kursbuchungen mit Prioritätssystem, Adminbereich, Exporten und Seed-Daten.

## Stack

- .NET 9 / ASP.NET Core
- Blazor Server
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- FluentValidation
- MudBlazor
- Serilog

## Projektstruktur

- `CourseBooking.Web`: Blazor UI, Routing, Identity-Accountseiten, Export-Endpunkte
- `CourseBooking.Application`: DTOs, Service-Verträge, Validierung, Konstanten
- `CourseBooking.Domain`: Fachmodelle und Enums
- `CourseBooking.Infrastructure`: EF Core, Identity, Seeder, Services, Migrationen

## Enthaltene MVP-Funktionen

- Öffentliche Kursübersicht mit Filtern nach Bereich, Typ, Ort, Turnus, Wochentag und freien Plätzen
- Kursdetailseite mit Preis, Zeitraum, Kursleitung, Altersregel und Buchungsstatus
- Anmeldeformular mit Prioritätenliste, Turnuspräferenz, DSGVO/AGB, Notiz und Bestätigungsseite
- Prioritäts- und Regelprüfung über Application-/Infrastructure-Services
- Automatische Kurszuordnung im Adminbereich mit Ergebnisprotokoll und Wartelistenlogik
- Admin-Dashboard mit Kennzahlen
- Kursverwaltung, Anmeldungsübersicht, Detailansicht, Statuswechsel, manuelle Zuweisung
- Mailvorlagen mit Platzhaltern
- CSV-Export und Druckansicht pro Kurs
- Audit-Logging für Admin-Aktionen
- Demo-Seed für Kurse, Orte, Turnusse, Altersregeln, Anmeldungen und Adminzugang

## Schnellstart

1. PostgreSQL starten:

```bash
docker compose up -d db
```

2. Lösung bauen:

```bash
dotnet build CourseBooking.sln
```

3. Anwendung starten:

```bash
dotnet run --project CourseBooking.Web
```

4. Browser öffnen:

- Öffentlicher Bereich: `https://localhost:5001` oder die von `dotnet run` ausgegebene URL
- Adminbereich: `/admin`

## Demo-Admin

- E-Mail: `admin@coursebooking.local`
- Passwort: `Admin1234`

Die Werte können in `CourseBooking.Web/appsettings.json` unter `SeedAdmin` geändert werden.

## Datenbank

- Connection String: `CourseBooking.Web/appsettings.json`
- Provider: PostgreSQL
- Migrations: `CourseBooking.Infrastructure/Persistence/Migrations`

Die erste Migration `InitialCreate` ist bereits enthalten. Beim Start führt der Seeder `Database.Migrate()` und die Demo-Befüllung automatisch aus.

## E-Mail-Verhalten im MVP

- E-Mails werden nicht real versendet.
- Vorbereitete Nachrichten werden als Dateien unter `CourseBooking.Web/App_Data/SentEmails` abgelegt.
- So lassen sich Eingangsbestätigung, Zusage, Warteliste und Absage lokal prüfen.

## Wichtige URLs

- `/`
- `/courses/{id}`
- `/register`
- `/admin`
- `/admin/courses`
- `/admin/registrations`
- `/admin/templates`

## Hinweise zum Weiterbau

- Mandantenfähigkeit ist in der Architektur vorbereitet, aber noch nicht umgesetzt.
- Für Produktion fehlen noch echtes Mail-Gateway, feinere Rollen/Rechte, CI/CD und härtere Sicherheitseinstellungen.
- Das Prioritätssystem und die Fachregeln liegen bereits in Services und lassen sich dort gezielt erweitern.
