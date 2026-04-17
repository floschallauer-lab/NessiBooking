using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class CourseCategory : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CourseType> CourseTypes { get; set; } = new List<CourseType>();
    public ICollection<CourseOffering> CourseOfferings { get; set; } = new List<CourseOffering>();
}
