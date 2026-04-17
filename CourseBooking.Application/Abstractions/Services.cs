using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Application.Abstractions;

public interface IPublicCatalogService
{
    Task<CatalogPageDto> GetCatalogAsync(CourseFilterDto filter, CancellationToken cancellationToken = default);
    Task<CourseDetailDto?> GetCourseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LookupItemDto>> GetInternalCourseOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LookupItemDto>> GetCycleOptionsAsync(CancellationToken cancellationToken = default);
}

public interface IRegistrationService
{
    Task<RegistrationSubmissionResult> SubmitAsync(RegistrationCreateRequest request, CancellationToken cancellationToken = default);
}

public interface IRegistrationAssignmentService
{
    Task<AssignmentResultDto> AutoAssignAsync(Guid registrationId, CancellationToken cancellationToken = default);
    Task ManualAssignAsync(Guid registrationId, Guid courseOfferingId, string? adminNote, CancellationToken cancellationToken = default);
}

public interface IAdminDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(AdminDashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IAdminCourseService
{
    Task<CourseManagementDataDto> GetManagementDataAsync(CancellationToken cancellationToken = default);
    Task<CourseUpsertRequest?> GetCourseForEditAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> SaveAsync(CourseUpsertRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IAdminRegistrationService
{
    Task<RegistrationListPageDto> GetRegistrationsAsync(RegistrationAdminFilter filter, CancellationToken cancellationToken = default);
    Task<RegistrationDetailDto?> GetRegistrationAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, RegistrationStatus status, string? adminNotes, CancellationToken cancellationToken = default);
    Task UpdateAdminNotesAsync(Guid id, string adminNotes, CancellationToken cancellationToken = default);
    Task<string> ExportRegistrationsCsvAsync(RegistrationAdminFilter filter, CancellationToken cancellationToken = default);
    Task<string> ExportParticipantsCsvAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CourseParticipantDto>> GetParticipantsAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);
}

public interface IEmailTemplateService
{
    Task<IReadOnlyCollection<EmailTemplateEditDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(EmailTemplateEditDto template, CancellationToken cancellationToken = default);
}

public interface IEmailSenderService
{
    Task SendTemplatedEmailAsync(string templateKey, string recipientEmail, IDictionary<string, string> tokens, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task WriteAsync(string action, string entityName, string entityId, string details, CancellationToken cancellationToken = default);
}
