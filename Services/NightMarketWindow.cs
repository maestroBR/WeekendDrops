using System.Globalization;
using WeekendDrops.Models;

namespace WeekendDrops.Services;

/// The market runs on its own schedule, independent of the weekend window. Times are
/// server-local to match WeekendWindow.
public static class NightMarketWindow
{
    private static readonly DateTime Epoch = new(2024, 1, 1);

    /// Start of the window covering `now`, or null when the market is shut. Yesterday is
    /// checked too so a window can run past midnight.
    private static DateTime? OpenWindowStart(NightMarketSchedule s, DateTime now)
    {
        int dur  = Math.Max(1, s.DurationHours);
        int hour = ((s.StartHour % 24) + 24) % 24;

        for (int back = 0; back <= 1; back++)
        {
            var start = now.Date.AddDays(-back).AddHours(hour);
            if (now < start || now >= start.AddHours(dur)) continue;
            if (!DayAllowed(s, start)) continue;
            return start;
        }

        return null;
    }

    // An empty day list means every night. Days are matched against the day the window
    // opens, so a window running past midnight still belongs to the night it started.
    private static bool DayAllowed(NightMarketSchedule s, DateTime start)
        => s.Days is null || s.Days.Count == 0 || s.Days.Contains((int)start.DayOfWeek);

    public static bool IsOpen(NightMarketSchedule s, DateTime now) => OpenWindowStart(s, now) is not null;

    public static double SecondsUntilClose(NightMarketSchedule s, DateTime now)
    {
        var start = OpenWindowStart(s, now);
        if (start is null) return 0;
        return Math.Max(0, (start.Value.AddHours(Math.Max(1, s.DurationHours)) - now).TotalSeconds);
    }

    public static double SecondsUntilOpen(NightMarketSchedule s, DateTime now)
    {
        int hour = ((s.StartHour % 24) + 24) % 24;

        for (int i = 0; i <= 8; i++)
        {
            var start = now.Date.AddDays(i).AddHours(hour);
            if (start <= now || !DayAllowed(s, start)) continue;
            return (start - now).TotalSeconds;
        }

        return 0;
    }

    /// Anchored to the window's start, not to now: a reroll boundary falling at midnight
    /// would otherwise swap the cards out from under a window that opened at 22:00.
    public static string CurrentPeriodId(NightMarketSchedule s, DateTime now)
    {
        var anchor = OpenWindowStart(s, now) ?? now;
        int days = Math.Max(1, s.RerollDays);
        int bucket = (int)Math.Floor((anchor.Date - Epoch).TotalDays / days);
        return Epoch.AddDays(bucket * (double)days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string ScheduleText(NightMarketSchedule s)
    {
        var culture = CultureInfo.CurrentCulture;
        int hour = ((s.StartHour % 24) + 24) % 24;
        int dur  = Math.Max(1, s.DurationHours);

        string Time(int h) => new TimeOnly(((h % 24) + 24) % 24, 0).ToString("t", culture);
        string when = $"{Time(hour)} to {Time(hour + dur)}";

        if (s.Days is null || s.Days.Count == 0) return $"Nightly, {when}";

        var names = s.Days
            .Select(d => culture.DateTimeFormat.AbbreviatedDayNames[((d % 7) + 7) % 7]);
        return $"{string.Join(", ", names)}, {when}";
    }
}
