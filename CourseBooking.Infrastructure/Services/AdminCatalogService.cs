using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services;

internal sealed class AdminCatalogService(
    CourseBookingDbContext dbContext,
    IAuditService auditService) : IAdminCatalogService
{
    public async Task<AdminCatalogDataDto> GetManagementDataAsync(CancellationToken cancellationToken = default)
    {
        var categoryTypeCounts = await dbContext.CourseTypes
            .AsNoTracking()
            .GroupBy(x => x.CourseCategoryId)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var categoryCourseCounts = await dbContext.CourseOfferings
            .AsNoTracking()
            .GroupBy(x => x.CourseCategoryId)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var courseTypeCourseCounts = await dbContext.CourseOfferings
            .AsNoTracking()
            .GroupBy(x => x.CourseTypeId)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var venueCourseCounts = await dbContext.CourseOfferings
            .AsNoTracking()
            .GroupBy(x => x.VenueId)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var cycleCourseCounts = await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(x => x.CourseCycleId.HasValue)
            .GroupBy(x => x.CourseCycleId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var ageRuleCourseCounts = await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(x => x.AgeRuleId.HasValue)
            .GroupBy(x => x.AgeRuleId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var instructorCourseCounts = await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(x => x.CourseInstructorId.HasValue)
            .GroupBy(x => x.CourseInstructorId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var categories = await dbContext.CourseCategories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryManagementRowDto(
                x.Id,
                x.Name,
                x.Description,
                x.SortOrder,
                x.IsActive,
                categoryTypeCounts.GetValueOrDefault(x.Id),
                categoryCourseCounts.GetValueOrDefault(x.Id)))
            .ToListAsync(cancellationToken);

        var courseTypes = await dbContext.CourseTypes
            .AsNoTracking()
            .Include(x => x.CourseCategory)
            .OrderBy(x => x.CourseCategory!.SortOrder)
            .ThenBy(x => x.CourseCategory!.Name)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CourseTypeManagementRowDto(
                x.Id,
                x.CourseCategoryId,
                x.CourseCategory!.Name,
                x.Name,
                x.Description,
                x.OnlyBookableOnce,
                x.SortOrder,
                x.IsActive,
                courseTypeCourseCounts.GetValueOrDefault(x.Id)))
            .ToListAsync(cancellationToken);

        var venues = await dbContext.Venues
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new VenueManagementRowDto(
                x.Id,
                x.Name,
                x.AddressLine1,
                x.PostalCode,
                x.City,
                x.Notes,
                x.IsActive,
                venueCourseCounts.GetValueOrDefault(x.Id)))
            .ToListAsync(cancellationToken);

        var cycles = await dbContext.CourseCycles
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CourseCycleManagementRowDto(
                x.Id,
                x.Name,
                x.Code,
                x.Description,
                x.SortOrder,
                x.IsActive,
                cycleCourseCounts.GetValueOrDefault(x.Id)))
            .ToListAsync(cancellationToken);

        var ageRules = await dbContext.AgeRules
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AgeRuleManagementRowDto(
                x.Id,
                x.Name,
                x.MinimumValue,
                x.MaximumValue,
                x.Unit.ToString(),
                x.Notes,
                x.IsActive,
                ageRuleCourseCounts.GetValueOrDefault(x.Id)))
            .ToListAsync(cancellationToken);

        var instructors = await dbContext.CourseInstructors
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.FullName)
            .Select(x => new CourseInstructorManagementRowDto(
                x.Id,
                x.FullName,
                x.Description,
                x.SortOrder,
                x.IsActive,
                instructorCourseCounts.GetValueOrDefault(x.Id)))
            .ToListAsync(cancellationToken);

        return new AdminCatalogDataDto(categories, courseTypes, venues, cycles, ageRules, instructors);
    }

    public async Task<Guid> SaveCategoryAsync(CourseCategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireText(request.Name, "Bitte geben Sie einen Bereichsnamen ein.", 120);
        var description = (request.Description ?? string.Empty).Trim();

        if (await dbContext.CourseCategories.AnyAsync(
                x => x.Id != request.Id && x.Name.ToUpper() == name.ToUpper(),
                cancellationToken))
        {
            throw new InvalidOperationException("Ein Bereich mit diesem Namen ist bereits vorhanden.");
        }

        var entity = request.Id.HasValue
            ? await dbContext.CourseCategories.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Bereich nicht gefunden.")
            : new CourseCategory();

        entity.Name = name;
        entity.Description = description;
        entity.SortOrder = Math.Max(request.SortOrder, 0);
        entity.IsActive = request.IsActive;
        entity.Slug = await BuildUniqueSlugAsync(dbContext.CourseCategories, name, request.Id, cancellationToken);
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!request.Id.HasValue)
        {
            dbContext.CourseCategories.Add(entity);
        }

        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Der Bereich konnte nicht gespeichert werden. Bitte pruefen Sie die Eingaben und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("CategorySaved", nameof(CourseCategory), entity.Id.ToString(), entity.Name, cancellationToken);
        return entity.Id;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CourseCategories
            .Include(x => x.CourseTypes)
            .Include(x => x.CourseOfferings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Bereich nicht gefunden.");

        if (entity.CourseTypes.Count > 0 || entity.CourseOfferings.Count > 0)
        {
            throw new InvalidOperationException("Der Bereich ist noch in Verwendung und kann deshalb nicht geloescht werden. Bitte stattdessen auf inaktiv stellen.");
        }

        dbContext.CourseCategories.Remove(entity);
        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Der Bereich konnte nicht geloescht werden. Bitte laden Sie die Stammdaten neu und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("CategoryDeleted", nameof(CourseCategory), entity.Id.ToString(), entity.Name, cancellationToken);
    }

    public async Task<Guid> SaveCourseTypeAsync(CourseTypeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.CourseCategories.FirstOrDefaultAsync(x => x.Id == request.CourseCategoryId, cancellationToken)
            ?? throw new InvalidOperationException("Bitte waehlen Sie zuerst einen passenden Bereich.");

        var name = RequireText(request.Name, "Bitte geben Sie einen Namen fuer die Unterkategorie ein.", 120);
        var description = (request.Description ?? string.Empty).Trim();

        if (await dbContext.CourseTypes.AnyAsync(
                x => x.Id != request.Id &&
                     x.CourseCategoryId == request.CourseCategoryId &&
                     x.Name.ToUpper() == name.ToUpper(),
                cancellationToken))
        {
            throw new InvalidOperationException("Diese Unterkategorie existiert in dem gewaehlten Bereich bereits.");
        }

        var entity = request.Id.HasValue
            ? await dbContext.CourseTypes.Include(x => x.CourseOfferings).FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Unterkategorie nicht gefunden.")
            : new CourseType();

        if (request.Id.HasValue &&
            entity.CourseCategoryId != Guid.Empty &&
            entity.CourseCategoryId != request.CourseCategoryId &&
            entity.CourseOfferings.Count > 0)
        {
            throw new InvalidOperationException("Eine Unterkategorie mit bereits zugeordneten Kursen kann nicht in einen anderen Bereich verschoben werden.");
        }

        entity.CourseCategoryId = category.Id;
        entity.Name = name;
        entity.Description = description;
        entity.OnlyBookableOnce = request.OnlyBookableOnce;
        entity.SortOrder = Math.Max(request.SortOrder, 0);
        entity.IsActive = request.IsActive;
        entity.Slug = await BuildUniqueSlugAsync(dbContext.CourseTypes, $"{category.Slug}-{name}", request.Id, cancellationToken);
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!request.Id.HasValue)
        {
            dbContext.CourseTypes.Add(entity);
        }

        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Die Unterkategorie konnte nicht gespeichert werden. Bitte pruefen Sie die Angaben und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("CourseTypeSaved", nameof(CourseType), entity.Id.ToString(), entity.Name, cancellationToken);
        return entity.Id;
    }

    public async Task DeleteCourseTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CourseTypes
            .Include(x => x.CourseOfferings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Unterkategorie nicht gefunden.");

        if (entity.CourseOfferings.Count > 0)
        {
            throw new InvalidOperationException("Die Unterkategorie ist noch Kursen zugeordnet und kann nicht geloescht werden. Bitte stattdessen auf inaktiv stellen.");
        }

        dbContext.CourseTypes.Remove(entity);
        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Die Unterkategorie konnte nicht geloescht werden. Bitte laden Sie die Stammdaten neu und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("CourseTypeDeleted", nameof(CourseType), entity.Id.ToString(), entity.Name, cancellationToken);
    }

    public async Task<Guid> SaveVenueAsync(VenueUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireText(request.Name, "Bitte geben Sie einen Ortsnamen ein.", 120);
        var address = RequireText(request.AddressLine1, "Bitte geben Sie die Adresse ein.", 180);
        var postalCode = RequireText(request.PostalCode, "Bitte geben Sie die Postleitzahl ein.", 20);
        var city = RequireText(request.City, "Bitte geben Sie den Ort an.", 80);
        var notes = (request.Notes ?? string.Empty).Trim();

        var entity = request.Id.HasValue
            ? await dbContext.Venues.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Ort nicht gefunden.")
            : new Venue();

        entity.Name = name;
        entity.AddressLine1 = address;
        entity.PostalCode = postalCode;
        entity.City = city;
        entity.Notes = notes;
        entity.IsActive = request.IsActive;
        entity.Slug = await BuildUniqueSlugAsync(dbContext.Venues, $"{name}-{postalCode}-{city}", request.Id, cancellationToken);
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!request.Id.HasValue)
        {
            dbContext.Venues.Add(entity);
        }

        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Der Ort konnte nicht gespeichert werden. Bitte pruefen Sie die Angaben und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("VenueSaved", nameof(Venue), entity.Id.ToString(), entity.Name, cancellationToken);
        return entity.Id;
    }

    public async Task DeleteVenueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Venues
            .Include(x => x.CourseOfferings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Ort nicht gefunden.");

        if (entity.CourseOfferings.Count > 0)
        {
            throw new InvalidOperationException("Der Ort ist noch Kursen zugeordnet und kann nicht geloescht werden. Bitte stattdessen auf inaktiv stellen.");
        }

        dbContext.Venues.Remove(entity);
        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Der Ort konnte nicht geloescht werden. Bitte laden Sie die Stammdaten neu und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("VenueDeleted", nameof(Venue), entity.Id.ToString(), entity.Name, cancellationToken);
    }

    public async Task<Guid> SaveCycleAsync(CourseCycleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireText(request.Name, "Bitte geben Sie einen Turnusnamen ein.", 120);
        var code = RequireText(request.Code, "Bitte geben Sie ein eindeutiges Kurzzeichen ein.", 40).ToUpperInvariant();
        var description = (request.Description ?? string.Empty).Trim();

        if (await dbContext.CourseCycles.AnyAsync(x => x.Id != request.Id && x.Code.ToUpper() == code, cancellationToken))
        {
            throw new InvalidOperationException("Dieses Kurzzeichen wird bereits verwendet.");
        }

        var entity = request.Id.HasValue
            ? await dbContext.CourseCycles.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Turnus nicht gefunden.")
            : new CourseCycle();

        entity.Name = name;
        entity.Code = code;
        entity.Description = description;
        entity.SortOrder = Math.Max(request.SortOrder, 0);
        entity.IsActive = request.IsActive;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!request.Id.HasValue)
        {
            dbContext.CourseCycles.Add(entity);
        }

        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Der Turnus konnte nicht gespeichert werden. Bitte pruefen Sie die Angaben und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("CycleSaved", nameof(CourseCycle), entity.Id.ToString(), entity.Name, cancellationToken);
        return entity.Id;
    }

    public async Task DeleteCycleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cycle = await dbContext.CourseCycles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Turnus nicht gefunden.");

        var hasLinkedCourses = await dbContext.CourseOfferings.AnyAsync(x => x.CourseCycleId == id, cancellationToken);
        var hasLinkedRegistrations = await dbContext.Registrations.AnyAsync(x => x.PreferredCourseCycleId == id, cancellationToken);
        if (hasLinkedCourses || hasLinkedRegistrations)
        {
            throw new InvalidOperationException("Der Turnus wird noch verwendet und kann nicht geloescht werden. Bitte stattdessen auf inaktiv stellen.");
        }

        dbContext.CourseCycles.Remove(cycle);
        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Der Turnus konnte nicht geloescht werden. Bitte laden Sie die Stammdaten neu und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("CycleDeleted", nameof(CourseCycle), cycle.Id.ToString(), cycle.Name, cancellationToken);
    }

    public async Task<Guid> SaveAgeRuleAsync(AgeRuleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireText(request.Name, "Bitte geben Sie einen Namen fuer die Altersregel ein.", 120);
        var notes = (request.Notes ?? string.Empty).Trim();
        if (!Enum.TryParse<AgeUnit>(request.Unit, ignoreCase: true, out var unit))
        {
            throw new InvalidOperationException("Bitte waehlen Sie eine gueltige Einheit fuer die Altersregel.");
        }

        if (request.MinimumValue.HasValue && request.MaximumValue.HasValue && request.MinimumValue > request.MaximumValue)
        {
            throw new InvalidOperationException("Der minimale Wert darf nicht groesser als der maximale Wert sein.");
        }

        var entity = request.Id.HasValue
            ? await dbContext.AgeRules.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Altersregel nicht gefunden.")
            : new AgeRule();

        entity.Name = name;
        entity.MinimumValue = request.MinimumValue;
        entity.MaximumValue = request.MaximumValue;
        entity.Unit = unit;
        entity.Notes = notes;
        entity.IsActive = request.IsActive;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!request.Id.HasValue)
        {
            dbContext.AgeRules.Add(entity);
        }

        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Die Altersregel konnte nicht gespeichert werden. Bitte pruefen Sie die Angaben und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("AgeRuleSaved", nameof(AgeRule), entity.Id.ToString(), entity.Name, cancellationToken);
        return entity.Id;
    }

    public async Task DeleteAgeRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AgeRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Altersregel nicht gefunden.");

        if (await dbContext.CourseOfferings.AnyAsync(x => x.AgeRuleId == id, cancellationToken))
        {
            throw new InvalidOperationException("Die Altersregel wird noch in Kursen verwendet und kann nicht geloescht werden.");
        }

        dbContext.AgeRules.Remove(entity);
        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Die Altersregel konnte nicht geloescht werden. Bitte laden Sie die Stammdaten neu und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("AgeRuleDeleted", nameof(AgeRule), entity.Id.ToString(), entity.Name, cancellationToken);
    }

    public async Task<Guid> SaveInstructorAsync(CourseInstructorUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var fullName = RequireText(request.FullName, "Bitte geben Sie einen Namen fuer die Kursleitung ein.", 160);
        var description = (request.Description ?? string.Empty).Trim();

        if (await dbContext.CourseInstructors.AnyAsync(
                x => x.Id != request.Id && x.FullName.ToUpper() == fullName.ToUpper(),
                cancellationToken))
        {
            throw new InvalidOperationException("Diese Kursleitung ist bereits vorhanden.");
        }

        var entity = request.Id.HasValue
            ? await dbContext.CourseInstructors.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Kursleitung nicht gefunden.")
            : new CourseInstructor();

        entity.FullName = fullName;
        entity.Description = description;
        entity.SortOrder = Math.Max(request.SortOrder, 0);
        entity.IsActive = request.IsActive;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!request.Id.HasValue)
        {
            dbContext.CourseInstructors.Add(entity);
        }

        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Die Kursleitung konnte nicht gespeichert werden. Bitte pruefen Sie die Angaben und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("InstructorSaved", nameof(CourseInstructor), entity.Id.ToString(), entity.FullName, cancellationToken);
        return entity.Id;
    }

    public async Task DeleteInstructorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CourseInstructors.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Kursleitung nicht gefunden.");

        if (await dbContext.CourseOfferings.AnyAsync(x => x.CourseInstructorId == id, cancellationToken))
        {
            throw new InvalidOperationException("Die Kursleitung wird noch in Kursen verwendet und kann nicht geloescht werden.");
        }

        dbContext.CourseInstructors.Remove(entity);
        await PersistenceGuard.SaveChangesAsync(
            dbContext,
            "Die Kursleitung konnte nicht geloescht werden. Bitte laden Sie die Stammdaten neu und versuchen Sie es erneut.",
            cancellationToken);
        await auditService.WriteAsync("InstructorDeleted", nameof(CourseInstructor), entity.Id.ToString(), entity.FullName, cancellationToken);
    }

    private static string RequireText(string? value, string errorMessage, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(errorMessage);
        }

        if (text.Length > maxLength)
        {
            throw new InvalidOperationException($"Der Wert darf maximal {maxLength} Zeichen lang sein.");
        }

        return text;
    }

    private static async Task<string> BuildUniqueSlugAsync<TEntity>(
        IQueryable<TEntity> query,
        string baseValue,
        Guid? existingId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var baseSlug = Slugify(baseValue);
        var candidate = string.IsNullOrWhiteSpace(baseSlug) ? "eintrag" : baseSlug;
        var suffix = 2;
        var currentId = existingId ?? Guid.Empty;
        var existingSlugs = await query
            .Select(entity => new
            {
                Id = EF.Property<Guid>(entity, "Id"),
                Slug = EF.Property<string>(entity, "Slug")
            })
            .ToListAsync(cancellationToken);

        while (existingSlugs.Any(x => x.Id != currentId && x.Slug == candidate))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var normalized = value
            .Trim()
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
}
