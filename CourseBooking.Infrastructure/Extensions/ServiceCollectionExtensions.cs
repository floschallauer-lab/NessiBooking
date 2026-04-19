using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Constants;
using CourseBooking.Application.Dtos;
using CourseBooking.Application.Validation;
using CourseBooking.Infrastructure.Identity;
using CourseBooking.Infrastructure.Persistence;
using CourseBooking.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CourseBooking.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<CourseBookingDbContext>(options =>
            options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(typeof(CourseBookingDbContext).Assembly.FullName)));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CourseBookingDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin));

        services.AddHttpContextAccessor();

        services.AddScoped<IValidator<RegistrationCreateRequest>, RegistrationCreateRequestValidator>();
        services.AddScoped<IValidator<CourseUpsertRequest>, CourseUpsertRequestValidator>();

        services.AddScoped<IPublicCatalogService, PublicCatalogService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IRegistrationAssignmentService, RegistrationAssignmentService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminCourseService, AdminCourseService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IAdminRegistrationService, AdminRegistrationService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailSenderService, FileSystemEmailSenderService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
