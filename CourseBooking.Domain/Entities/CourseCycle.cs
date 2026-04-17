using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class CourseCycle : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CourseOffering> CourseOfferings { get; set; } = new List<CourseOffering>();
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
