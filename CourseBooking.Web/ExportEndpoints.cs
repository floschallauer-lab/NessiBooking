using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Constants;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Web;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapCourseBookingExports(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/exports/registrations.csv", async (
                IAdminRegistrationService service,
                Guid? courseOfferingId,
                Guid? venueId,
                Guid? courseCycleId,
                string? status,
                string? registrationMode) =>
            {
                RegistrationStatus? parsedStatus = null;
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RegistrationStatus>(status, true, out var enumStatus))
                {
                    parsedStatus = enumStatus;
                }

                CourseRegistrationMode? parsedMode = null;
                if (!string.IsNullOrWhiteSpace(registrationMode) && Enum.TryParse<CourseRegistrationMode>(registrationMode, true, out var enumMode))
                {
                    parsedMode = enumMode;
                }

                var csv = await service.ExportRegistrationsCsvAsync(
                    new RegistrationAdminFilter(parsedStatus, courseOfferingId, venueId, courseCycleId, parsedMode));
                return Results.Text(csv, "text/csv");
            })
            .RequireAuthorization(PolicyNames.AdminOnly);

        endpoints.MapGet("/admin/exports/courses/{courseOfferingId:guid}/participants.csv", async (
                Guid courseOfferingId,
                IAdminRegistrationService service) =>
            {
                var csv = await service.ExportParticipantsCsvAsync(courseOfferingId);
                return Results.Text(csv, "text/csv");
            })
            .RequireAuthorization(PolicyNames.AdminOnly);

        return endpoints;
    }
}
