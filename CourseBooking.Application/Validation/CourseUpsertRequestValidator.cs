using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;
using FluentValidation;

namespace CourseBooking.Application.Validation;

public sealed class CourseUpsertRequestValidator : AbstractValidator<CourseUpsertRequest>
{
    public CourseUpsertRequestValidator()
    {
        RuleFor(x => x.CourseCategoryId)
            .NotEmpty()
            .WithMessage("Bitte wählen Sie einen Bereich aus.");
        RuleFor(x => x.CourseTypeId)
            .NotEmpty()
            .WithMessage("Bitte wählen Sie eine passende Unterkategorie aus.");
        RuleFor(x => x.VenueId)
            .NotEmpty()
            .WithMessage("Bitte wählen Sie einen Ort oder ein Bad aus.");
        RuleFor(x => x.CourseInstructorId)
            .NotNull()
            .WithMessage("Bitte wählen Sie eine Kursleitung aus.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Bitte geben Sie einen Kurstitel ein.")
            .MaximumLength(200).WithMessage("Der Kurstitel darf maximal 200 Zeichen lang sein.");
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Bitte hinterlegen Sie eine Beschreibung für den Kundenbereich.");
        RuleFor(x => x.CustomerNotice)
            .MaximumLength(500)
            .WithMessage("Der Hinweis für den Kundenbereich darf maximal 500 Zeichen lang sein.");
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Der Preis darf nicht negativ sein.");
        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .WithMessage("Bitte geben Sie mindestens einen Platz an.");
        RuleFor(x => x.StartDate)
            .NotNull()
            .WithMessage("Bitte wählen Sie ein Startdatum aus.");
        RuleFor(x => x.EndDate)
            .NotNull()
            .WithMessage("Bitte wählen Sie ein Enddatum aus.")
            .Must((request, endDate) => request.StartDate is null || endDate >= request.StartDate)
            .WithMessage("Das Enddatum muss nach dem Startdatum liegen.");
        RuleFor(x => x.StartTime)
            .NotNull()
            .WithMessage("Bitte wählen Sie eine Startzeit aus.");
        RuleFor(x => x.EndTime)
            .NotNull()
            .WithMessage("Bitte wählen Sie eine Endzeit aus.")
            .Must((request, endTime) => request.StartTime is null || endTime > request.StartTime)
            .WithMessage("Die Endzeit muss nach der Startzeit liegen.");
        RuleFor(x => x.ExternalRegistrationUrl)
            .Empty()
            .When(x => x.RegistrationMode == CourseRegistrationMode.Internal)
            .WithMessage("Interne Kurse dürfen keinen externen Buchungslink haben.");
        RuleFor(x => x.ExternalRegistrationUrl)
            .NotEmpty()
            .When(x => x.RegistrationMode == CourseRegistrationMode.External)
            .WithMessage("Bitte hinterlegen Sie für externe Kurse einen Buchungslink.");
        RuleFor(x => x.ExternalRegistrationUrl)
            .Must(BeValidAbsoluteUrl)
            .When(x => x.RegistrationMode == CourseRegistrationMode.External && !string.IsNullOrWhiteSpace(x.ExternalRegistrationUrl))
            .WithMessage("Bitte hinterlegen Sie einen vollständigen externen Buchungslink.");
    }

    private static bool BeValidAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
