using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CourseBooking.Infrastructure.Services;

internal sealed class AdminCourseService(
    CourseBookingDbContext dbContext,
    IValidator<CourseUpsertRequest> validator,
    IAuditService auditService) : IAdminCourseService
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-AT");

    public async Task<CourseManagementDataDto> GetManagementDataAsync(CancellationToken cancellationToken = default)
    {
        var occupancy = await dbContext.Registrations
            .AsNoTracking()
            .Where(x => x.AssignedCourseOfferingId.HasValue && (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved))
            .GroupBy(x => x.AssignedCourseOfferingId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var courses = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(x => x.CourseCategory)
            .Include(x => x.CourseType)
                .ThenInclude(x => x!.CourseCategory)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .Include(x => x.CourseInstructor)
            .OrderBy(x => x.StartDate)
            .ToListAsync(cancellationToken);

        var rows = courses.Select(x => new CourseManagementRowDto(
            x.Id,
            x.Title,
            x.CourseType?.CourseCategory?.Name ?? x.CourseCategory?.Name ?? string.Empty,
            x.CourseType?.Name ?? string.Empty,
            x.Venue?.Name ?? string.Empty,
            x.CourseCycle?.Name,
            x.CourseInstructor?.FullName ?? x.InstructorName,
            GermanCulture.DateTimeFormat.GetDayName(x.DayOfWeek),
            $"{x.StartDate:dd.MM.yyyy} - {x.EndDate:dd.MM.yyyy}",
            x.Status,
            x.RegistrationMode,
            x.Capacity,
            x.Capacity - occupancy.GetValueOrDefault(x.Id),
            x.Status == CourseOfferingStatus.Archived)).ToList();

        var summary = new CourseManagementSummaryDto(
            rows.Count,
            rows.Count(x => x.Status == CourseOfferingStatus.Published),
            rows.Count(x => x.Status == CourseOfferingStatus.Draft),
            rows.Count(x => x.RegistrationMode == CourseRegistrationMode.Internal),
            rows.Count(x => x.RegistrationMode == CourseRegistrationMode.External),
            rows.Count(x => x.Status == CourseOfferingStatus.SoldOut || (x.RegistrationMode == CourseRegistrationMode.Internal && x.SeatsRemaining <= 0)),
            rows.Count(x => x.Archived),
            courses.Count(x => CourseOfferingPolicy.HasLowAvailability(x, occupancy.GetValueOrDefault(x.Id))));

        return new CourseManagementDataDto(
            rows,
            await dbContext.CourseCategories
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new LookupItemDto(x.Id, x.Name, null))
                .ToListAsync(cancellationToken),
            await LoadCanonicalCourseTypesAsync(cancellationToken),
            await LoadCanonicalVenuesAsync(cancellationToken),
            await dbContext.CourseCycles
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new LookupItemDto(x.Id, x.Name, null))
                .ToListAsync(cancellationToken),
            await dbContext.AgeRules
                .AsNoTracking()
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Name)
                .Select(x => new LookupItemDto(x.Id, x.IsActive ? x.Name : $"{x.Name} (inaktiv)", null))
                .ToListAsync(cancellationToken),
            await dbContext.CourseInstructors
                .AsNoTracking()
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.FullName)
                .Select(x => new LookupItemDto(x.Id, x.IsActive ? x.FullName : $"{x.FullName} (inaktiv)", null))
                .ToListAsync(cancellationToken),
            summary);
    }

    public async Task<CourseUpsertRequest?> GetCourseForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CourseUpsertRequest
            {
                Id = x.Id,
                CourseCategoryId = x.CourseCategoryId,
                CourseTypeId = x.CourseTypeId,
                VenueId = x.VenueId,
                CourseCycleId = x.CourseCycleId,
                AgeRuleId = x.AgeRuleId,
                CourseInstructorId = x.CourseInstructorId,
                Title = x.Title,
                Description = x.Description,
                CustomerNotice = x.CustomerNotice,
                Price = x.Price,
                Capacity = x.Capacity,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Status = x.Status,
                RegistrationMode = x.RegistrationMode,
                AllowWaitlistWhenFull = x.AllowWaitlistWhenFull,
                ExternalRegistrationUrl = x.ExternalRegistrationUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return null;
        }

        request.CourseCategoryId = await ResolveCategoryIdForEditAsync(request.CourseCategoryId, request.CourseTypeId, cancellationToken);
        request.CourseTypeId = await CanonicalizeCourseTypeIdAsync(request.CourseTypeId, request.CourseCategoryId, cancellationToken);
        request.VenueId = await CanonicalizeVenueIdAsync(request.VenueId, cancellationToken);
        return request;
    }

    public async Task<CourseUpsertRequest?> GetCourseForDuplicateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await GetCourseForEditAsync(id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        request.Id = null;
        request.Title = $"{request.Title} (Kopie)";
        request.Status = CourseOfferingStatus.Draft;
        return request;
    }

    public async Task<Guid> SaveAsync(CourseUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var selectedCourseType = await dbContext.CourseTypes
            .AsNoTracking()
            .Where(x => x.Id == request.CourseTypeId)
            .Select(x => new
            {
                x.CourseCategoryId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidationException("Die gewaehlte Unterkategorie wurde nicht gefunden.");

        if (selectedCourseType.CourseCategoryId != request.CourseCategoryId)
        {
            throw new ValidationException("Die gewaehlte Unterkategorie gehoert nicht zum ausgewaehlten Bereich.");
        }

        if (!await dbContext.CourseCategories.AnyAsync(x => x.Id == request.CourseCategoryId, cancellationToken))
        {
            throw new ValidationException("Der gewaehlte Bereich wurde nicht gefunden.");
        }

        if (!await dbContext.Venues.AnyAsync(x => x.Id == request.VenueId, cancellationToken))
        {
            throw new ValidationException("Der gewaehlte Ort wurde nicht gefunden.");
        }

        if (request.CourseCycleId.HasValue &&
            !await dbContext.CourseCycles.AnyAsync(x => x.Id == request.CourseCycleId.Value, cancellationToken))
        {
            throw new ValidationException("Der gewaehlte Turnus wurde nicht gefunden.");
        }

        if (request.AgeRuleId.HasValue &&
            !await dbContext.AgeRules.AnyAsync(x => x.Id == request.AgeRuleId.Value, cancellationToken))
        {
            throw new ValidationException("Die gewaehlte Altersregel wurde nicht gefunden.");
        }

        if (request.CourseInstructorId.HasValue &&
            !await dbContext.CourseInstructors.AnyAsync(x => x.Id == request.CourseInstructorId.Value, cancellationToken))
        {
            throw new ValidationException("Die gewaehlte Kursleitung wurde nicht gefunden.");
        }

        CourseOffering entity;
        if (request.Id.HasValue)
        {
            entity = await dbContext.CourseOfferings.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Kurs nicht gefunden.");
        }
        else
        {
            entity = new CourseOffering();
            dbContext.CourseOfferings.Add(entity);
        }

        entity.CourseCategoryId = selectedCourseType.CourseCategoryId;
        entity.CourseTypeId = await CanonicalizeCourseTypeIdAsync(request.CourseTypeId, selectedCourseType.CourseCategoryId, cancellationToken);
        entity.VenueId = await CanonicalizeVenueIdAsync(request.VenueId, cancellationToken);
        entity.CourseCycleId = request.CourseCycleId;
        entity.AgeRuleId = request.AgeRuleId;
        entity.CourseInstructorId = request.CourseInstructorId;
        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.InstructorName = request.CourseInstructorId.HasValue
            ? await dbContext.CourseInstructors
                .AsNoTracking()
                .Where(x => x.Id == request.CourseInstructorId.Value)
                .Select(x => x.FullName)
                .FirstAsync(cancellationToken)
            : string.Empty;
        entity.CustomerNotice = request.CustomerNotice.Trim();
        entity.Price = request.Price;
        entity.Capacity = request.Capacity;
        entity.StartDate = request.StartDate!.Value;
        entity.EndDate = request.EndDate!.Value;
        entity.DayOfWeek = request.DayOfWeek;
        entity.StartTime = request.StartTime!.Value;
        entity.EndTime = request.EndTime!.Value;
        entity.Status = request.Status;
        entity.RegistrationMode = request.RegistrationMode;
        entity.AllowWaitlistWhenFull = request.AllowWaitlistWhenFull;
        entity.ExternalRegistrationUrl = string.IsNullOrWhiteSpace(request.ExternalRegistrationUrl) ? null : request.ExternalRegistrationUrl.Trim();
        entity.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("CourseSaved", nameof(CourseOffering), entity.Id.ToString(), entity.Title, cancellationToken);
        return entity.Id;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CourseOfferings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Kurs nicht gefunden.");
        entity.Status = CourseOfferingStatus.Archived;
        entity.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("CourseArchived", nameof(CourseOffering), entity.Id.ToString(), entity.Title, cancellationToken);
    }

    private async Task<Guid> ResolveCategoryIdForEditAsync(Guid currentCategoryId, Guid courseTypeId, CancellationToken cancellationToken)
    {
        var courseTypeCategoryId = await dbContext.CourseTypes
            .AsNoTracking()
            .Where(x => x.Id == courseTypeId)
            .Select(x => x.CourseCategoryId)
            .FirstOrDefaultAsync(cancellationToken);

        return courseTypeCategoryId == Guid.Empty ? currentCategoryId : courseTypeCategoryId;
    }

    private async Task<IReadOnlyCollection<CategorizedLookupItemDto>> LoadCanonicalCourseTypesAsync(CancellationToken cancellationToken)
    {
        var rawCourseTypes = await dbContext.CourseTypes
            .AsNoTracking()
            .OrderBy(x => x.CourseCategoryId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategorizedLookupItemDto(x.Id, x.CourseCategoryId, x.Name, null))
            .ToListAsync(cancellationToken);

        return rawCourseTypes
            .GroupBy(x => new { x.CategoryId, Key = LookupGrouping.NormalizeLabelValue(x.Label) })
            .Select(group => group.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).First())
            .OrderBy(x => x.CategoryId)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyCollection<LookupItemDto>> LoadCanonicalVenuesAsync(CancellationToken cancellationToken)
    {
        var rawVenues = await dbContext.Venues
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);

        return LookupGrouping.GroupByLabel(rawVenues)
            .Select(x => new LookupItemDto(x.Id, x.Label, null))
            .ToList();
    }

    private async Task<Guid> CanonicalizeCourseTypeIdAsync(Guid courseTypeId, Guid categoryId, CancellationToken cancellationToken)
    {
        var rawCourseTypes = await dbContext.CourseTypes
            .AsNoTracking()
            .Where(x => x.CourseCategoryId == categoryId)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        var selected = rawCourseTypes.FirstOrDefault(x => x.Id == courseTypeId);
        if (selected is null)
        {
            return courseTypeId;
        }

        var normalizedLabel = LookupGrouping.NormalizeLabelValue(selected.Name);
        return rawCourseTypes
            .Where(x => LookupGrouping.NormalizeLabelValue(x.Name) == normalizedLabel)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .First();
    }

    private async Task<Guid> CanonicalizeVenueIdAsync(Guid venueId, CancellationToken cancellationToken)
    {
        var rawVenues = await dbContext.Venues
            .AsNoTracking()
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        var selected = rawVenues.FirstOrDefault(x => x.Id == venueId);
        if (selected is null)
        {
            return venueId;
        }

        var normalizedLabel = LookupGrouping.NormalizeLabelValue(selected.Name);
        return rawVenues
            .Where(x => LookupGrouping.NormalizeLabelValue(x.Name) == normalizedLabel)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .First();
    }
}
