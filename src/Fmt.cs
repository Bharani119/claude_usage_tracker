using System;
using System.Globalization;

namespace ClaudeUsageTray
{
    static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Percent(double v)
        {
            return Math.Round(v).ToString(Inv);
        }

        // "Thu 15:30" for resets within a week, else the short date.
        public static string Day(DateTime t)
        {
            return ((t - DateTime.Now).TotalDays < 6 ? t.ToString("ddd") : t.ToString("d MMM")) + " " + t.ToString("t");
        }

        public static string Duration(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1
                ? string.Format(Inv, "{0}h {1:00}m", (int)t.TotalHours, t.Minutes)
                : string.Format(Inv, "{0}m", Math.Max(1, (int)Math.Ceiling(t.TotalMinutes)));
        }
    }
}
