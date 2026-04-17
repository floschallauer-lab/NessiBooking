using CourseBooking.Application.Abstractions;
using CourseBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CourseBooking.Infrastructure.Services;

internal sealed class FileSystemEmailSenderService(
    CourseBookingDbContext dbContext,
    IHostEnvironment hostEnvironment,
    ILogger<FileSystemEmailSenderService> logger) : IEmailSenderService
{
    public async Task SendTemplatedEmailAsync(string templateKey, string recipientEmail, IDictionary<string, string> tokens, CancellationToken cancellationToken = default)
    {
        var template = await dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == templateKey && x.IsActive, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Email template {TemplateKey} not found.", templateKey);
            return;
        }

        var subject = Render(template.SubjectTemplate, tokens);
        var body = Render(template.BodyTemplate, tokens);
        var outputDirectory = Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "SentEmails");
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{SanitizeFileName(recipientEmail)}_{templateKey}.txt";
        var path = Path.Combine(outputDirectory, fileName);
        await File.WriteAllTextAsync(path, $"TO: {recipientEmail}{Environment.NewLine}SUBJECT: {subject}{Environment.NewLine}{Environment.NewLine}{body}", cancellationToken);
        logger.LogInformation("Email written to {Path}", path);
    }

    private static string Render(string template, IDictionary<string, string> tokens)
    {
        var output = template;
        foreach (var (key, value) in tokens)
        {
            output = output.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
