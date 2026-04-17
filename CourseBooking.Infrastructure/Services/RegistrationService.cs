using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Constants;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services.Support;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services;

internal sealed class RegistrationService(
    CourseBookingDbContext dbContext,
    IValidator<RegistrationCreateRequest> validator,
    IEmailSenderService emailSenderService) : IRegistrationService
{
    public async Task<RegistrationSubmissionResult> SubmitAsync(RegistrationCreateRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var orderedPriorities = request.Priorities.OrderBy(x => x.PriorityOrder).ToList();
        var offeringIds = orderedPriorities.Select(x => x.CourseOfferingId).ToArray();
        var offerings = await dbContext.CourseOfferings
            .Include(x => x.CourseType)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .Include(x => x.AgeRule)
            .Where(x => offeringIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (offerings.Count != offeringIds.Length)
        {
            throw new ValidationException("Mindestens ein gewählter Kurs existiert nicht mehr.");
        }

        foreach (var priority in orderedPriorities)
        {
            var offering = offerings.Single(x => x.Id == priority.CourseOfferingId);
            if (offering.RegistrationMode != CourseRegistrationMode.Internal)
            {
                throw new ValidationException($"Der Kurs '{offering.Title}' ist extern und kann nicht intern gebucht werden.");
            }

            if (offering.Status is CourseOfferingStatus.Draft or CourseOfferingStatus.Archived)
            {
                throw new ValidationException($"Der Kurs '{offering.Title}' ist aktuell nicht buchbar.");
            }

            if (offering.Status == CourseOfferingStatus.SoldOut && !offering.AllowWaitlistWhenFull)
            {
                throw new ValidationException($"Der Kurs '{offering.Title}' ist ausgebucht und nimmt keine Warteliste mehr an.");
            }

            if (!CourseRuleEvaluator.MatchesAgeRule(request.ChildBirthDate!.Value, offering.StartDate, offering.AgeRule, out var message))
            {
                throw new ValidationException($"Der Kurs '{offering.Title}' passt nicht zur Altersregel: {message}");
            }

            if (offering.CourseType?.OnlyBookableOnce == true)
            {
                var hasExistingBooking = await dbContext.Registrations
                    .Include(x => x.ChildParticipant)
                    .Include(x => x.AssignedCourseOffering)
                    .AnyAsync(x =>
                        x.ChildParticipant!.FullName == request.ChildFullName &&
                        x.ChildParticipant.BirthDate == request.ChildBirthDate &&
                        x.AssignedCourseOffering != null &&
                        (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved) &&
                        x.AssignedCourseOffering.CourseTypeId == offering.CourseTypeId,
                        cancellationToken);

                if (hasExistingBooking)
                {
                    throw new ValidationException($"Der Kurstyp '{offering.CourseType.Name}' darf nur einmal gebucht werden.");
                }
            }
        }

        var guardian = new Guardian
        {
            FullName = request.GuardianFullName.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            PostalCode = request.PostalCode.Trim(),
            City = request.City.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email.Trim()
        };

        var child = new ChildParticipant
        {
            FullName = request.ChildFullName.Trim(),
            BirthDate = request.ChildBirthDate!.Value
        };

        var registration = new Registration
        {
            Guardian = guardian,
            ChildParticipant = child,
            PreferredCourseCycleId = request.PreferredCourseCycleId,
            TermsAccepted = request.TermsAccepted,
            PrivacyAccepted = request.PrivacyAccepted,
            Note = request.Note?.Trim() ?? string.Empty,
            Source = RegistrationSource.PublicForm,
            Status = RegistrationStatus.Received,
            SubmittedAtUtc = DateTime.UtcNow,
            LastStatusChangedAtUtc = DateTime.UtcNow,
            Priorities = orderedPriorities
                .Select(x => new RegistrationPriority
                {
                    CourseOfferingId = x.CourseOfferingId,
                    PriorityOrder = x.PriorityOrder
                })
                .ToList()
        };

        dbContext.Registrations.Add(registration);
        await dbContext.SaveChangesAsync(cancellationToken);

        var topOffering = offerings.Single(x => x.Id == orderedPriorities[0].CourseOfferingId);
        await emailSenderService.SendTemplatedEmailAsync(
            EmailTemplateKeys.RegistrationReceived,
            guardian.Email,
            BuildTokens(guardian.FullName, topOffering),
            cancellationToken);

        var confirmationNumber = registration.Id.ToString("N")[..8].ToUpperInvariant();
        return new RegistrationSubmissionResult(
            registration.Id,
            confirmationNumber,
            "Ihre Anmeldung ist eingegangen. Wir prüfen jetzt die Prioritäten und melden uns per E-Mail.");
    }

    private static Dictionary<string, string> BuildTokens(string guardianName, CourseOffering offering)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = guardianName,
            ["Kursname"] = offering.Title,
            ["Bad"] = offering.Venue?.Name ?? string.Empty,
            ["Wochentag"] = offering.DayOfWeek.ToString(),
            ["Uhrzeit"] = $"{offering.StartTime:HH\\:mm} - {offering.EndTime:HH\\:mm}",
            ["Turnus"] = offering.CourseCycle?.Name ?? "flexibel"
        };
    }
}
