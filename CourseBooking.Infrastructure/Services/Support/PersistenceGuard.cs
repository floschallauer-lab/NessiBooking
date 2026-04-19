using Microsoft.EntityFrameworkCore;

namespace CourseBooking.Infrastructure.Services.Support;

internal static class PersistenceGuard
{
    public static async Task SaveChangesAsync(
        DbContext dbContext,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(userMessage, ex);
        }
    }
}
