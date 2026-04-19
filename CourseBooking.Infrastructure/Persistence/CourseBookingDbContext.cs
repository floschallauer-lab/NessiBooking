using CourseBooking.Domain.Entities;
using CourseBooking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Persistence;

public sealed class CourseBookingDbContext(DbContextOptions<CourseBookingDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();
    public DbSet<CourseType> CourseTypes => Set<CourseType>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<CourseCycle> CourseCycles => Set<CourseCycle>();
    public DbSet<AgeRule> AgeRules => Set<AgeRule>();
    public DbSet<CourseInstructor> CourseInstructors => Set<CourseInstructor>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<ChildParticipant> ChildParticipants => Set<ChildParticipant>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<RegistrationPriority> RegistrationPriorities => Set<RegistrationPriority>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CourseCategory>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Slug).HasMaxLength(120);
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<CourseType>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Slug).HasMaxLength(120);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasOne(x => x.CourseCategory)
                .WithMany(x => x.CourseTypes)
                .HasForeignKey(x => x.CourseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Venue>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Slug).HasMaxLength(120);
            entity.Property(x => x.AddressLine1).HasMaxLength(180);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.City).HasMaxLength(80);
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<CourseCycle>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<AgeRule>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<CourseInstructor>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(400);
        });

        builder.Entity<CourseOffering>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(180);
            entity.Property(x => x.InstructorName).HasMaxLength(120);
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RegistrationMode).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(x => x.CourseCategory)
                .WithMany(x => x.CourseOfferings)
                .HasForeignKey(x => x.CourseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseType)
                .WithMany(x => x.CourseOfferings)
                .HasForeignKey(x => x.CourseTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Venue)
                .WithMany(x => x.CourseOfferings)
                .HasForeignKey(x => x.VenueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CourseCycle)
                .WithMany(x => x.CourseOfferings)
                .HasForeignKey(x => x.CourseCycleId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AgeRule)
                .WithMany(x => x.CourseOfferings)
                .HasForeignKey(x => x.AgeRuleId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CourseInstructor)
                .WithMany(x => x.CourseOfferings)
                .HasForeignKey(x => x.CourseInstructorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Guardian>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.AddressLine1).HasMaxLength(250);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.PhoneNumber).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(200);
        });

        builder.Entity<ChildParticipant>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.HasIndex(x => new { x.FullName, x.BirthDate });
        });

        builder.Entity<Registration>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(x => x.Guardian)
                .WithMany(x => x.Registrations)
                .HasForeignKey(x => x.GuardianId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ChildParticipant)
                .WithMany(x => x.Registrations)
                .HasForeignKey(x => x.ChildParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssignedCourseOffering)
                .WithMany(x => x.AssignedRegistrations)
                .HasForeignKey(x => x.AssignedCourseOfferingId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PreferredCourseCycle)
                .WithMany(x => x.Registrations)
                .HasForeignKey(x => x.PreferredCourseCycleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RegistrationPriority>(entity =>
        {
            entity.HasIndex(x => new { x.RegistrationId, x.PriorityOrder }).IsUnique();
            entity.HasIndex(x => new { x.RegistrationId, x.CourseOfferingId }).IsUnique();
            entity.HasOne(x => x.Registration)
                .WithMany(x => x.Priorities)
                .HasForeignKey(x => x.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CourseOffering)
                .WithMany(x => x.RegistrationPriorities)
                .HasForeignKey(x => x.CourseOfferingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WaitlistEntry>(entity =>
        {
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.HasOne(x => x.Registration)
                .WithMany(x => x.WaitlistEntries)
                .HasForeignKey(x => x.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CourseOffering)
                .WithMany(x => x.WaitlistEntries)
                .HasForeignKey(x => x.CourseOfferingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmailTemplate>(entity =>
        {
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.ActorUserId).HasMaxLength(80);
            entity.Property(x => x.ActorEmail).HasMaxLength(200);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityName).HasMaxLength(120);
            entity.Property(x => x.EntityId).HasMaxLength(120);
            entity.Property(x => x.IpAddress).HasMaxLength(80);
        });
    }
}
