using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class Venue : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<CourseOffering> CourseOfferings { get; set; } = new List<CourseOffering>();
}
