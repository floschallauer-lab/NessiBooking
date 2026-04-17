using CourseBooking.Domain.Enums;

namespace CourseBooking.Application.Dtos;

public sealed record DashboardSummaryDto(
    IReadOnlyCollection<LookupItemDto> Categories,
    IReadOnlyCollection<LookupItemDto> CourseTypes,
    IReadOnlyCollection<LookupItemDto> Venues,
    IReadOnlyCollection<LookupItemDto> Cycles,
    int OpenRegistrations,
    int ConfirmedRegistrations,
    int WaitlistEntries,
    int InternalCourses,
    int ExternalCourses,
    int BookableInternalCourses,
    int SoldOutCourses,
    int CoursesWithLowAvailability,
    IReadOnlyCollection<DashboardBreakdownRowDto> RegistrationStatusDistribution,
    IReadOnlyCollection<DashboardBreakdownRowDto> VenueDistribution,
    IReadOnlyCollection<DashboardBreakdownRowDto> CategoryDistribution,
    IReadOnlyCollection<DashboardBreakdownRowDto> ModeDistribution,
    IReadOnlyCollection<DashboardAttentionCourseDto> CoursesNeedingAttention,
    IReadOnlyCollection<DashboardRecentRegistrationDto> RecentRegistrations);

public sealed record AdminDashboardFilter(
    Guid? CategoryId = null,
    Guid? CourseTypeId = null,
    Guid? VenueId = null,
    Guid? CourseCycleId = null,
    CourseRegistrationMode? RegistrationMode = null,
    RegistrationStatus? RegistrationStatus = null);

public sealed record DashboardBreakdownRowDto(string Label, int Value, int Total);

public sealed record DashboardAttentionCourseDto(
    Guid Id,
    string Title,
    string Venue,
    string ModeLabel,
    int SeatsRemaining,
    int Capacity,
    int OpenRegistrations,
    int WaitlistCount,
    bool IsLowAvailability,
    bool IsSoldOut);

public sealed record DashboardRecentRegistrationDto(
    Guid Id,
    DateTime SubmittedAtUtc,
    string GuardianName,
    string ChildName,
    RegistrationStatus Status,
    string? AssignedCourse);

public sealed record CourseManagementDataDto(
    IReadOnlyCollection<CourseManagementRowDto> Courses,
    IReadOnlyCollection<LookupItemDto> Categories,
    IReadOnlyCollection<LookupItemDto> CourseTypes,
    IReadOnlyCollection<LookupItemDto> Venues,
    IReadOnlyCollection<LookupItemDto> Cycles,
    IReadOnlyCollection<LookupItemDto> AgeRules);

public sealed record CourseManagementRowDto(
    Guid Id,
    string Title,
    string Category,
    string CourseType,
    string Venue,
    string? Cycle,
    string DayLabel,
    string PeriodLabel,
    CourseOfferingStatus Status,
    CourseRegistrationMode RegistrationMode,
    int Capacity,
    int SeatsRemaining,
    bool Archived);

public sealed class CourseUpsertRequest
{
    public Guid? Id { get; set; }
    public Guid CourseCategoryId { get; set; }
    public Guid CourseTypeId { get; set; }
    public Guid VenueId { get; set; }
    public Guid? CourseCycleId { get; set; }
    public Guid? AgeRuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string CustomerNotice { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public CourseOfferingStatus Status { get; set; } = CourseOfferingStatus.Published;
    public CourseRegistrationMode RegistrationMode { get; set; } = CourseRegistrationMode.Internal;
    public bool AllowWaitlistWhenFull { get; set; } = true;
    public string? ExternalRegistrationUrl { get; set; }
}

public sealed record RegistrationAdminFilter(
    RegistrationStatus? Status = null,
    Guid? CourseOfferingId = null,
    Guid? VenueId = null,
    Guid? CourseCycleId = null,
    CourseRegistrationMode? RegistrationMode = null);

public sealed record RegistrationListPageDto(
    IReadOnlyCollection<RegistrationRowDto> Registrations,
    IReadOnlyCollection<LookupItemDto> Courses,
    IReadOnlyCollection<LookupItemDto> Venues,
    IReadOnlyCollection<LookupItemDto> Cycles);

public sealed record RegistrationRowDto(
    Guid Id,
    DateTime SubmittedAtUtc,
    string GuardianName,
    string ChildName,
    string Email,
    RegistrationStatus Status,
    string? AssignedCourse,
    string PrioritySummary);

public sealed record RegistrationPriorityDetailDto(Guid CourseOfferingId, int PriorityOrder, string CourseLabel);

public sealed record RegistrationDetailDto(
    Guid Id,
    DateTime SubmittedAtUtc,
    RegistrationStatus Status,
    string GuardianName,
    string ChildName,
    DateOnly ChildBirthDate,
    string AddressLine1,
    string PostalCode,
    string City,
    string PhoneNumber,
    string Email,
    string? PreferredCycle,
    string Note,
    string AdminNotes,
    string AssignmentProtocol,
    Guid? AssignedCourseOfferingId,
    string? AssignedCourseTitle,
    IReadOnlyCollection<RegistrationPriorityDetailDto> Priorities);

public sealed record CourseParticipantDto(
    Guid RegistrationId,
    string ChildName,
    DateOnly BirthDate,
    string GuardianName,
    string Email,
    string PhoneNumber);

public sealed record EmailTemplateEditDto(
    Guid Id,
    string Key,
    string DisplayName,
    string Description,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsActive);
