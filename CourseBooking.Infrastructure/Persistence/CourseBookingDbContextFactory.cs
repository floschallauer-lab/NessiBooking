using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CourseBooking.Infrastructure.Persistence;

public sealed class CourseBookingDbContextFactory : IDesignTimeDbContextFactory<CourseBookingDbContext>
{
    public CourseBookingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CourseBookingDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=coursebooking_dev;Username=postgres;Password=postgres",
            options => options.MigrationsAssembly(typeof(CourseBookingDbContext).Assembly.FullName));

        return new CourseBookingDbContext(optionsBuilder.Options);
    }
}
