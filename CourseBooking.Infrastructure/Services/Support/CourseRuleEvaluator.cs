using CourseBooking.Domain.Entities;
using CourseBooking.Domain.Enums;

namespace CourseBooking.Infrastructure.Services.Support;

internal static class CourseRuleEvaluator
{
    public static string BuildAgeLabel(AgeRule? rule)
    {
        if (rule is null)
        {
            return "Keine Altersgrenze";
        }

        var unitLabel = rule.Unit == AgeUnit.Months ? "Monate" : "Jahre";

        return (rule.MinimumValue, rule.MaximumValue) switch
        {
            (null, null) => "Keine Altersgrenze",
            (not null, null) => $"Ab {rule.MinimumValue} {unitLabel}",
            (null, not null) => $"Bis {rule.MaximumValue} {unitLabel}",
            _ => $"{rule.MinimumValue}-{rule.MaximumValue} {unitLabel}"
        };
    }

    public static bool MatchesAgeRule(DateOnly birthDate, DateOnly courseStart, AgeRule? rule, out string message)
    {
        if (rule is null)
        {
            message = "Keine Altersregel hinterlegt.";
            return true;
        }

        var ageValue = CalculateAgeValue(birthDate, courseStart, rule.Unit);

        if (rule.MinimumValue.HasValue && ageValue < rule.MinimumValue.Value)
        {
            message = $"Kind ist zu jung ({ageValue} {UnitLabel(rule.Unit)}).";
            return false;
        }

        if (rule.MaximumValue.HasValue && ageValue > rule.MaximumValue.Value)
        {
            message = $"Kind ist zu alt ({ageValue} {UnitLabel(rule.Unit)}).";
            return false;
        }

        message = $"Altersregel erfüllt ({ageValue} {UnitLabel(rule.Unit)}).";
        return true;
    }

    private static int CalculateAgeValue(DateOnly birthDate, DateOnly courseStart, AgeUnit unit)
    {
        var months = (courseStart.Year - birthDate.Year) * 12 + courseStart.Month - birthDate.Month;
        if (courseStart.Day < birthDate.Day)
        {
            months--;
        }

        return unit == AgeUnit.Months ? months : months / 12;
    }

    private static string UnitLabel(AgeUnit unit) => unit == AgeUnit.Months ? "Monate" : "Jahre";
}
