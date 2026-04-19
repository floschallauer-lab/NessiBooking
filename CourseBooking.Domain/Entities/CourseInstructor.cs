using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class CourseInstructor : EntityBase
{
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<CourseOffering> CourseOfferings { get; set; } = new List<CourseOffering>();
}
