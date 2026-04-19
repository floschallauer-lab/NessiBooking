using FluentValidation;

namespace CourseBooking.Web.Components.Shared;

public static class UiFeedback
{
    public static string FromException(Exception exception, string fallbackMessage)
    {
        return exception switch
        {
            ValidationException validationException => BuildValidationMessage(validationException, fallbackMessage),
            InvalidOperationException invalidOperationException => BuildText(invalidOperationException.Message, fallbackMessage),
            ArgumentException argumentException => BuildText(argumentException.Message, fallbackMessage),
            _ => fallbackMessage
        };
    }

    private static string BuildValidationMessage(ValidationException exception, string fallbackMessage)
    {
        var messages = exception.Errors
            .Select(error => error.ErrorMessage?.Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return messages.Length == 0
            ? BuildText(exception.Message, fallbackMessage)
            : string.Join(" ", messages);
    }

    private static string BuildText(string? text, string fallbackMessage)
        => string.IsNullOrWhiteSpace(text) ? fallbackMessage : text.Trim();
}
