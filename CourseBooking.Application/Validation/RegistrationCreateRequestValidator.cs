using CourseBooking.Application.Dtos;
using FluentValidation;

namespace CourseBooking.Application.Validation;

public sealed class RegistrationCreateRequestValidator : AbstractValidator<RegistrationCreateRequest>
{
    public RegistrationCreateRequestValidator()
    {
        RuleFor(x => x.GuardianFullName)
            .NotEmpty().WithMessage("Bitte geben Sie den Namen eines Elternteils an.")
            .MaximumLength(200).WithMessage("Der Name darf maximal 200 Zeichen lang sein.");
        RuleFor(x => x.ChildFullName)
            .NotEmpty().WithMessage("Bitte geben Sie den Namen des Kindes an.")
            .MaximumLength(200).WithMessage("Der Name darf maximal 200 Zeichen lang sein.");
        RuleFor(x => x.ChildBirthDate)
            .NotNull()
            .WithMessage("Bitte wählen Sie das Geburtsdatum des Kindes aus.");
        RuleFor(x => x.AddressLine1)
            .NotEmpty().WithMessage("Bitte geben Sie Straße und Hausnummer an.")
            .MaximumLength(250).WithMessage("Die Adresse darf maximal 250 Zeichen lang sein.");
        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("Bitte geben Sie eine Postleitzahl an.")
            .MaximumLength(20).WithMessage("Die Postleitzahl darf maximal 20 Zeichen lang sein.");
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Bitte geben Sie den Ort an.")
            .MaximumLength(120).WithMessage("Der Ortsname darf maximal 120 Zeichen lang sein.");
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Bitte geben Sie eine Telefonnummer an.")
            .MaximumLength(50).WithMessage("Die Telefonnummer darf maximal 50 Zeichen lang sein.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Bitte geben Sie eine E-Mail-Adresse an.")
            .EmailAddress().WithMessage("Bitte geben Sie eine gültige E-Mail-Adresse an.")
            .MaximumLength(200).WithMessage("Die E-Mail-Adresse darf maximal 200 Zeichen lang sein.");
        RuleFor(x => x.TermsAccepted)
            .Equal(true)
            .WithMessage("Bitte akzeptieren Sie die Teilnahmebedingungen.");
        RuleFor(x => x.PrivacyAccepted)
            .Equal(true)
            .WithMessage("Bitte bestätigen Sie die Datenschutz-Hinweise.");
        RuleFor(x => x.Priorities)
            .NotEmpty()
            .WithMessage("Bitte wählen Sie mindestens einen Wunschkurs aus.")
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
