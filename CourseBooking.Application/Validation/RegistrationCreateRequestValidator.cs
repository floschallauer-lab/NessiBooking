using CourseBooking.Application.Dtos;
using FluentValidation;

namespace CourseBooking.Application.Validation;

public sealed class RegistrationCreateRequestValidator : AbstractValidator<RegistrationCreateRequest>
{
    public RegistrationCreateRequestValidator()
    {
        RuleFor(x => x.GuardianFullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChildFullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChildBirthDate).NotNull();
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(250);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.TermsAccepted).Equal(true);
        RuleFor(x => x.PrivacyAccepted).Equal(true);
        RuleFor(x => x.Priorities)
            .NotEmpty()
            .Must(HaveUniqueCourseIds)
            .WithMessage("Jeder Kurs darf nur einmal in der Prioritätenliste vorkommen.")
            .Must(HaveContinuousOrdering)
            .WithMessage("Prioritäten müssen eindeutig und lückenlos von 1 beginnend vergeben werden.");
    }

    private static bool HaveUniqueCourseIds(List<RegistrationPriorityInputDto> priorities)
        => priorities.Select(x => x.CourseOfferingId).Distinct().Count() == priorities.Count;

    private static bool HaveContinuousOrdering(List<RegistrationPriorityInputDto> priorities)
    {
        var expected = Enumerable.Range(1, priorities.Count).ToArray();
        var actual = priorities.Select(x => x.PriorityOrder).OrderBy(x => x).ToArray();
        return expected.SequenceEqual(actual);
    }
}
