using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class CourseType : EntityBase
{
    public Guid CourseCategoryId { get; set; }
    public CourseCategory? CourseCategory { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool OnlyBookableOnce { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<CourseOffering> CourseOfferings { get; set; } = new List<CourseOffering>();
}
