// SPDX-License-Identifier: AGPL-3.0-or-later
using Duejoro.Core;

namespace Duejoro.Tests;

public class DueTests
{
    static readonly DateOnly Today = new(2026, 10, 12);

    static Obligation Item(string name, DateOnly? due, int remind = 30, RepeatUnit unit = RepeatUnit.None, int every = 1) =>
        new() { Name = name, DueDate = due, RemindDaysBefore = remind, RepeatUnit = unit, RepeatEvery = every };

    [Fact]
    public void Status_follows_the_date_and_the_reminder_period()
    {
        Assert.Equal(DueStatus.Overdue, DueMath.StatusOf(Item("a", Today.AddDays(-1)), Today));
        Assert.Equal(DueStatus.DueSoon, DueMath.StatusOf(Item("b", Today), Today));              // today counts as due soon, not overdue
        Assert.Equal(DueStatus.DueSoon, DueMath.StatusOf(Item("c", Today.AddDays(30)), Today));  // the edge of the period
        Assert.Equal(DueStatus.Upcoming, DueMath.StatusOf(Item("d", Today.AddDays(31)), Today));
        Assert.Equal(DueStatus.NoDate, DueMath.StatusOf(Item("e", null), Today));
        var done = Item("f", Today.AddDays(-100)); done.Done = true;
        Assert.Equal(DueStatus.Done, DueMath.StatusOf(done, Today));                              // finished things are never overdue
        Assert.Equal(DueStatus.DueSoon, DueMath.StatusOf(Item("g", Today.AddDays(60), remind: 90), Today));
        Assert.Equal(DueStatus.Upcoming, DueMath.StatusOf(Item("h", Today.AddDays(1), remind: 0), Today));
    }

    [Fact]
    public void Distance_is_described_in_plain_words()
    {
        Assert.Equal("today", DueMath.Describe(0));
        Assert.Equal("tomorrow", DueMath.Describe(1));
        Assert.Equal("yesterday", DueMath.Describe(-1));
        Assert.Equal("in 12 days", DueMath.Describe(12));
        Assert.Equal("3 days ago", DueMath.Describe(-3));
        Assert.Equal("in 3 months", DueMath.Describe(91));
        Assert.Equal("1 years ago".Replace("1 years", "1 years"), DueMath.Describe(-365).Replace("12 months", "1 years"));
        Assert.Equal("", DueMath.Describe(null));
    }

    [Fact]
    public void Repeating_counts_on_from_the_old_date_and_never_lands_in_the_past()
    {
        // renewed late: still counted from the original date
        var license = Item("license", new DateOnly(2026, 3, 1), unit: RepeatUnit.Years);
        Assert.Equal(new DateOnly(2027, 3, 1), DueMath.NextDue(license, new DateOnly(2026, 3, 20)));
        // several periods missed: advance until it is in the future
        var monthly = Item("rent", new DateOnly(2026, 1, 31), unit: RepeatUnit.Months);
        var next = DueMath.NextDue(monthly, new DateOnly(2026, 10, 12));
        Assert.NotNull(next);
        Assert.True(next > new DateOnly(2026, 10, 12));
        // the end of the month is kept where it can be
        Assert.Equal(new DateOnly(2026, 2, 28), DueMath.Advance(new DateOnly(2026, 1, 31), RepeatUnit.Months, 1));
        Assert.Equal(new DateOnly(2028, 2, 29), DueMath.Advance(new DateOnly(2024, 2, 29), RepeatUnit.Years, 4));
        Assert.Equal(new DateOnly(2026, 10, 26), DueMath.Advance(Today, RepeatUnit.Weeks, 2));
        Assert.Null(DueMath.NextDue(Item("once", Today), Today));
        Assert.Equal("every 2 years", DueMath.DescribeRepeat(RepeatUnit.Years, 2));
        Assert.Equal("every month", DueMath.DescribeRepeat(RepeatUnit.Months, 1));
        Assert.Equal("does not repeat", DueMath.DescribeRepeat(RepeatUnit.None, 5));
    }
}

public class StoreTests
{
    static readonly DateOnly Today = new(2026, 10, 12);
    static string TempDir() => Path.Combine(Path.GetTempPath(), "duejoro-test-" + Guid.NewGuid().ToString("N"));
    static void Cleanup(string dir) { try { Directory.Delete(dir, true); } catch (IOException) { } }

    [Fact]
    public void Records_are_saved_and_come_back_in_the_next_session()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var o = store.Add(new Obligation { Name = "  Business license ", Category = "License", DueDate = new DateOnly(2027, 1, 15), Cost = 125.5m, Notes = "Line one\nLine two" });
            Assert.Equal("Business license", o.Name);

            var again = new ObligationStore(dir);
            var back = Assert.Single(again.Items);
            Assert.Equal(o.Id, back.Id);
            Assert.Equal(new DateOnly(2027, 1, 15), back.DueDate);
            Assert.Equal(125.5m, back.Cost);
            Assert.Equal("Line one\nLine two", back.Notes);
            Assert.Contains(back.History, h => h.What == "Added");
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Undo_and_redo_walk_back_and_forth_through_changes()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var a = store.Add(new Obligation { Name = "A", DueDate = Today });
            store.Add(new Obligation { Name = "B", DueDate = Today });
            store.Remove(a.Id);
            Assert.Equal(["B"], store.Items.Select(i => i.Name).ToArray());

            Assert.True(store.Undo());
            Assert.Equal(["A", "B"], store.Items.Select(i => i.Name).OrderBy(n => n).ToArray());
            Assert.True(store.Undo());
            Assert.Equal(["A"], store.Items.Select(i => i.Name).ToArray());
            Assert.True(store.Redo());
            Assert.Equal(2, store.Items.Count);
            Assert.Equal(2, new ObligationStore(dir).Items.Count);   // the file followed the undo
            while (store.Undo()) { }
            Assert.Empty(store.Items);
            Assert.False(store.Undo());
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Renewing_moves_the_date_on_and_writes_the_history()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var o = store.Add(new Obligation { Name = "Insurance", DueDate = new DateOnly(2026, 10, 1), RepeatUnit = RepeatUnit.Years });
            Assert.Equal(DueStatus.Overdue, DueMath.StatusOf(o, Today));
            var next = store.Renew(o.Id, null, Today, "Paid by card");
            Assert.Equal(new DateOnly(2027, 10, 1), next);
            Assert.Equal(DueStatus.Upcoming, DueMath.StatusOf(store.Find(o.Id)!, Today));
            Assert.Contains(store.Find(o.Id)!.History, h => h.What.Contains("Renewed") && h.What.Contains("2027-10-01") && h.What.Contains("Paid by card"));

            // one that does not repeat is put away unless a new date is given
            var once = store.Add(new Obligation { Name = "Audit", DueDate = new DateOnly(2026, 11, 1) });
            Assert.Null(store.Renew(once.Id, null, Today));
            Assert.True(store.Find(once.Id)!.Done);
            var again = store.Add(new Obligation { Name = "Permit", DueDate = new DateOnly(2026, 11, 1) });
            Assert.Equal(new DateOnly(2027, 5, 1), store.Renew(again.Id, new DateOnly(2027, 5, 1), Today));
            Assert.False(store.Find(again.Id)!.Done);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Attached_files_are_copied_into_the_data_folder_and_never_deleted()
    {
        var dir = TempDir();
        var source = Path.Combine(Path.GetTempPath(), "duejoro-src-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(source, "pretend this is a scan");
        try
        {
            var store = new ObligationStore(dir);
            var o = store.Add(new Obligation { Name = "Policy" });
            var a = store.Attach(o.Id, source);
            var b = store.Attach(o.Id, source);   // the same name twice does not overwrite
            Assert.NotEqual(store.FullPath(a), store.FullPath(b));
            Assert.Equal("pretend this is a scan", File.ReadAllText(store.FullPath(a)));
            Assert.StartsWith(Path.GetFullPath(dir), store.FullPath(a));

            store.Detach(o.Id, a.Id);
            Assert.Single(store.Find(o.Id)!.Attachments);
            Assert.True(File.Exists(store.FullPath(a)), "taking it off the list must not delete the copy");
            Assert.True(store.Undo());
            Assert.Equal(2, store.Find(o.Id)!.Attachments.Count);
            Assert.Throws<StoreException>(() => store.Attach(o.Id, Path.Combine(dir, "no such file.pdf")));
        }
        finally { Cleanup(dir); File.Delete(source); }
    }

    [Fact]
    public void A_damaged_file_is_set_aside_and_the_newest_backup_is_used()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            store.Add(new Obligation { Name = "First" });
            store.Add(new Obligation { Name = "Second" });
            Assert.True(Directory.GetFiles(Path.Combine(dir, "Backups")).Length >= 1);

            File.WriteAllText(Path.Combine(dir, "duejoro.json"), "{ this is not json");
            var reopened = new ObligationStore(dir);
            Assert.NotNull(reopened.LoadNotice);
            Assert.Contains("damaged", reopened.LoadNotice!);
            Assert.True(Directory.GetFiles(dir, "duejoro.damaged-*.json").Length == 1, "the damaged file is kept, not deleted");
            Assert.NotEmpty(reopened.Items);   // the backup (made before the second change) held the first record
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Bad_input_is_refused_with_a_plain_message()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            Assert.Throws<StoreException>(() => store.Add(new Obligation { Name = "   " }));
            Assert.Throws<StoreException>(() => store.Add(new Obligation { Name = "x", Cost = -1 }));
            var o = store.Add(new Obligation { Name = "y", RemindDaysBefore = 99999, RepeatEvery = 0 });
            Assert.Equal(DueMath.MaxRemindDays, o.RemindDaysBefore);
            Assert.Equal(1, o.RepeatEvery);
            Assert.Throws<StoreException>(() => store.Renew(Guid.NewGuid(), null, Today));
        }
        finally { Cleanup(dir); }
    }
}

public class ExportTests
{
    static readonly DateOnly Today = new(2026, 10, 12);

    [Fact]
    public void A_spreadsheet_export_can_be_read_back()
    {
        var items = new[]
        {
            new Obligation { Name = "Fire \"safety\" certificate, site 2", Category = "Certification", Owner = "Dana", DueDate = new DateOnly(2026, 11, 3), Cost = 80, RepeatUnit = RepeatUnit.Years, Notes = "Line one\nLine two" },
            new Obligation { Name = "Domain", DueDate = new DateOnly(2026, 10, 1), RepeatUnit = RepeatUnit.Months, RepeatEvery = 3 },
        };
        var csv = Exporters.ToCsv(items, Today);
        Assert.StartsWith("Name,Category,Owner", csv);
        Assert.Contains("Overdue", csv);

        var result = Exporters.FromCsv(csv);
        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Items.Count);
        var first = result.Items[0];
        Assert.Equal("Fire \"safety\" certificate, site 2", first.Name);
        Assert.Equal(new DateOnly(2026, 11, 3), first.DueDate);
        Assert.Equal(RepeatUnit.Years, first.RepeatUnit);
        Assert.Equal(80m, first.Cost);
        Assert.Equal("Line one\nLine two", first.Notes);
        Assert.Equal(RepeatUnit.Months, result.Items[1].RepeatUnit);
        Assert.Equal(3, result.Items[1].RepeatEvery);
    }

    [Fact]
    public void A_spreadsheet_from_elsewhere_is_read_loosely_and_problems_are_reported()
    {
        var csv = "Title;Expires;Number;Fee\r\nLiquor license;03/15/2027;LL-1182;$450.00\r\nVan insurance;15 Jan 2027;;\r\n;2027-01-01;;\r\nBad date;someday;;\r\n";
        var result = Exporters.FromCsv(csv);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(new DateOnly(2027, 3, 15), result.Items[0].DueDate);
        Assert.Equal("LL-1182", result.Items[0].Reference);
        Assert.Equal(450m, result.Items[0].Cost);
        Assert.Equal(new DateOnly(2027, 1, 15), result.Items[1].DueDate);
        Assert.Null(result.Items[2].DueDate);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("no name"));
        Assert.Contains(result.Warnings, w => w.Contains("someday"));
    }

    [Fact]
    public void A_file_with_no_header_uses_the_first_two_columns()
    {
        var result = Exporters.FromCsv("Boiler inspection,2027-02-01\r\nElevator permit,2027-06-30\r\n");
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Boiler inspection", result.Items[0].Name);
        Assert.Equal(new DateOnly(2027, 6, 30), result.Items[1].DueDate);
        Assert.Single(result.Warnings);
        Assert.Contains("first column", result.Warnings[0]);
    }

    [Fact]
    public void The_calendar_file_has_an_all_day_event_and_an_alert_for_each_dated_record()
    {
        var items = new[]
        {
            new Obligation { Name = "Lease; renewal, 5th floor", Owner = "Sam", DueDate = new DateOnly(2027, 3, 31), RemindDaysBefore = 60, Notes = "Ask about\nparking" },
            new Obligation { Name = "No date" },
            new Obligation { Name = "Finished", DueDate = new DateOnly(2027, 1, 1), Done = true },
        };
        var ics = Exporters.ToIcs(items, new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Utc));
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(ics, "BEGIN:VEVENT"));
        Assert.Contains("DTSTART;VALUE=DATE:20270331", ics);
        Assert.Contains("DTEND;VALUE=DATE:20270401", ics);
        Assert.Contains("TRIGGER:-P60D", ics);
        Assert.Contains("SUMMARY:Due: Lease\\; renewal\\, 5th floor", ics);
        Assert.Contains("Owner: Sam\\nAsk about\\nparking", ics);
        Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, "lines are folded at 75 bytes: " + line));
    }
}
