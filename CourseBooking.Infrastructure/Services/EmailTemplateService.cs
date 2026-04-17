using CourseBooking.Application.Abstractions;
using CourseBooking.Application.Dtos;
using CourseBooking.Domain.Entities;
using CourseBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services;

internal sealed class EmailTemplateService(CourseBookingDbContext dbContext, IAuditService auditService) : IEmailTemplateService
{
    public async Task<IReadOnlyCollection<EmailTemplateEditDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailTemplates
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new EmailTemplateEditDto(x.Id, x.Key, x.DisplayName, x.Description, x.SubjectTemplate, x.BodyTemplate, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(EmailTemplateEditDto template, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmailTemplates.FirstOrDefaultAsync(x => x.Id == template.Id, cancellationToken);
        if (entity is null)
        {
            entity = new EmailTemplate { Key = template.Key };
            dbContext.EmailTemplates.Add(entity);
        }

        entity.DisplayName = template.DisplayName.Trim();
        entity.Description = template.Description.Trim();
        entity.SubjectTemplate = template.SubjectTemplate.Trim();
        entity.BodyTemplate = template.BodyTemplate.Trim();
        entity.IsActive = template.IsActive;
        entity.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync("EmailTemplateSaved", nameof(EmailTemplate), entity.Id.ToString(), entity.Key, cancellationToken);
    }
}
