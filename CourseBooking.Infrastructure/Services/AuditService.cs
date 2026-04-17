using CourseBooking.Application.Abstractions;
using CourseBooking.Domain.Entities;
using CourseBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace CourseBooking.Infrastructure.Services;

internal sealed class AuditService(
    CourseBookingDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task WriteAsync(string action, string entityName, string entityId, string details, CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var log = new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            ActorUserId = user?.FindFirst("sub")?.Value ?? user?.Identity?.Name ?? "system",
            ActorEmail = user?.Identity?.Name ?? "system",
            IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
