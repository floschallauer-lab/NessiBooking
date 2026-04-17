using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services;

internal sealed class AdminDashboardService(CourseBookingDbContext dbContext) : IAdminDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(AdminDashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var rawCourseTypes = await dbContext.CourseTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);
        var rawVenues = await dbContext.Venues
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LookupItemDto(x.Id, x.Name, null))
            .ToListAsync(cancellationToken);
        var groupedCourseTypes = LookupGrouping.GroupByLabel(rawCourseTypes);
        var groupedVenues = LookupGrouping.GroupByLabel(rawVenues);

        var courseQuery = dbContext.CourseOfferings
            .AsNoTracking()
            .Include(x => x.CourseCategory)
            .Include(x => x.CourseType)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .Where(x => x.Status != CourseOfferingStatus.Archived);

        if (filter.CategoryId.HasValue)
        {
            courseQuery = courseQuery.Where(x => x.CourseCategoryId == filter.CategoryId.Value);
        }

        if (filter.CourseCycleId.HasValue)
        {
            courseQuery = courseQuery.Where(x => x.CourseCycleId == filter.CourseCycleId.Value);
        }

        if (filter.RegistrationMode.HasValue)
        {
            courseQuery = courseQuery.Where(x => x.RegistrationMode == filter.RegistrationMode.Value);
        }

        if (filter.CourseTypeId.HasValue)
        {
            var courseTypeIds = groupedCourseTypes.FirstOrDefault(x => x.Id == filter.CourseTypeId.Value)?.MemberIds;
            if (courseTypeIds?.Count > 0)
            {
                courseQuery = courseQuery.Where(x => courseTypeIds.Contains(x.CourseTypeId));
            }
        }

        if (filter.VenueId.HasValue)
        {
            var venueIds = groupedVenues.FirstOrDefault(x => x.Id == filter.VenueId.Value)?.MemberIds;
            if (venueIds?.Count > 0)
            {
                courseQuery = courseQuery.Where(x => venueIds.Contains(x.VenueId));
            }
        }

        var courses = await courseQuery
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        var courseIds = courses.Select(x => x.Id).ToList();
        var hasCourseScopedFilter =
            filter.CategoryId.HasValue ||
            filter.CourseTypeId.HasValue ||
            filter.VenueId.HasValue ||
            filter.CourseCycleId.HasValue ||
            filter.RegistrationMode.HasValue;

        var occupancy = await dbContext.Registrations
            .AsNoTracking()
            .Where(x => x.AssignedCourseOfferingId.HasValue && (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved))
            .GroupBy(x => x.AssignedCourseOfferingId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var openRequestCounts = courseIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.RegistrationPriorities
                .AsNoTracking()
                .Where(x => courseIds.Contains(x.CourseOfferingId) && (x.Registration!.Status == RegistrationStatus.Received || x.Registration.Status == RegistrationStatus.Reserved))
                .GroupBy(x => x.CourseOfferingId)
                .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var waitlistCounts = courseIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.WaitlistEntries
                .AsNoTracking()
                .Where(x => x.IsActive && courseIds.Contains(x.CourseOfferingId))
                .GroupBy(x => x.CourseOfferingId)
                .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var registrationQuery = dbContext.Registrations
            .AsNoTracking()
            .Include(x => x.Guardian)
            .Include(x => x.ChildParticipant)
            .Include(x => x.AssignedCourseOffering)
            .AsQueryable();

        if (filter.RegistrationStatus.HasValue)
        {
            registrationQuery = registrationQuery.Where(x => x.Status == filter.RegistrationStatus.Value);
        }

        if (hasCourseScopedFilter)
        {
            if (courseIds.Count == 0)
            {
                registrationQuery = registrationQuery.Where(_ => false);
            }
            else
            {
                registrationQuery = registrationQuery.Where(x =>
                    (x.AssignedCourseOfferingId.HasValue && courseIds.Contains(x.AssignedCourseOfferingId.Value)) ||
                    x.Priorities.Any(p => courseIds.Contains(p.CourseOfferingId)));
            }
        }

        var registrationStatusCounts = await registrationQuery
            .GroupBy(x => x.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var openRegistrations = registrationStatusCounts
            .Where(x => x.Key == RegistrationStatus.Received || x.Key == RegistrationStatus.Reserved)
            .Sum(x => x.Count);
        var confirmedRegistrations = registrationStatusCounts
            .Where(x => x.Key == RegistrationStatus.Confirmed)
            .Sum(x => x.Count);
        var waitlistEntries = registrationStatusCounts
            .Where(x => x.Key == RegistrationStatus.Waitlisted)
            .Sum(x => x.Count);

        var internalCourses = courses.Count(x => x.RegistrationMode == CourseRegistrationMode.Internal);
        var externalCourses = courses.Count(x => x.RegistrationMode == CourseRegistrationMode.External);
        var bookableInternalCourses = courses.Count(x => CourseOfferingPolicy.IsPubliclyBookable(x, occupancy.GetValueOrDefault(x.Id)));
        var soldOutCourses = courses.Count(x => CourseOfferingPolicy.IsOperationallySoldOut(x, occupancy.GetValueOrDefault(x.Id)));
        var lowAvailability = courses.Count(x => CourseOfferingPolicy.HasLowAvailability(x, occupancy.GetValueOrDefault(x.Id)));

        var recentRegistrations = await registrationQuery
            .OrderByDescending(x => x.SubmittedAtUtc)
            .Take(8)
            .Select(x => new DashboardRecentRegistrationDto(
                x.Id,
                x.SubmittedAtUtc,
                x.Guardian!.FullName,
                x.ChildParticipant!.FullName,
                x.Status,
                x.AssignedCourseOffering != null ? x.AssignedCourseOffering.Title : null))
            .ToListAsync(cancellationToken);

        var attentionCourses = courses
            .Where(x => x.RegistrationMode == CourseRegistrationMode.Internal && x.Status == CourseOfferingStatus.Published)
            .Select(x =>
            {
                var occupied = occupancy.GetValueOrDefault(x.Id);
                var seatsRemaining = x.SeatsRemaining(occupied);
                return new DashboardAttentionCourseDto(
                    x.Id,
                    x.Title,
                    x.Venue?.Name ?? string.Empty,
                    "Intern",
                    seatsRemaining,
                    x.Capacity,
                    openRequestCounts.GetValueOrDefault(x.Id),
                    waitlistCounts.GetValueOrDefault(x.Id),
                    CourseOfferingPolicy.HasLowAvailability(x, occupied),
                    CourseOfferingPolicy.IsOperationallySoldOut(x, occupied));
            })
            .OrderByDescending(x => x.IsSoldOut)
            .ThenByDescending(x => x.WaitlistCount)
            .ThenByDescending(x => x.OpenRegistrations)
            .ThenBy(x => x.SeatsRemaining)
            .ThenBy(x => x.Title)
            .Take(8)
            .ToList();

        var registrationTotal = Math.Max(registrationStatusCounts.Sum(x => x.Count), 1);
        var courseTotal = Math.Max(courses.Count, 1);

        return new DashboardSummaryDto(
            await dbContext.CourseCategories.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new LookupItemDto(x.Id, x.Name, null))
                .ToListAsync(cancellationToken),
            groupedCourseTypes.Select(x => new LookupItemDto(x.Id, x.Label, null)).ToList(),
            groupedVenues.Select(x => new LookupItemDto(x.Id, x.Label, null)).ToList(),
            await dbContext.CourseCycles.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new LookupItemDto(x.Id, x.Name, null))
                .ToListAsync(cancellationToken),
            openRegistrations,
            confirmedRegistrations,
            waitlistEntries,
            internalCourses,
            externalCourses,
            bookableInternalCourses,
            soldOutCourses,
            lowAvailability,
            registrationStatusCounts
                .OrderByDescending(x => x.Count)
                .Select(x => new DashboardBreakdownRowDto(ResolveRegistrationStatusLabel(x.Key), x.Count, registrationTotal))
                .ToList(),
            courses
                .GroupBy(x => x.Venue?.Name ?? "Ohne Ort")
                .Select(x => new DashboardBreakdownRowDto(x.Key, x.Count(), courseTotal))
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Label)
                .Take(8)
                .ToList(),
            courses
                .GroupBy(x => x.CourseCategory?.Name ?? "Ohne Bereich")
                .Select(x => new DashboardBreakdownRowDto(x.Key, x.Count(), courseTotal))
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Label)
                .Take(8)
                .ToList(),
            new List<DashboardBreakdownRowDto>
            {
                new("Intern", internalCourses, courseTotal),
                new("Extern", externalCourses, courseTotal)
            },
            attentionCourses,
            recentRegistrations);
    }

    private static string ResolveRegistrationStatusLabel(RegistrationStatus status)
    {
        return status switch
        {
            RegistrationStatus.Received => "Eingegangen",
            RegistrationStatus.Reserved => "Vorgemerkt",
            RegistrationStatus.Confirmed => "Bestätigt",
            RegistrationStatus.Waitlisted => "Warteliste",
            RegistrationStatus.Rejected => "Abgelehnt",
            _ => status.ToString()
        };
    }
}
