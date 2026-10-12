namespace Aether.Tenancy.Domain;

public sealed record DailyWindow(TimeOnly Start, TimeOnly End);
public sealed class SendWindow
{
    private readonly TimeZoneInfo timezone;
    private readonly HashSet<DayOfWeek> days;
    private readonly HashSet<DateOnly> holidays;
    private readonly DailyWindow[] windows;

    public SendWindow(string timezoneId, IEnumerable<DayOfWeek> days,
        IEnumerable<DailyWindow> windows, IEnumerable<DateOnly>? holidays = null)
    {
        timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        this.days = days.ToHashSet();
        this.holidays = holidays?.ToHashSet() ?? [];
        this.windows = windows.OrderBy(w => w.Start).ToArray();
        if (this.days.Count == 0 || this.windows.Length == 0 ||
            this.windows.Any(w => w.Start >= w.End))
            throw new ArgumentException("Calendário inválido; dividir intervalos que cruzam meia-noite.");
    }
    public DateTimeOffset? NextAllowed(DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var local = TimeZoneInfo.ConvertTime(now, timezone);
        var first = DateOnly.FromDateTime(local.Date);
        for (var offset = 0; offset <= 366; offset++)
        {
            var date = first.AddDays(offset);
            if (!days.Contains(date.DayOfWeek) || holidays.Contains(date)) continue;
            foreach (var window in windows)
            {
                if (offset == 0 && TimeOnly.FromDateTime(local.DateTime) >= window.Start
                    && TimeOnly.FromDateTime(local.DateTime) < window.End)
                    return now < expiresAt ? now : null;
                var start = date.ToDateTime(window.Start, DateTimeKind.Unspecified);
                while (timezone.IsInvalidTime(start)) start = start.AddMinutes(1);
                var candidate = timezone.IsAmbiguousTime(start)
                    ? new DateTimeOffset(start, timezone.GetAmbiguousTimeOffsets(start).Max()).ToUniversalTime()
                    : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, timezone));
                if (candidate >= now && candidate < expiresAt &&
                    TimeOnly.FromDateTime(start) < window.End) return candidate;
            }
        }
        return null;
    }
}
