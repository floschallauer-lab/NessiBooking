using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;
using CourseBooking.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CourseBooking.Infrastructure.Services;

internal sealed class AdminCourseService(
    CourseBookingDbContext dbContext,
    IValidator<CourseUpsertRequest> validator,
    IAuditService auditService) : IAdminCourseService
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-AT");

    public async Task<CourseManagementDataDto> GetManagementDataAsync(CancellationToken cancellationToken = default)
    {
        var occupancy = await dbContext.Registrations
            .AsNoTracking()
            .Where(x => x.AssignedCourseOfferingId.HasValue && (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Reserved))
            .GroupBy(x => x.AssignedCourseOfferingId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        var courses = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(x => x.CourseCategory)
            .Include(x => x.CourseType)
            .Include(x => x.Venue)
            .Include(x => x.CourseCycle)
            .OrderBy(x => x.StartDate)
            .ToListAsync(cancellationToken);

        var rows = courses.Select(x => new CourseManagementRowDto(
            x.Id,
            x.Title,
            x.CourseCategory?.Name ?? string.Empty,
            x.CourseType?.Name ?? string.Empty,
            x.Venue?.Name ?? string.Empty,
            x.CourseCycle?.Name,
            GermanCulture.DateTimeFormat.GetDayName(x.DayOfWeek),
            $"{x.StartDate:dd.MM.yyyy} - {x.EndDate:dd.MM.yyyy}",
            x.Status,
            x.RegistrationMode,
            x.Capacity,
            x.Capacity - occupancy.GetValueOrDefault(x.Id),
            x.Status == CourseOfferingStatus.Archived)).ToList();

        return new CourseManagementDataDto(
            rows,
            await dbContext.CourseCategories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new LookupItemDto(x.Id, x.Name, null)).ToListAsync(cancellationToken),
            await dbContext.CourseTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new LookupItemDto(x.Id, x.Name, null)).ToListAsync(cancellationToken),
            await dbContext.Venues.AsNoTracking().OrderBy(x => x.Name).Select(x => new LookupItemDto(x.Id, x.Name, null)).ToListAsync(cancellationToken),
            await dbContext.CourseCycles.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new LookupItemDto(x.Id, x.Name, null)).ToListAsync(cancellationToken),
            await dbContext.AgeRules.AsNoTracking().OrderBy(x => x.Name).Select(x => new LookupItemDto(x.Id, x.Name, null)).ToListAsync(cancellationToken));
    }

    public async Task<CourseUpsertRequest?> GetCourseForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CourseUpsertRequest
            {
                Id = x.Id,
                CourseCategoryId = x.CourseCategoryId,
                CourseTypeId = x.CourseTypeId,
                VenueId = x.VenueId,
                CourseCycleId = x.CourseCycleId,
                AgeRuleId = x.AgeRuleId,
                Title = x.Title,
                Description = x.Description,
                InstructorName = x.InstructorName,
                CustomerNotice = x.CustomerNotice,
                Price = x.Price,
                Capacity = x.Capacity,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Status = x.Status,
                RegistrationMode = x.RegistrationMode,
                AllowWaitlistWhenFull = x.AllowWaitlistWhenFull,
                ExternalRegistrationUrl = x.ExternalRegistrationUrl
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> SaveAsync(CourseUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        CourseOffering entity;
        if (request.Id.HasValue)
        {
            entity = await dbContext.CourseOfferings.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Kurs nicht gefunden.");
        }
        else
        {
            entity = new CourseOffering();
            dbContext.CourseOfferings.Add(entity);
        }

        entity.CourseCategoryId = request.CourseCategoryId;
        entity.CourseTypeId = request.CourseTypeId;
        entity.VenueId = request.VenueId;
        entity.CourseCycleId = request.CourseCycleId;
        entity.AgeRuleId = request.AgeRuleId;
        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.InstructorName = request.InstructorName.Trim();
        entity.CustomerNotice = request.CustomerNotice.Trim();
        entity.Price = request.Price;
        entity.Capacity = request.Capacity;
        entity.StartDate = request.StartDate!.Value;
        entity.EndDate = request.EndDate!.Value;
        entity.DayOfWeek = request.DayOfWeek;
        entity.StartTime = request.StartTime!.Value;
        entity.EndTime = request.EndTime!.Value;
        entity.Status = request.Status;
        entity.RegistrationMode = request.RegistrationMode;
        entity.AllowWaitlistWhenFull = request.AllowWaitlistWhenFull;
        entity.ExternalRegistrationUrl = string.IsNullOrWhiteSpace(request.ExternalRegistrationUrl) ? null : request.ExternalRegistrationUrl.Trim();
        entity.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("CourseSaved", nameof(CourseOffering), entity.Id.ToString(), entity.Title, cancellationToken);
        return entity.Id;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CourseOfferings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Kurs nicht gefunden.");
        entity.Status = CourseOfferingStatus.Archived;
        entity.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("CourseArchived", nameof(CourseOffering), entity.Id.ToString(), entity.Title, cancellationToken);
    }
}
