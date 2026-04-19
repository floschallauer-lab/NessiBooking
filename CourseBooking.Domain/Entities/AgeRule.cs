using CourseBooking.Domain.Common;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Domain.Entities;

public sealed class AgeRule : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public int? MinimumValue { get; set; }
    public int? MaximumValue { get; set; }
    public AgeUnit Unit { get; set; } = AgeUnit.Years;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<CourseOffering> CourseOfferings { get; set; } = new List<CourseOffering>();
}
