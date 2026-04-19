using CourseBooking.Domain.Enums;

namespace CourseBooking.Application.Dtos;

public sealed record DashboardSummaryDto(
    IReadOnlyCollection<LookupItemDto> Categories,
    IReadOnlyCollection<CategorizedLookupItemDto> CourseTypes,
    IReadOnlyCollection<LookupItemDto> Venues,
    IReadOnlyCollection<LookupItemDto> Cycles,
    int OpenRegistrations,
    int ConfirmedRegistrations,
    int WaitlistEntries,
    int InternalCourses,
    int ExternalCourses,
    int BookableInternalCourses,
    int SoldOutCourses,
    int ExternalSoldOutCourses,
    int CoursesWithLowAvailability,
    IReadOnlyCollection<DashboardBreakdownRowDto> RegistrationStatusDistribution,
    IReadOnlyCollection<DashboardBreakdownRowDto> VenueDistribution,
    IReadOnlyCollection<DashboardBreakdownRowDto> CategoryDistribution,
    IReadOnlyCollection<DashboardBreakdownRowDto> ModeDistribution,
    IReadOnlyCollection<DashboardAttentionCourseDto> CoursesNeedingAttention,
    IReadOnlyCollection<DashboardRecentCourseDto> RecentCourses,
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

public sealed record DashboardRecentCourseDto(
    Guid Id,
    string Title,
    string Category,
    string CourseType,
    DateTime ChangedAtUtc,
    CourseOfferingStatus Status);

public sealed record CourseManagementDataDto(
    IReadOnlyCollection<CourseManagementRowDto> Courses,
    IReadOnlyCollection<LookupItemDto> Categories,
    IReadOnlyCollection<CategorizedLookupItemDto> CourseTypes,
    IReadOnlyCollection<LookupItemDto> Venues,
    IReadOnlyCollection<LookupItemDto> Cycles,
    IReadOnlyCollection<LookupItemDto> AgeRules,
    IReadOnlyCollection<LookupItemDto> Instructors,
    CourseManagementSummaryDto Summary);

public sealed record CourseManagementSummaryDto(
    int TotalCourses,
    int PublishedCourses,
    int DraftCourses,
    int InternalCourses,
    int ExternalCourses,
    int SoldOutCourses,
    int ArchivedCourses,
    int CoursesWithLowAvailability);

public sealed record CourseManagementRowDto(
    Guid Id,
    string Title,
    string Category,
    string CourseType,
    string Venue,
    string? Cycle,
    string InstructorName,
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
    public Guid? CourseInstructorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
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
    IReadOnlyCollection<LookupItemDto> Cycles,
    RegistrationAdminSummaryDto Summary);

public sealed record RegistrationAdminSummaryDto(
    int Total,
    int Open,
    int Confirmed,
    int Waitlisted,
    int Rejected);

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

public sealed record AdminCatalogDataDto(
    IReadOnlyCollection<CategoryManagementRowDto> Categories,
    IReadOnlyCollection<CourseTypeManagementRowDto> CourseTypes,
    IReadOnlyCollection<VenueManagementRowDto> Venues,
    IReadOnlyCollection<CourseCycleManagementRowDto> Cycles,
    IReadOnlyCollection<AgeRuleManagementRowDto> AgeRules,
    IReadOnlyCollection<CourseInstructorManagementRowDto> Instructors);

public sealed record CategoryManagementRowDto(
    Guid Id,
    string Name,
    string Description,
    int SortOrder,
    bool IsActive,
    int CourseTypeCount,
    int CourseCount);

public sealed class CourseCategoryUpsertRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record CourseTypeManagementRowDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Description,
    bool OnlyBookableOnce,
    int SortOrder,
    bool IsActive,
    int CourseCount);

public sealed class CourseTypeUpsertRequest
{
    public Guid? Id { get; set; }
    public Guid CourseCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool OnlyBookableOnce { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record VenueManagementRowDto(
    Guid Id,
    string Name,
    string AddressLine1,
    string PostalCode,
    string City,
    string Notes,
    bool IsActive,
    int CourseCount);

public sealed class VenueUpsertRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed record CourseCycleManagementRowDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    int SortOrder,
    bool IsActive,
    int CourseCount);

public sealed class CourseCycleUpsertRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record AgeRuleManagementRowDto(
    Guid Id,
    string Name,
    int? MinimumValue,
    int? MaximumValue,
    string Unit,
    string Notes,
    bool IsActive,
    int CourseCount);

public sealed class AgeRuleUpsertRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MinimumValue { get; set; }
    public int? MaximumValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed record CourseInstructorManagementRowDto(
    Guid Id,
    string FullName,
    string Description,
    int SortOrder,
    bool IsActive,
    int CourseCount);

public sealed class CourseInstructorUpsertRequest
{
    public Guid? Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
