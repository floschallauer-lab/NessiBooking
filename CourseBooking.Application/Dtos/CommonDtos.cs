using CourseBooking.Domain.Enums;

namespace CourseBooking.Application.Dtos;

public sealed record LookupItemDto(Guid Id, string Label, string? SecondaryLabel = null);

public sealed record StatusOptionDto(int Value, string Label);

public sealed record CourseFilterDto(
    Guid? CategoryId = null,
    Guid? CourseTypeId = null,
    Guid? VenueId = null,
    Guid? CourseCycleId = null,
    DayOfWeek? DayOfWeek = null,
    CourseRegistrationMode? RegistrationMode = null,
    bool OnlyWithFreeSpots = false);

public sealed record CatalogPageDto(
    IReadOnlyCollection<LookupItemDto> Categories,
    IReadOnlyCollection<LookupItemDto> CourseTypes,
    IReadOnlyCollection<LookupItemDto> Venues,
    IReadOnlyCollection<LookupItemDto> Cycles,
    IReadOnlyCollection<CourseListItemDto> Courses);

public sealed record CourseListItemDto(
    Guid Id,
    string Title,
    string Category,
    string CourseType,
    string Venue,
    string? Cycle,
    string DayLabel,
    string TimeLabel,
    string PeriodLabel,
    string AgeLabel,
    decimal Price,
    int Capacity,
    int SeatsRemaining,
    CourseOfferingStatus Status,
    CourseRegistrationMode RegistrationMode,
    bool IsPubliclyBookable,
    bool AllowWaitlistWhenFull,
    string? ExternalRegistrationUrl,
    string CustomerNotice);

public sealed record CourseDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string CourseType,
    string Venue,
    string VenueAddress,
    string? Cycle,
    string InstructorName,
    string DayLabel,
    string TimeLabel,
    string PeriodLabel,
    string AgeLabel,
    decimal Price,
    int Capacity,
    int SeatsRemaining,
    CourseOfferingStatus Status,
    CourseRegistrationMode RegistrationMode,
    bool IsPubliclyBookable,
    bool AllowWaitlistWhenFull,
    string? ExternalRegistrationUrl,
    string CustomerNotice);

public sealed record RegistrationPriorityInputDto(Guid CourseOfferingId, int PriorityOrder);

public sealed class RegistrationCreateRequest
{
    public string GuardianFullName { get; set; } = string.Empty;
    public string ChildFullName { get; set; } = string.Empty;
    public DateOnly? ChildBirthDate { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? PreferredCourseCycleId { get; set; }
    public bool TermsAccepted { get; set; }
    public bool PrivacyAccepted { get; set; }
    public string? Note { get; set; }
    public List<RegistrationPriorityInputDto> Priorities { get; set; } = new();
}

public sealed record RegistrationSubmissionResult(Guid RegistrationId, string ConfirmationNumber, string Message);

public sealed record AssignmentResultDto(
    bool Assigned,
    bool Waitlisted,
    Guid? CourseOfferingId,
    string Summary,
    IReadOnlyCollection<string> ProtocolLines);
