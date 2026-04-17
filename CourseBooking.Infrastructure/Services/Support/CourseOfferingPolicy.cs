using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Infrastructure.Services.Support;

internal static class CourseOfferingPolicy
{
    public static bool IsPubliclyBookable(CourseOffering offering, int occupiedSeats)
    {
        return offering.RegistrationMode == CourseRegistrationMode.Internal
            && offering.Status == CourseOfferingStatus.Published
            && offering.SeatsRemaining(occupiedSeats) > 0;
    }

    public static bool IsOperationallySoldOut(CourseOffering offering, int occupiedSeats)
    {
        return offering.RegistrationMode == CourseRegistrationMode.Internal
            && offering.Status == CourseOfferingStatus.Published
            && offering.SeatsRemaining(occupiedSeats) <= 0;
    }

    public static bool HasLowAvailability(CourseOffering offering, int occupiedSeats)
    {
        var seatsRemaining = offering.SeatsRemaining(occupiedSeats);
        return offering.RegistrationMode == CourseRegistrationMode.Internal
            && offering.Status == CourseOfferingStatus.Published
            && seatsRemaining > 0
            && seatsRemaining <= 2;
    }
}
