using System.Text;
using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Constants;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services;

internal sealed class AdminRegistrationService(
    CourseBookingDbContext dbContext,
    IAuditService auditService,
    IEmailSenderService emailSenderService) : IAdminRegistrationService
{
    public async Task<RegistrationListPageDto> GetRegistrationsAsync(RegistrationAdminFilter filter, CancellationToken cancellationToken = default)
    {
        var groupedVenues = LookupGrouping.GroupByLabel(
            await dbContext.Venues.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemDto(x.Id, x.Name, null))
                .ToListAsync(cancellationToken));

        var query = dbContext.Registrations
            .AsNoTracking()
            .Include(x => x.Guardian)
            .Include(x => x.ChildParticipant)
            .Include(x => x.AssignedCourseOffering)
            .Include(x => x.Priorities)
                .ThenInclude(x => x.CourseOffering)
            .AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Status == filter.Status.Value);
        }

        if (filter.CourseOfferingId.HasValue)
        {
            query = query.Where(x => x.AssignedCourseOfferingId == filter.CourseOfferingId.Value || x.Priorities.Any(p => p.CourseOfferingId == filter.CourseOfferingId.Value));
        }

        if (filter.VenueId.HasValue)
        {
            var venueIds = groupedVenues.FirstOrDefault(x => x.Id == filter.VenueId.Value)?.MemberIds;
            if (venueIds?.Count > 0)
            {
                query = query.Where(x => x.Priorities.Any(p => venueIds.Contains(p.CourseOffering!.VenueId)));
            }
        }

        if (filter.CourseCycleId.HasValue)
        {
            query = query.Where(x => x.PreferredCourseCycleId == filter.CourseCycleId.Value || x.Priorities.Any(p => p.CourseOffering!.CourseCycleId == filter.CourseCycleId.Value));
        }

        if (filter.RegistrationMode.HasValue)
        {
            query = query.Where(x =>
                (x.AssignedCourseOfferingId.HasValue && x.AssignedCourseOffering!.RegistrationMode == filter.RegistrationMode.Value) ||
                x.Priorities.Any(p => p.CourseOffering!.RegistrationMode == filter.RegistrationMode.Value));
        }

        var items = await query
            .OrderByDescending(x => x.SubmittedAtUtc)
            .Select(x => new RegistrationRowDto(
                x.Id,
                x.SubmittedAtUtc,
                x.Guardian!.FullName,
                x.ChildParticipant!.FullName,
                x.Guardian.Email,
                x.Status,
                x.AssignedCourseOffering != null ? x.AssignedCourseOffering.Title : null,
                string.Join(" | ", x.Priorities.OrderBy(p => p.PriorityOrder).Select(p => $"{p.PriorityOrder}. {p.CourseOffering!.Title}"))))
            .ToListAsync(cancellationToken);

        var summary = new RegistrationAdminSummaryDto(
            items.Count,
            items.Count(x => x.Status == RegistrationStatus.Received || x.Status == RegistrationStatus.Reserved),
            items.Count(x => x.Status == RegistrationStatus.Confirmed),
            items.Count(x => x.Status == RegistrationStatus.Waitlisted),
            items.Count(x => x.Status == RegistrationStatus.Rejected));

        return new RegistrationListPageDto(
            items,
            await dbContext.CourseOfferings.AsNoTracking().OrderBy(x => x.Title).Select(x => new LookupItemDto(x.Id, x.Title, null)).ToListAsync(cancellationToken),
            groupedVenues.Select(x => new LookupItemDto(x.Id, x.Label, null)).ToList(),
            await dbContext.CourseCycles.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => new LookupItemDto(x.Id, x.Name, null)).ToListAsync(cancellationToken),
            summary);
    }

    public async Task<RegistrationDetailDto?> GetRegistrationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registration = await dbContext.Registrations
            .AsNoTracking()
            .Include(x => x.Guardian)
            .Include(x => x.ChildParticipant)
            .Include(x => x.PreferredCourseCycle)
            .Include(x => x.AssignedCourseOffering)
            .Include(x => x.Priorities)
                .ThenInclude(x => x.CourseOffering)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (registration is null)
        {
            return null;
        }

        return new RegistrationDetailDto(
            registration.Id,
            registration.SubmittedAtUtc,
            registration.Status,
            registration.Guardian!.FullName,
            registration.ChildParticipant!.FullName,
            registration.ChildParticipant.BirthDate,
            registration.Guardian.AddressLine1,
            registration.Guardian.PostalCode,
            registration.Guardian.City,
            registration.Guardian.PhoneNumber,
            registration.Guardian.Email,
            registration.PreferredCourseCycle?.Name,
            registration.Note,
            registration.AdminNotes,
            registration.AssignmentProtocol,
            registration.AssignedCourseOfferingId,
            registration.AssignedCourseOffering?.Title,
            registration.Priorities.OrderBy(p => p.PriorityOrder).Select(p => new RegistrationPriorityDetailDto(p.CourseOfferingId, p.PriorityOrder, p.CourseOffering?.Title ?? string.Empty)).ToList());
    }

    public async Task UpdateStatusAsync(Guid id, RegistrationStatus status, string? adminNotes, CancellationToken cancellationToken = default)
    {
        var registration = await dbContext.Registrations
            .Include(x => x.Guardian)
            .Include(x => x.AssignedCourseOffering)
            .ThenInclude(x => x!.Venue)
            .Include(x => x.AssignedCourseOffering)
            .ThenInclude(x => x!.CourseCycle)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Anmeldung nicht gefunden.");

        registration.Status = status;
        registration.LastStatusChangedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(adminNotes))
        {
            registration.AdminNotes = $"{registration.AdminNotes}{Environment.NewLine}{adminNotes}".Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("RegistrationStatusUpdated", nameof(Domain.Entities.Registration), registration.Id.ToString(), status.ToString(), cancellationToken);

        var templateKey = status switch
        {
            RegistrationStatus.Confirmed => EmailTemplateKeys.RegistrationAccepted,
            RegistrationStatus.Waitlisted => EmailTemplateKeys.RegistrationWaitlisted,
            RegistrationStatus.Rejected => EmailTemplateKeys.RegistrationRejected,
            _ => null
        };

        if (templateKey is null)
        {
            return;
        }

        await emailSenderService.SendTemplatedEmailAsync(
            templateKey,
            registration.Guardian!.Email,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = registration.Guardian.FullName,
                ["Kursname"] = registration.AssignedCourseOffering?.Title ?? string.Empty,
                ["Bad"] = registration.AssignedCourseOffering?.Venue?.Name ?? string.Empty,
                ["Wochentag"] = registration.AssignedCourseOffering?.DayOfWeek.ToString() ?? string.Empty,
                ["Uhrzeit"] = registration.AssignedCourseOffering is null ? string.Empty : $"{registration.AssignedCourseOffering.StartTime:HH\\:mm} - {registration.AssignedCourseOffering.EndTime:HH\\:mm}",
                ["Turnus"] = registration.AssignedCourseOffering?.CourseCycle?.Name ?? string.Empty
            },
            cancellationToken);
    }

    public async Task UpdateAdminNotesAsync(Guid id, string adminNotes, CancellationToken cancellationToken = default)
    {
        var registration = await dbContext.Registrations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Anmeldung nicht gefunden.");
        registration.AdminNotes = adminNotes.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("RegistrationNotesUpdated", nameof(Domain.Entities.Registration), registration.Id.ToString(), "Notiz aktualisiert", cancellationToken);
    }

    public async Task<string> ExportRegistrationsCsvAsync(RegistrationAdminFilter filter, CancellationToken cancellationToken = default)
    {
        var page = await GetRegistrationsAsync(filter, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("SubmittedAtUtc,GuardianName,ChildName,Email,Status,AssignedCourse,Priorities");
        foreach (var row in page.Registrations)
        {
            sb.AppendLine(string.Join(",", Escape(row.SubmittedAtUtc.ToString("O")), Escape(row.GuardianName), Escape(row.ChildName), Escape(row.Email), Escape(row.Status.ToString()), Escape(row.AssignedCourse), Escape(row.PrioritySummary)));
        }

        return sb.ToString();
    }

    public async Task<string> ExportParticipantsCsvAsync(Guid courseOfferingId, CancellationToken cancellationToken = default)
    {
        var registrations = await dbContext.Registrations
            .AsNoTracking()
            .Include(x => x.Guardian)
            .Include(x => x.ChildParticipant)
            .Where(x => x.AssignedCourseOfferingId == courseOfferingId && (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved))
            .OrderBy(x => x.ChildParticipant!.FullName)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("ChildName,BirthDate,GuardianName,Email,Phone");
        foreach (var item in registrations)
        {
            sb.AppendLine(string.Join(",", Escape(item.ChildParticipant!.FullName), Escape(item.ChildParticipant.BirthDate.ToString("yyyy-MM-dd")), Escape(item.Guardian!.FullName), Escape(item.Guardian.Email), Escape(item.Guardian.PhoneNumber)));
        }

        return sb.ToString();
    }

    public async Task<IReadOnlyCollection<CourseParticipantDto>> GetParticipantsAsync(Guid courseOfferingId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Registrations
            .AsNoTracking()
            .Include(x => x.Guardian)
            .Include(x => x.ChildParticipant)
            .Where(x => x.AssignedCourseOfferingId == courseOfferingId && (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved))
            .OrderBy(x => x.ChildParticipant!.FullName)
            .Select(x => new CourseParticipantDto(
                x.Id,
                x.ChildParticipant!.FullName,
                x.ChildParticipant.BirthDate,
                x.Guardian!.FullName,
                x.Guardian.Email,
                x.Guardian.PhoneNumber))
            .ToListAsync(cancellationToken);
    }

    private static string Escape(string? value)
    {
        var sanitized = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{sanitized}\"";
    }
}
