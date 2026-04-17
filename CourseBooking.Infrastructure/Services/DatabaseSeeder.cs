using System.Text;
using System.Text.Json;
using CourseBooking.Application.Constants;
using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Identity;
using CourseBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CourseBooking.Infrastructure.Services;

public sealed class DatabaseSeeder(
    CourseBookingDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    private const string CatalogSyncToken = "NessieSync:2026-04-17-neutral-catalog-v2";
    private const string SeedFilePath = "Seed/nessie-catalog-2026.json";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnsureRolesAndAdminAsync(cancellationToken);
        await EnsureEmailTemplatesAsync(cancellationToken);

        if (await IsCatalogUpToDateAsync(cancellationToken))
        {
            return;
        }

        var snapshot = await LoadSnapshotAsync(cancellationToken);

        await ResetCatalogDataAsync(cancellationToken);
        await SeedCatalogAsync(snapshot, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedSampleRegistrationsAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Nessie catalog seeded with {CourseCount} course offerings.", snapshot.Courses.Count);
    }

    private async Task<bool> IsCatalogUpToDateAsync(CancellationToken cancellationToken)
    {
        return await dbContext.CourseOfferings
            .AsNoTracking()
            .AnyAsync(x => x.InternalNotes.Contains(CatalogSyncToken), cancellationToken);
    }

    private async Task ResetCatalogDataAsync(CancellationToken cancellationToken)
    {
        dbContext.WaitlistEntries.RemoveRange(await dbContext.WaitlistEntries.ToListAsync(cancellationToken));
        dbContext.RegistrationPriorities.RemoveRange(await dbContext.RegistrationPriorities.ToListAsync(cancellationToken));
        dbContext.Registrations.RemoveRange(await dbContext.Registrations.ToListAsync(cancellationToken));
        dbContext.Guardians.RemoveRange(await dbContext.Guardians.ToListAsync(cancellationToken));
        dbContext.ChildParticipants.RemoveRange(await dbContext.ChildParticipants.ToListAsync(cancellationToken));
        dbContext.AuditLogs.RemoveRange(await dbContext.AuditLogs.ToListAsync(cancellationToken));
        dbContext.CourseOfferings.RemoveRange(await dbContext.CourseOfferings.ToListAsync(cancellationToken));
        dbContext.AgeRules.RemoveRange(await dbContext.AgeRules.ToListAsync(cancellationToken));
        dbContext.CourseTypes.RemoveRange(await dbContext.CourseTypes.ToListAsync(cancellationToken));
        dbContext.CourseCategories.RemoveRange(await dbContext.CourseCategories.ToListAsync(cancellationToken));
        dbContext.Venues.RemoveRange(await dbContext.Venues.ToListAsync(cancellationToken));
        dbContext.CourseCycles.RemoveRange(await dbContext.CourseCycles.ToListAsync(cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCatalogAsync(NessieCatalogSnapshot snapshot, CancellationToken cancellationToken)
    {
        var categories = snapshot.Courses
            .Select(x => x.CategoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select((name, index) => new CourseCategory
            {
                Name = FixImportedText(name),
                Slug = Slugify(name),
                SortOrder = index + 1,
                Description = BuildCategoryDescription(name)
            })
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var types = snapshot.Courses
            .GroupBy(x => $"{FixImportedText(x.CategoryName)}|{FixImportedText(x.TypeName)}", StringComparer.OrdinalIgnoreCase)
            .Select((group, index) =>
            {
                var keyParts = group.Key.Split('|', 2);
                var category = categories[keyParts[0]];
                return new CourseType
                {
                    Name = keyParts[1],
                    Slug = $"{category.Slug}-{Slugify(keyParts[1])}",
                    Description = $"Import aus Nessie-Katalog für {keyParts[1]}.",
                    OnlyBookableOnce = group.Any(x => x.OnlyBookableOnce),
                    SortOrder = index + 1,
                    CourseCategory = category
                };
            })
            .ToDictionary(x => $"{x.CourseCategory!.Name}|{x.Name}", StringComparer.OrdinalIgnoreCase);

        var venues = snapshot.Courses
            .GroupBy(x => BuildVenueLookupKey(x.VenueName, x.AddressLine1, x.PostalCode, x.City), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new Venue
                {
                    Name = FixImportedText(first.VenueName),
                    Slug = Slugify($"{FixImportedText(first.VenueName)}-{FixImportedText(first.PostalCode)}-{FixImportedText(first.City)}"),
                    AddressLine1 = FixImportedText(first.AddressLine1),
                    PostalCode = FixImportedText(first.PostalCode),
                    City = FixImportedText(first.City),
                    Notes = string.Join(" | ", group.Select(x => FixImportedText(x.VenueNotes)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                };
            })
            .ToDictionary(x => BuildVenueLookupKey(x.Name, x.AddressLine1, x.PostalCode, x.City), StringComparer.OrdinalIgnoreCase);

        var cycles = snapshot.Courses
            .GroupBy(x => x.CycleCode, StringComparer.OrdinalIgnoreCase)
            .Select((group, index) => new CourseCycle
            {
                Name = FixImportedText(group.First().CycleName),
                Code = group.First().CycleCode,
                Description = "Synchronisiert aus dem aktuellen Nessie-Katalog.",
                SortOrder = index + 1
            })
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var ageRules = snapshot.Courses
            .Where(x => x.AgeRule is not null)
            .GroupBy(x => $"{x.AgeRule!.Name}|{x.AgeRule.MinimumValue}|{x.AgeRule.MaximumValue}|{x.AgeRule.Unit}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First().AgeRule!;
                return new AgeRule
                {
                    Name = FixImportedText(first.Name),
                    MinimumValue = first.MinimumValue,
                    MaximumValue = first.MaximumValue,
                    Unit = Enum.Parse<AgeUnit>(first.Unit, ignoreCase: true),
                    Notes = "Regel aus Nessie-Katalog."
                };
            })
            .ToDictionary(x => $"{x.Name}|{x.MinimumValue}|{x.MaximumValue}|{x.Unit}", StringComparer.OrdinalIgnoreCase);

        dbContext.AddRange(categories.Values);
        dbContext.AddRange(types.Values);
        dbContext.AddRange(venues.Values);
        dbContext.AddRange(cycles.Values);
        dbContext.AddRange(ageRules.Values);

        foreach (var item in snapshot.Courses.OrderBy(x => x.StartDate).ThenBy(x => x.StartTime))
        {
            var categoryName = FixImportedText(item.CategoryName);
            var typeName = FixImportedText(item.TypeName);
            var venueKey = BuildVenueLookupKey(item.VenueName, item.AddressLine1, item.PostalCode, item.City);
            var ageRuleKey = item.AgeRule is null
                ? null
                : $"{FixImportedText(item.AgeRule.Name)}|{item.AgeRule.MinimumValue}|{item.AgeRule.MaximumValue}|{item.AgeRule.Unit}";

            dbContext.CourseOfferings.Add(new CourseOffering
            {
                CourseCategory = categories[categoryName],
                CourseType = types[$"{categoryName}|{typeName}"],
                Venue = venues[venueKey],
                CourseCycle = cycles[item.CycleCode],
                AgeRule = ageRuleKey is null ? null : ageRules[ageRuleKey],
                Title = FixImportedText(item.Title),
                Description = FixImportedText(item.Description),
                InstructorName = FixImportedText(item.InstructorName),
                CustomerNotice = FixImportedText(item.CustomerNotice),
                InternalNotes = $"{FixImportedText(item.InternalNotes)} | {CatalogSyncToken}".Trim(' ', '|'),
                Price = item.Price,
                Capacity = item.Capacity,
                StartDate = DateOnly.Parse(item.StartDate),
                EndDate = DateOnly.Parse(item.EndDate),
                DayOfWeek = Enum.Parse<DayOfWeek>(item.DayOfWeek, ignoreCase: true),
                StartTime = TimeOnly.Parse(item.StartTime),
                EndTime = TimeOnly.Parse(item.EndTime),
                Status = CourseOfferingStatus.Published,
                RegistrationMode = Enum.Parse<CourseRegistrationMode>(item.RegistrationMode, ignoreCase: true),
                AllowWaitlistWhenFull = item.AllowWaitlistWhenFull,
                ExternalRegistrationUrl = string.IsNullOrWhiteSpace(item.ExternalRegistrationUrl) ? null : item.ExternalRegistrationUrl
            });
        }
    }

    private async Task SeedSampleRegistrationsAsync(CancellationToken cancellationToken)
    {
        var internalCourses = await dbContext.CourseOfferings
            .Where(x => x.RegistrationMode == CourseRegistrationMode.Internal)
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.StartTime)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (internalCourses.Count < 3)
        {
            return;
        }

        dbContext.Registrations.Add(new Registration
        {
            Guardian = new Guardian
            {
                FullName = "Miriam Aigner",
                AddressLine1 = "Mühlkreisweg 14",
                PostalCode = "4040",
                City = "Linz",
                PhoneNumber = "0664 112233",
                Email = "miriam.aigner@example.com"
            },
            ChildParticipant = new ChildParticipant
            {
                FullName = "Theo Aigner",
                BirthDate = new DateOnly(2025, 12, 8)
            },
            Status = RegistrationStatus.Received,
            TermsAccepted = true,
            PrivacyAccepted = true,
            SubmittedAtUtc = DateTime.UtcNow.AddHours(-9),
            LastStatusChangedAtUtc = DateTime.UtcNow.AddHours(-9),
            Priorities = new List<RegistrationPriority>
            {
                new() { CourseOffering = internalCourses[0], PriorityOrder = 1 },
                new() { CourseOffering = internalCourses[1], PriorityOrder = 2 }
            }
        });

        dbContext.Registrations.Add(new Registration
        {
            Guardian = new Guardian
            {
                FullName = "Klaus Berger",
                AddressLine1 = "Traunuferstraße 7",
                PostalCode = "4050",
                City = "Traun",
                PhoneNumber = "0664 445566",
                Email = "klaus.berger@example.com"
            },
            ChildParticipant = new ChildParticipant
            {
                FullName = "Lia Berger",
                BirthDate = new DateOnly(2022, 4, 11)
            },
            Status = RegistrationStatus.Waitlisted,
            TermsAccepted = true,
            PrivacyAccepted = true,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-1),
            LastStatusChangedAtUtc = DateTime.UtcNow.AddHours(-16),
            Priorities = new List<RegistrationPriority>
            {
                new() { CourseOffering = internalCourses[1], PriorityOrder = 1 }
            },
            WaitlistEntries = new List<WaitlistEntry>
            {
                new() { CourseOffering = internalCourses[1], Position = 1, Reason = "Beispielhafte Warteliste aus Seed-Daten" }
            }
        });

        dbContext.Registrations.Add(new Registration
        {
            Guardian = new Guardian
            {
                FullName = "Sabine Gruber",
                AddressLine1 = "Donaulände 5",
                PostalCode = "4020",
                City = "Linz",
                PhoneNumber = "0664 778899",
                Email = "sabine.gruber@example.com"
            },
            ChildParticipant = new ChildParticipant
            {
                FullName = "Jonas Gruber",
                BirthDate = new DateOnly(2019, 9, 2)
            },
            Status = RegistrationStatus.Confirmed,
            AssignedCourseOffering = internalCourses[2],
            TermsAccepted = true,
            PrivacyAccepted = true,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-5),
            LastStatusChangedAtUtc = DateTime.UtcNow.AddDays(-4),
            AssignmentProtocol = "Seed-Zuweisung an ersten verfügbaren Kurs.",
            Priorities = new List<RegistrationPriority>
            {
                new() { CourseOffering = internalCourses[2], PriorityOrder = 1 }
            }
        });
    }

    private async Task EnsureEmailTemplatesAsync(CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new EmailTemplateDefinition(
                EmailTemplateKeys.RegistrationReceived,
                "Eingangsbestätigung",
                "Nach erfolgreichem Formularversand.",
                "Ihre Anmeldung für {{Kursname}} ist eingegangen",
                "Hallo {{Name}},\n\nvielen Dank für Ihre Anmeldung. Wir prüfen jetzt Ihre Prioritäten und melden uns mit der passenden Kurszusage.\n\nKurs: {{Kursname}}\nBad: {{Bad}}\nTag: {{Wochentag}}\nUhrzeit: {{Uhrzeit}}\nTurnus: {{Turnus}}\n"),
            new EmailTemplateDefinition(
                EmailTemplateKeys.RegistrationAccepted,
                "Zusage",
                "Bei fixer Kurszuteilung.",
                "Zusage für {{Kursname}}",
                "Hallo {{Name}},\n\nwir freuen uns, Ihnen einen Platz in {{Kursname}} bestätigen zu können.\nBad: {{Bad}}\nTag: {{Wochentag}}\nUhrzeit: {{Uhrzeit}}\nTurnus: {{Turnus}}\n"),
            new EmailTemplateDefinition(
                EmailTemplateKeys.RegistrationWaitlisted,
                "Warteliste",
                "Wenn aktuell kein Platz verfügbar ist.",
                "Warteliste für {{Kursname}}",
                "Hallo {{Name}},\n\naktuell ist kein fixer Platz frei. Wir haben Ihre Anmeldung auf die Warteliste für {{Kursname}} gesetzt.\n"),
            new EmailTemplateDefinition(
                EmailTemplateKeys.RegistrationRejected,
                "Absage",
                "Bei Absage oder manueller Ablehnung.",
                "Aktualisierung zu Ihrer Anmeldung",
                "Hallo {{Name}},\n\nleider können wir aktuell keinen Platz anbieten. Bei Fragen melden Sie sich gerne direkt bei uns.\n")
        };

        foreach (var definition in definitions)
        {
            var template = await dbContext.EmailTemplates.FirstOrDefaultAsync(x => x.Key == definition.Key, cancellationToken);
            if (template is null)
            {
                dbContext.EmailTemplates.Add(new EmailTemplate
                {
                    Key = definition.Key,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    SubjectTemplate = definition.Subject,
                    BodyTemplate = definition.Body
                });
                continue;
            }

            template.DisplayName = definition.DisplayName;
            template.Description = definition.Description;
            template.SubjectTemplate = definition.Subject;
            template.BodyTemplate = definition.Body;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRolesAndAdminAsync(CancellationToken cancellationToken)
    {
        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(RoleNames.Admin));
        }

        var adminEmail = configuration["SeedAdmin:Email"] ?? "admin@coursebooking.local";
        var adminPassword = configuration["SeedAdmin:Password"] ?? "Admin1234";
        var adminName = configuration["SeedAdmin:DisplayName"] ?? "Demo Admin";

        var user = await userManager.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = adminName
            };

            var createResult = await userManager.CreateAsync(user, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Admin-User konnte nicht angelegt werden: {errors}");
            }
        }
        else
        {
            user.UserName = adminEmail;
            user.Email = adminEmail;
            user.EmailConfirmed = true;
            user.DisplayName = adminName;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Admin-User konnte nicht aktualisiert werden: {errors}");
            }

            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, adminPassword);
            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Admin-Passwort konnte nicht gesetzt werden: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            await userManager.AddToRoleAsync(user, RoleNames.Admin);
        }
    }

    private async Task<NessieCatalogSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, SeedFilePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Seed-Datei '{filePath}' wurde nicht gefunden.");
        }

        await using var stream = File.OpenRead(filePath);
        var snapshot = await JsonSerializer.DeserializeAsync<NessieCatalogSnapshot>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Nessie-Katalog konnte nicht gelesen werden.");
        }

        snapshot.Courses = snapshot.Courses
            .Select(course => course with
            {
                CategoryName = FixImportedText(course.CategoryName),
                TypeName = FixImportedText(course.TypeName),
                VenueName = FixImportedText(course.VenueName),
                AddressLine1 = FixImportedText(course.AddressLine1),
                PostalCode = FixImportedText(course.PostalCode),
                City = FixImportedText(course.City),
                VenueNotes = FixImportedText(course.VenueNotes),
                CycleName = FixImportedText(course.CycleName),
                Title = FixImportedText(course.Title),
                Description = FixImportedText(course.Description),
                InstructorName = FixImportedText(course.InstructorName),
                CustomerNotice = FixImportedText(course.CustomerNotice),
                InternalNotes = FixImportedText(course.InternalNotes),
                AgeRule = course.AgeRule is null
                    ? null
                    : course.AgeRule with { Name = FixImportedText(course.AgeRule.Name) }
            })
            .ToList();

        return snapshot;
    }

    private static string BuildCategoryDescription(string categoryName) => categoryName switch
    {
        "Babyschwimmen" => "Aktuelle Nessie-Kurse für Wassergewöhnung mit den Kleinsten.",
        "Kinderschwimmen" => "Begleitete Wassergewöhnung und erste Bewegungsabläufe im Wasser.",
        "Schwimmen lernen" => "Schwimmkurse für Kinder mit steigendem Schwierigkeitsgrad.",
        "Erwachsene" => "Schwimmangebote für Erwachsene.",
        "Intensivkurse" => "Kompakte Ferien- und Intensivformate.",
        _ => "Kurse aus dem aktuellen Nessie-Katalog."
    };

    private static string FixImportedText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value;
        if (normalized.Contains("Ã", StringComparison.Ordinal) || normalized.Contains("â", StringComparison.Ordinal))
        {
            normalized = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(normalized));
        }

        if (normalized.Contains("\u00C3", StringComparison.Ordinal) || normalized.Contains("\u00E2", StringComparison.Ordinal))
        {
            normalized = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(normalized));
        }

        return normalized
            .Replace("Anf?nger", "Anfänger", StringComparison.Ordinal)
            .Replace("Platzverf?gbarkeit", "Platzverfügbarkeit", StringComparison.Ordinal)
            .Replace("Best?tigung", "Bestätigung", StringComparison.Ordinal)
            .Replace("f?r", "für", StringComparison.Ordinal)
            .Replace("J?nner", "Jänner", StringComparison.Ordinal)
            .Replace("M?rz", "März", StringComparison.Ordinal)
            .Replace("?ber", "\u00FCber", StringComparison.Ordinal)
            .Replace("2j?hrige", "2j\u00E4hrige", StringComparison.Ordinal)
            .Replace("3j?hrige", "3j\u00E4hrige", StringComparison.Ordinal)
            .Replace("2-4j?hrige", "2-4j\u00E4hrige", StringComparison.Ordinal)
            .Replace("4-5j?hrige", "4-5j\u00E4hrige", StringComparison.Ordinal)
            .Replace(" O?", " O\u00D6", StringComparison.Ordinal)
            .Trim();
    }

    private static string BuildVenueLookupKey(string venueName, string addressLine1, string postalCode, string city)
    {
        return string.Join(
            "|",
            NormalizeLookupToken(venueName),
            NormalizeLookupToken(addressLine1),
            NormalizeLookupToken(postalCode),
            NormalizeLookupToken(city));
    }

    private static string NormalizeLookupToken(string value)
    {
        var normalized = FixImportedText(value)
            .Replace("–", "-", StringComparison.Ordinal)
            .Replace("—", "-", StringComparison.Ordinal);

        normalized = string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.ToUpperInvariant();
    }

    private static string Slugify(string value)
    {
        var normalized = value
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);

        normalized = string.Concat(normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }

    private sealed record EmailTemplateDefinition(
        string Key,
        string DisplayName,
        string Description,
        string Subject,
        string Body);

    private sealed class NessieCatalogSnapshot
    {
        public string GeneratedAt { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<NessieCourseSeed> Courses { get; set; } = new();
    }

    private sealed record NessieCourseSeed(
        string CategoryName,
        string TypeName,
        bool OnlyBookableOnce,
        string VenueName,
        string AddressLine1,
        string PostalCode,
        string City,
        string VenueNotes,
        string CycleName,
        string CycleCode,
        string Title,
        string Description,
        string InstructorName,
        string CustomerNotice,
        string InternalNotes,
        decimal Price,
        int Capacity,
        string StartDate,
        string EndDate,
        string DayOfWeek,
        string StartTime,
        string EndTime,
        string Status,
        string RegistrationMode,
        bool AllowWaitlistWhenFull,
        string? ExternalRegistrationUrl,
        string SourceUrl,
        NessieAgeRuleSeed? AgeRule);

    private sealed record NessieAgeRuleSeed(
        string Name,
        int? MinimumValue,
        int? MaximumValue,
        string Unit);
}
