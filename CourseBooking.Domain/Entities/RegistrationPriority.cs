using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class RegistrationPriority : EntityBase
{
    public Guid RegistrationId { get; set; }
    public Registration? Registration { get; set; }

    public Guid CourseOfferingId { get; set; }
    public CourseOffering? CourseOffering { get; set; }

    public int PriorityOrder { get; set; }
}
