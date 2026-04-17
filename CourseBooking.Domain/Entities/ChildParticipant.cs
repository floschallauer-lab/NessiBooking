using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class ChildParticipant : EntityBase
{
    public string FullName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
