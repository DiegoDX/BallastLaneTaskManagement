namespace Application.Llm;

public static class TaskSuggestionDueDateResolver
{
    private const int MaxRandomDayOffset = 30;

    public static DateTime Resolve(
        DateTime? overrideDueDate,
        DateTime? rootDueDate,
        TimeProvider timeProvider,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (HasValue(overrideDueDate))
        {
            return overrideDueDate!.Value;
        }

        if (HasValue(rootDueDate))
        {
            return rootDueDate!.Value;
        }

        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
        var dayOffset = (random ?? Random.Shared).Next(MaxRandomDayOffset + 1);
        return today.AddDays(dayOffset);
    }

    private static bool HasValue(DateTime? value) =>
        value is not null && value.Value != default;
}
