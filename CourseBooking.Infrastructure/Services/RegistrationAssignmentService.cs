using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Constants;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services;

internal sealed class RegistrationAssignmentService(
    CourseBookingDbContext dbContext,
    IAuditService auditService,
    IEmailSenderService emailSenderService) : IRegistrationAssignmentService
{
    public async Task<AssignmentResultDto> AutoAssignAsync(Guid registrationId, CancellationToken cancellationToken = default)
    {
        var registration = await LoadRegistrationAsync(registrationId, cancellationToken);
        var protocol = new List<string>();
        CourseOffering? waitlistCandidate = null;

        foreach (var priority in registration.Priorities.OrderBy(x => x.PriorityOrder))
        {
            var course = priority.CourseOffering!;
            protocol.Add($"Priorität {priority.PriorityOrder}: {course.Title}");

            if (course.RegistrationMode != CourseRegistrationMode.Internal)
            {
                protocol.Add("  - Übersprungen: externer Kurs.");
                continue;
            }

            if (course.Status is CourseOfferingStatus.Draft or CourseOfferingStatus.Archived)
            {
                protocol.Add("  - Übersprungen: Kursstatus nicht buchbar.");
                continue;
            }

            if (!CourseRuleEvaluator.MatchesAgeRule(registration.ChildParticipant!.BirthDate, course.StartDate, course.AgeRule, out var ageMessage))
            {
                protocol.Add($"  - Übersprungen: {ageMessage}");
                continue;
            }

            if (course.CourseType?.OnlyBookableOnce == true)
            {
                var alreadyBooked = await dbContext.Registrations
                    .Include(x => x.AssignedCourseOffering)
                    .AnyAsync(x =>
                        x.Id != registration.Id &&
                        x.ChildParticipantId == registration.ChildParticipantId &&
                        x.AssignedCourseOffering != null &&
                        (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved) &&
                        x.AssignedCourseOffering.CourseTypeId == course.CourseTypeId,
                        cancellationToken);

                if (alreadyBooked)
                {
                    protocol.Add("  - Übersprungen: Kurstyp darf nur einmal gebucht werden.");
                    continue;
                }
            }

            var occupancy = await GetOccupancyAsync(course.Id, registration.Id, cancellationToken);
            if (course.Status != CourseOfferingStatus.SoldOut && occupancy < course.Capacity)
            {
                registration.AssignedCourseOfferingId = course.Id;
                registration.Status = RegistrationStatus.Confirmed;
                registration.LastStatusChangedAtUtc = DateTime.UtcNow;
                registration.AssignmentProtocol = string.Join(Environment.NewLine, protocol.Append("  - Zugeordnet."));

                foreach (var waitlistEntry in registration.WaitlistEntries.Where(x => x.IsActive))
                {
                    waitlistEntry.IsActive = false;
                }

                course.Status = occupancy + 1 >= course.Capacity ? CourseOfferingStatus.SoldOut : CourseOfferingStatus.Published;
                course.UpdatedUtc = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
                await auditService.WriteAsync("RegistrationAutoAssigned", nameof(Registration), registration.Id.ToString(), registration.AssignmentProtocol, cancellationToken);
                await emailSenderService.SendTemplatedEmailAsync(
                    EmailTemplateKeys.RegistrationAccepted,
                    registration.Guardian!.Email,
                    BuildTokens(registration, course),
                    cancellationToken);

                return new AssignmentResultDto(true, false, course.Id, $"Zugeordnet zu {course.Title}.", protocol);
            }

            if (course.AllowWaitlistWhenFull)
            {
                protocol.Add("  - Kein Platz frei, Kurs eignet sich für Warteliste.");
                waitlistCandidate ??= course;
            }
            else
            {
                protocol.Add("  - Kein Platz frei, Warteliste deaktiviert.");
            }
        }

        waitlistCandidate ??= registration.Priorities.OrderBy(x => x.PriorityOrder).First().CourseOffering;
        if (waitlistCandidate is null)
        {
            throw new InvalidOperationException("Keine Wartelistenoption gefunden.");
        }

        var position = await dbContext.WaitlistEntries.CountAsync(x => x.CourseOfferingId == waitlistCandidate.Id && x.IsActive, cancellationToken) + 1;

        if (!registration.WaitlistEntries.Any(x => x.CourseOfferingId == waitlistCandidate.Id && x.IsActive))
        {
            registration.WaitlistEntries.Add(new WaitlistEntry
            {
                CourseOfferingId = waitlistCandidate.Id,
                Position = position,
                Reason = "Keine direkte Zuordnung moeglich."
            });
        }

        registration.AssignedCourseOfferingId = null;
        registration.Status = RegistrationStatus.Waitlisted;
        registration.LastStatusChangedAtUtc = DateTime.UtcNow;
        registration.AssignmentProtocol = string.Join(Environment.NewLine, protocol.Append($"Warteliste fuer {waitlistCandidate.Title}."));

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("RegistrationWaitlisted", nameof(Registration), registration.Id.ToString(), registration.AssignmentProtocol, cancellationToken);
        await emailSenderService.SendTemplatedEmailAsync(
            EmailTemplateKeys.RegistrationWaitlisted,
            registration.Guardian!.Email,
            BuildTokens(registration, waitlistCandidate),
            cancellationToken);

        return new AssignmentResultDto(false, true, waitlistCandidate.Id, $"Auf Warteliste fuer {waitlistCandidate.Title}.", protocol);
    }

    public async Task ManualAssignAsync(Guid registrationId, Guid courseOfferingId, string? adminNote, CancellationToken cancellationToken = default)
    {
        var registration = await LoadRegistrationAsync(registrationId, cancellationToken);
        var course = await dbContext.CourseOfferings
            .Include(x => x.CourseType)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .Include(x => x.AgeRule)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, cancellationToken)
            ?? throw new InvalidOperationException("Kurs nicht gefunden.");

        if (course.RegistrationMode != CourseRegistrationMode.Internal)
        {
            throw new InvalidOperationException("Externe Kurse können nicht intern zugewiesen werden.");
        }

        if (!CourseRuleEvaluator.MatchesAgeRule(registration.ChildParticipant!.BirthDate, course.StartDate, course.AgeRule, out var ageMessage))
        {
            throw new InvalidOperationException($"Altersregel verletzt: {ageMessage}");
        }

        var occupancy = await GetOccupancyAsync(course.Id, registration.Id, cancellationToken);
        if (occupancy >= course.Capacity)
        {
            throw new InvalidOperationException("Der Kurs ist bereits voll.");
        }

        registration.AssignedCourseOfferingId = course.Id;
        registration.Status = RegistrationStatus.Confirmed;
        registration.LastStatusChangedAtUtc = DateTime.UtcNow;
        registration.AdminNotes = string.IsNullOrWhiteSpace(adminNote)
            ? registration.AdminNotes
            : $"{registration.AdminNotes}{Environment.NewLine}{adminNote}".Trim();

        foreach (var waitlistEntry in registration.WaitlistEntries.Where(x => x.IsActive))
        {
            waitlistEntry.IsActive = false;
        }

        registration.AssignmentProtocol = $"{registration.AssignmentProtocol}{Environment.NewLine}Manuelle Zuweisung: {course.Title}".Trim();
        course.Status = occupancy + 1 >= course.Capacity ? CourseOfferingStatus.SoldOut : CourseOfferingStatus.Published;
        course.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("RegistrationManualAssigned", nameof(Registration), registration.Id.ToString(), $"Kurs: {course.Title}", cancellationToken);
        await emailSenderService.SendTemplatedEmailAsync(
            EmailTemplateKeys.RegistrationAccepted,
            registration.Guardian!.Email,
            BuildTokens(registration, course),
            cancellationToken);
    }

    private async Task<Registration> LoadRegistrationAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        return await dbContext.Registrations
            .Include(x => x.Guardian)
            .Include(x => x.ChildParticipant)
            .Include(x => x.WaitlistEntries)
            .Include(x => x.Priorities)
                .ThenInclude(x => x.CourseOffering!)
                    .ThenInclude(x => x.CourseType)
            .Include(x => x.Priorities)
                .ThenInclude(x => x.CourseOffering!)
                    .ThenInclude(x => x.Venue)
            .Include(x => x.Priorities)
                .ThenInclude(x => x.CourseOffering!)
                    .ThenInclude(x => x.CourseCycle)
            .Include(x => x.Priorities)
                .ThenInclude(x => x.CourseOffering!)
                    .ThenInclude(x => x.AgeRule)
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Anmeldung nicht gefunden.");
    }

    private async Task<int> GetOccupancyAsync(Guid courseOfferingId, Guid currentRegistrationId, CancellationToken cancellationToken)
    {
        return await dbContext.Registrations.CountAsync(x =>
            x.Id != currentRegistrationId &&
            x.AssignedCourseOfferingId == courseOfferingId &&
            (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved),
            cancellationToken);
    }

    private static Dictionary<string, string> BuildTokens(Registration registration, CourseOffering course)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = registration.Guardian?.FullName ?? string.Empty,
            ["Kursname"] = course.Title,
            ["Bad"] = course.Venue?.Name ?? string.Empty,
            ["Wochentag"] = course.DayOfWeek.ToString(),
            ["Uhrzeit"] = $"{course.StartTime:HH\\:mm} - {course.EndTime:HH\\:mm}",
            ["Turnus"] = course.CourseCycle?.Name ?? "flexibel"
        };
    }
}
