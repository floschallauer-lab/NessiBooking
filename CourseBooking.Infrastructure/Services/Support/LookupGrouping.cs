using CourseBooking.Application.Dtos;

namespace CourseBooking.Infrastructure.Services.Support;

internal sealed record GroupedLookup(Guid Id, string Label, IReadOnlyCollection<Guid> MemberIds);

internal static class LookupGrouping
{
    public static IReadOnlyCollection<GroupedLookup> GroupByLabel(IEnumerable<LookupItemDto> items)
    {
        return items
            .GroupBy(x => NormalizeLabelValue(x.Label), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).First();
                return new GroupedLookup(first.Id, first.Label, group.Select(x => x.Id).ToList());
            })
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string NormalizeLabelValue(string value)
    {
        return string.Join(
            " ",
            value
                .Trim()
                .Replace("–", "-", StringComparison.Ordinal)
                .Replace("—", "-", StringComparison.Ordinal)
                .Replace("_", " ", StringComparison.Ordinal)
                .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }
}
