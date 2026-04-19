using System.Globalization;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Web.Components.Shared;

public static class UiLabels
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-AT");

    public static string CourseStatus(CourseOfferingStatus status) => status switch
    {
        CourseOfferingStatus.Published => "Veröffentlicht",
        CourseOfferingStatus.SoldOut => "Ausgebucht",
        CourseOfferingStatus.Draft => "Entwurf",
        CourseOfferingStatus.Archived => "Archiviert",
        _ => status.ToString()
    };

    public static string RegistrationState(RegistrationStatus status) => status switch
    {
        RegistrationStatus.Received => "Eingegangen",
        RegistrationStatus.Reserved => "Vorgemerkt",
        RegistrationStatus.Confirmed => "Bestätigt",
        RegistrationStatus.Waitlisted => "Warteliste",
        RegistrationStatus.Rejected => "Abgelehnt",
        _ => status.ToString()
    };

    public static string RegistrationMode(CourseRegistrationMode mode) => mode switch
    {
        CourseRegistrationMode.Internal => "Intern",
        CourseRegistrationMode.External => "Extern",
        _ => mode.ToString()
    };

    public static string Day(DayOfWeek day) => GermanCulture.DateTimeFormat.GetDayName(day);
}
