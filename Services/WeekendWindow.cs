using System.Globalization;
using WeekendDrops.Models;

namespace WeekendDrops.Services;

public static class WeekendWindow
{
    public static bool IsActive(ModConfig config)
    {
        if (config.DebugMode) return true;

        return IsActiveAt(config, DateTime.Now);
    }

    public static bool IsActiveAt(ModConfig config, DateTime now)
    {
        if (config.DebugMode) return true;
        const int weekHours = 7 * 24;
        int start = NormalizeDay(config.WeekendStartDay) * 24 + NormalizeHour(config.WeekendStartHour);
        int end = NormalizeDay(config.WeekendEndDay) * 24 + NormalizeHour(config.WeekendEndHour);
        int current = (int)now.DayOfWeek * 24 + now.Hour;
        int duration = (end - start + weekHours) % weekHours;
        // Identical boundaries represent an always-open weekly window.
        return duration == 0 || (current - start + weekHours) % weekHours < duration;
    }

    public static string CurrentId(ModConfig config) => CurrentIdAt(config, DateTime.Now);

    public static string CurrentIdAt(ModConfig config, DateTime now)
    {
        int daysSinceStart = ((int)now.DayOfWeek - NormalizeDay(config.WeekendStartDay) + 7) % 7;
        var anchor = now.Date.AddDays(-daysSinceStart).AddHours(NormalizeHour(config.WeekendStartHour));
        if (now < anchor) anchor = anchor.AddDays(-7);
        return anchor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static int NormalizeDay(int day) => ((day % 7) + 7) % 7;
    private static int NormalizeHour(int hour) => ((hour % 24) + 24) % 24;

    public static string ScheduleText(ModConfig config)
    {
        var culture = CultureInfo.InvariantCulture;
        string Day(int d) => culture.DateTimeFormat.AbbreviatedDayNames[((d % 7) + 7) % 7];
        string Time(int h) => new TimeOnly(((h % 24) + 24) % 24, 0).ToString("HH:mm", culture);
        return $"{Day(config.WeekendStartDay)} {Time(config.WeekendStartHour)} to " +
               $"{Day(config.WeekendEndDay)} {Time(config.WeekendEndHour)}";
    }

    public static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (var c in s) h = (h ^ c) * 16777619u;

            h ^= h >> 16;
            h *= 0x85ebca6bu;
            h ^= h >> 13;
            h *= 0xc2b2ae35u;
            h ^= h >> 16;

            return (int)(h & 0x7fffffff);
        }
    }
}
