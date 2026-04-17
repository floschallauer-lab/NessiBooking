using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class WaitlistEntry : EntityBase
{
    public Guid RegistrationId { get; set; }
    public Registration? Registration { get; set; }

    public Guid CourseOfferingId { get; set; }
    public CourseOffering? CourseOffering { get; set; }

    public int Position { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
