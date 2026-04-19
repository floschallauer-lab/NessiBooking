using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;
using FluentValidation;

namespace CourseBooking.Application.Validation;

public sealed class CourseUpsertRequestValidator : AbstractValidator<CourseUpsertRequest>
{
    public CourseUpsertRequestValidator()
    {
        RuleFor(x => x.CourseCategoryId).NotEmpty();
        RuleFor(x => x.CourseTypeId).NotEmpty();
        RuleFor(x => x.VenueId).NotEmpty();
        RuleFor(x => x.CourseInstructorId).NotNull().WithMessage("Bitte waehlen Sie eine Kursleitung aus.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Capacity).GreaterThan(0);
        RuleFor(x => x.StartDate).NotNull();
        RuleFor(x => x.EndDate)
            .NotNull()
            .Must((request, endDate) => request.StartDate is null || endDate >= request.StartDate)
            .WithMessage("Das Enddatum muss nach dem Startdatum liegen.");
        RuleFor(x => x.StartTime).NotNull();
        RuleFor(x => x.EndTime)
            .NotNull()
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
    }
}
