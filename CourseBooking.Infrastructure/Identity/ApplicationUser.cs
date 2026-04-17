using Microsoft.AspNetCore.Identity;

namespace CourseBooking.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
