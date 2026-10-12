// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Duejoro.Core;

/// <summary>The date arithmetic of Duejoro: how urgent something is, and when it comes round next.</summary>
public static class DueMath
{
    /// <summary>The longest reminder period offered (about two years).</summary>
    public const int MaxRemindDays = 730;

    public static DueStatus StatusOf(Obligation o, DateOnly today)
    {
        if (o.Done) return DueStatus.Done;
        if (o.DueDate is not { } due) return DueStatus.NoDate;
        if (due < today) return DueStatus.Overdue;
        return due.DayNumber - today.DayNumber <= Math.Clamp(o.RemindDaysBefore, 0, MaxRemindDays) ? DueStatus.DueSoon : DueStatus.Upcoming;
    }

    /// <summary>Days from today to the date: negative when it has passed. Null when there is no date.</summary>
    public static int? DaysLeft(Obligation o, DateOnly today) => o.DueDate is { } d ? d.DayNumber - today.DayNumber : null;

    /// <summary>"in 12 days", "tomorrow", "today", "3 days ago", "in 2 months", "1 year ago": a short, human way to say how far away a date is.</summary>
    public static string Describe(int? days)
    {
        if (days is not { } d) return "";
        if (d == 0) return "today";
        if (d == 1) return "tomorrow";
        if (d == -1) return "yesterday";
        var n = Math.Abs(d);
        string span = n < 60 ? $"{n} days" : n < 365 * 2 ? $"{(int)Math.Round(n / 30.4)} months" : $"{(int)Math.Round(n / 365.25)} years";
        return d > 0 ? "in " + span : span + " ago";
    }

    /// <summary>Adds one repeat period to a date. Month and year steps keep the day where they can ("31 January" plus a month is "28/29 February").</summary>
    public static DateOnly Advance(DateOnly from, RepeatUnit unit, int every)
    {
        var n = Math.Max(1, every);
        return unit switch
        {
            RepeatUnit.Days => from.AddDays(n),
            RepeatUnit.Weeks => from.AddDays(7 * n),
            RepeatUnit.Months => from.AddMonths(n),
            RepeatUnit.Years => from.AddYears(n),
            _ => from,
        };
    }

    /// <summary>
    /// The next date after a renewal. It counts on from the old date (not from today), so a license due on 1 March that is renewed late on
    /// 20 March is next due on 1 March of next year. If that is still in the past (several periods missed) it is advanced until it is not.
    /// </summary>
    public static DateOnly? NextDue(Obligation o, DateOnly today)
    {
        if (o.RepeatUnit == RepeatUnit.None || o.DueDate is not { } due) return null;
        var next = Advance(due, o.RepeatUnit, o.RepeatEvery);
        var guard = 0;
        while (next <= today && guard++ < 1000) next = Advance(next, o.RepeatUnit, o.RepeatEvery);
        return next;
    }

    /// <summary>"every month", "every 2 years", "does not repeat".</summary>
    public static string DescribeRepeat(RepeatUnit unit, int every)
    {
        if (unit == RepeatUnit.None) return "does not repeat";
        var n = Math.Max(1, every);
        var word = unit switch { RepeatUnit.Days => "day", RepeatUnit.Weeks => "week", RepeatUnit.Months => "month", _ => "year" };
        return n == 1 ? $"every {word}" : $"every {n} {word}s";
    }
}
