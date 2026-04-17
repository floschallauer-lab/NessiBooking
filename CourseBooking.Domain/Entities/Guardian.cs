using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class Guardian : EntityBase
{
    public string FullName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
