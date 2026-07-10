namespace Application.Llm.TaskAssistant;

public static class NaturalDueDateParser
{
    public static DateTime? TryParse(string? dueDate, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (string.IsNullOrWhiteSpace(dueDate))
        {
            return null;
        }

        if (!DateTime.TryParse(dueDate.Trim(), out var parsed))
        {
            return null;
        }

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }
}
