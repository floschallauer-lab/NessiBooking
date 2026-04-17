using CourseBooking.Domain.Common;

namespace CourseBooking.Domain.Entities;

public sealed class AuditLog : EntityBase
{
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
