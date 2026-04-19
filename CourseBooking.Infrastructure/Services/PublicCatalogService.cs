using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CourseBooking.Infrastructure.Services;

internal sealed class PublicCatalogService(CourseBookingDbContext dbContext) : IPublicCatalogService
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-AT");

    public async Task<CatalogPageDto> GetCatalogAsync(CourseFilterDto filter, CancellationToken cancellationToken = default)
    {
        var occupancy = await LoadOccupancyAsync(cancellationToken);
        var rawCourseTypes = await dbContext.CourseTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CourseCategoryId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategorizedLookupItemDto(x.Id, x.CourseCategoryId, x.Name, null))
            .ToListAsync(cancellationToken);
        var rawVenues = await dbContext.Venues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);
        var groupedCourseTypeSets = rawCourseTypes
            .GroupBy(x => new { x.CategoryId, Key = LookupGrouping.NormalizeLabelValue(x.Label) })
            .Select(group =>
            {
                var canonical = group.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).First();
                return new
                {
                    Canonical = canonical,
                    MemberIds = group.Select(x => x.Id).ToHashSet()
                };
            })
            .ToList();
        var groupedCourseTypes = groupedCourseTypeSets.Select(x => x.Canonical).ToList();
        var groupedVenues = LookupGrouping.GroupByLabel(rawVenues);

        var query = dbContext.CourseOfferings
            .AsNoTracking()
            .Include(x => x.CourseCategory)
            .Include(x => x.CourseType)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .Include(x => x.AgeRule)
            .Where(x => x.Status != CourseOfferingStatus.Archived);

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.CourseCategoryId == filter.CategoryId.Value);
        }

        if (filter.CourseTypeId.HasValue)
        {
            var selectedCourseTypeIds = groupedCourseTypeSets
                .FirstOrDefault(x => x.Canonical.Id == filter.CourseTypeId.Value)?
                .MemberIds;
            if (selectedCourseTypeIds?.Count > 0)
            {
                query = query.Where(x => selectedCourseTypeIds.Contains(x.CourseTypeId));
            }
        }

        if (filter.VenueId.HasValue)
        {
            var selectedVenueIds = groupedVenues
                .FirstOrDefault(x => x.Id == filter.VenueId.Value)?
                .MemberIds;
            if (selectedVenueIds?.Count > 0)
            {
                query = query.Where(x => selectedVenueIds.Contains(x.VenueId));
            }
        }

        if (filter.CourseCycleId.HasValue)
        {
            query = query.Where(x => x.CourseCycleId == filter.CourseCycleId.Value);
        }

        if (filter.DayOfWeek.HasValue)
        {
            query = query.Where(x => x.DayOfWeek == filter.DayOfWeek.Value);
        }

        if (filter.RegistrationMode.HasValue)
        {
            query = query.Where(x => x.RegistrationMode == filter.RegistrationMode.Value);
        }

        var offerings = await query
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        var items = offerings
            .Select(offering =>
            {
                var occupied = occupancy.GetValueOrDefault(offering.Id);
                var seatsRemaining = offering.SeatsRemaining(occupied);
                var isPubliclyBookable = CourseOfferingPolicy.IsPubliclyBookable(offering, occupied);
                return new CourseListItemDto(
                    offering.Id,
                    offering.Title,
                    offering.CourseCategory?.Name ?? string.Empty,
                    offering.CourseType?.Name ?? string.Empty,
                    offering.Venue?.Name ?? string.Empty,
                    offering.CourseCycle?.Name,
                    GermanCulture.DateTimeFormat.GetDayName(offering.StartDate.ToDateTime(TimeOnly.MinValue).DayOfWeek),
                    $"{offering.StartTime:HH\\:mm} - {offering.EndTime:HH\\:mm}",
                    $"{offering.StartDate:dd.MM.yyyy} - {offering.EndDate:dd.MM.yyyy}",
                    CourseRuleEvaluator.BuildAgeLabel(offering.AgeRule),
                    offering.Price,
                    offering.Capacity,
                    seatsRemaining,
                    offering.Status,
                    offering.RegistrationMode,
                    isPubliclyBookable,
                    offering.AllowWaitlistWhenFull,
                    offering.ExternalRegistrationUrl,
                    offering.CustomerNotice);
            })
            .Where(item => !filter.OnlyWithFreeSpots || item.IsPubliclyBookable)
            .ToList();

        var categories = await dbContext.CourseCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);
        var cycles = await dbContext.CourseCycles.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);

        return new CatalogPageDto(
            categories,
            groupedCourseTypes,
            groupedVenues.Select(x => new LookupItemDto(x.Id, x.Label, null)).ToList(),
            cycles,
            items);
    }

    public async Task<CourseDetailDto?> GetCourseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var occupancy = await LoadOccupancyAsync(cancellationToken);
        var offering = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(x => x.CourseCategory)
            .Include(x => x.CourseType)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .Include(x => x.AgeRule)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (offering is null)
        {
            return null;
        }

        return new CourseDetailDto(
            offering.Id,
            offering.Title,
            offering.Description,
            offering.CourseCategory?.Name ?? string.Empty,
            offering.CourseType?.Name ?? string.Empty,
            offering.Venue?.Name ?? string.Empty,
            $"{offering.Venue?.AddressLine1}, {offering.Venue?.PostalCode} {offering.Venue?.City}",
            offering.CourseCycle?.Name,
            offering.InstructorName,
            GermanCulture.DateTimeFormat.GetDayName(offering.StartDate.ToDateTime(TimeOnly.MinValue).DayOfWeek),
            $"{offering.StartTime:HH\\:mm} - {offering.EndTime:HH\\:mm}",
            $"{offering.StartDate:dd.MM.yyyy} - {offering.EndDate:dd.MM.yyyy}",
            CourseRuleEvaluator.BuildAgeLabel(offering.AgeRule),
            offering.Price,
            offering.Capacity,
            offering.SeatsRemaining(occupancy.GetValueOrDefault(offering.Id)),
            offering.Status,
            offering.RegistrationMode,
            CourseOfferingPolicy.IsPubliclyBookable(offering, occupancy.GetValueOrDefault(offering.Id)),
            offering.AllowWaitlistWhenFull,
            offering.ExternalRegistrationUrl,
            offering.CustomerNotice);
    }

    public async Task<RegistrationFormOptionsDto> GetRegistrationFormOptionsAsync(CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.CourseCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);

        var rawCourseTypes = await dbContext.CourseTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CourseCategoryId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategorizedLookupItemDto(x.Id, x.CourseCategoryId, x.Name, null))
            .ToListAsync(cancellationToken);

        var groupedCourseTypes = rawCourseTypes
            .GroupBy(x => new { x.CategoryId, Key = LookupGrouping.NormalizeLabelValue(x.Label) })
            .Select(group => group.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).First())
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var canonicalCourseTypeIds = rawCourseTypes
            .GroupBy(x => new { x.CategoryId, Key = LookupGrouping.NormalizeLabelValue(x.Label) })
            .SelectMany(group =>
            {
                var canonicalId = group.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).First().Id;
                return group.Select(item => new KeyValuePair<Guid, Guid>(item.Id, canonicalId));
            })
            .ToDictionary(x => x.Key, x => x.Value);

        var offerings = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(x => x.Venue)
            .Where(x => x.RegistrationMode == CourseRegistrationMode.Internal && x.Status == CourseOfferingStatus.Published)
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        var cycles = await dbContext.CourseCycles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);

        var courseOptions = offerings
            .Select(x => new RegistrationCourseOptionDto(
                x.Id,
                x.CourseCategoryId,
                canonicalCourseTypeIds.TryGetValue(x.CourseTypeId, out var canonicalCourseTypeId) ? canonicalCourseTypeId : x.CourseTypeId,
                $"{x.Title} | {x.Venue!.Name} | {GermanCulture.DateTimeFormat.GetDayName(x.StartDate.ToDateTime(TimeOnly.MinValue).DayOfWeek)} {x.StartTime:HH\\:mm}"))
            .ToList();

        return new RegistrationFormOptionsDto(categories, groupedCourseTypes, cycles, courseOptions);
    }

    public async Task<IReadOnlyCollection<LookupItemDto>> GetInternalCourseOptionsAsync(CancellationToken cancellationToken = default)
    {
        var formOptions = await GetRegistrationFormOptionsAsync(cancellationToken);
        return formOptions.Courses
            .Select(x => new LookupItemDto(x.Id, x.Label, null))
            .ToList();
    }

    public async Task<IReadOnlyCollection<LookupItemDto>> GetCycleOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.CourseCycles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, int>> LoadOccupancyAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Registrations
            .AsNoTracking()
            .Where(x => x.AssignedCourseOfferingId.HasValue && (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved))
            .GroupBy(x => x.AssignedCourseOfferingId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);
    }
}
