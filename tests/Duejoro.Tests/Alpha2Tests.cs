// SPDX-License-Identifier: AGPL-3.0-or-later
using Duejoro.Core;

namespace Duejoro.Tests;

public class LinkTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("example.com/renew", "https://example.com/renew")]
    [InlineData("https://example.com/a?b=1", "https://example.com/a?b=1")]
    [InlineData("http://example.org", "http://example.org")]
    public void Web_addresses_are_accepted_and_completed(string typed, string expected) =>
        Assert.Equal(expected, Obligation.NormalizeLink(typed));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ftp://example.com/file")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("not a link")]
    public void Anything_that_is_not_a_web_address_is_refused(string typed) =>
        Assert.Null(Obligation.NormalizeLink(typed));
}

public class SnoozeAndBulkTests
{
    static readonly DateOnly Today = new(2026, 10, 12);
    static string TempDir() => Path.Combine(Path.GetTempPath(), "duejoro-test-" + Guid.NewGuid().ToString("N"));
    static void Cleanup(string dir) { try { Directory.Delete(dir, true); } catch (IOException) { } }

    [Fact]
    public void A_snoozed_item_keeps_its_status_but_stops_needing_attention_until_the_day_passes()
    {
        var o = new Obligation { Name = "Permit", DueDate = Today.AddDays(5), RemindDaysBefore = 30, SnoozedUntil = Today.AddDays(3) };
        Assert.Equal(DueStatus.DueSoon, DueMath.StatusOf(o, Today));
        Assert.True(DueMath.IsSnoozed(o, Today));
        Assert.False(DueMath.NeedsAttention(o, Today));
        Assert.True(DueMath.NeedsAttention(o, Today.AddDays(4)));
        Assert.False(DueMath.IsSnoozed(o, Today.AddDays(4)));
    }

    [Fact]
    public void Snooze_is_stored_logged_undoable_and_cleared_by_a_renewal()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var o = store.Add(new Obligation { Name = "Permit", DueDate = Today.AddDays(5), RepeatUnit = RepeatUnit.Years });
            store.Snooze(o.Id, Today.AddDays(2), Today);
            Assert.Equal(Today.AddDays(2), new ObligationStore(dir).Find(o.Id)!.SnoozedUntil);
            Assert.Contains(store.Find(o.Id)!.History, h => h.What.StartsWith("Snoozed until"));

            Assert.True(store.Undo());
            Assert.Null(store.Find(o.Id)!.SnoozedUntil);
            Assert.True(store.Redo());

            store.Renew(o.Id, null, Today);
            Assert.Null(store.Find(o.Id)!.SnoozedUntil);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Snooze_must_be_a_future_day_within_a_year()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var o = store.Add(new Obligation { Name = "Permit", DueDate = Today.AddDays(5) });
            Assert.Throws<StoreException>(() => store.Snooze(o.Id, Today, Today));
            Assert.Throws<StoreException>(() => store.Snooze(o.Id, Today.AddDays(400), Today));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Renewing_several_moves_each_on_by_its_own_repeat_and_skips_the_rest_in_one_undo_step()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var yearly = store.Add(new Obligation { Name = "A", DueDate = Today.AddDays(3), RepeatUnit = RepeatUnit.Years });
            var monthly = store.Add(new Obligation { Name = "B", DueDate = Today.AddDays(-2), RepeatUnit = RepeatUnit.Months });
            var once = store.Add(new Obligation { Name = "C", DueDate = Today.AddDays(1) });
            var undated = store.Add(new Obligation { Name = "D", RepeatUnit = RepeatUnit.Years });

            var n = store.RenewMany([yearly.Id, monthly.Id, once.Id, undated.Id], Today);

            Assert.Equal(2, n);
            Assert.Equal(Today.AddDays(3).AddYears(1), store.Find(yearly.Id)!.DueDate);
            Assert.True(store.Find(monthly.Id)!.DueDate > Today);
            Assert.Equal(Today.AddDays(1), store.Find(once.Id)!.DueDate);
            Assert.Null(store.Find(undated.Id)!.DueDate);

            Assert.True(store.Undo());
            Assert.Equal(Today.AddDays(3), store.Find(yearly.Id)!.DueDate);
            Assert.Equal(Today.AddDays(-2), store.Find(monthly.Id)!.DueDate);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Putting_away_and_deleting_several_are_one_step_each()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            var a = store.Add(new Obligation { Name = "A", DueDate = Today });
            var b = store.Add(new Obligation { Name = "B", DueDate = Today });
            var c = store.Add(new Obligation { Name = "C", DueDate = Today });

            Assert.Equal(2, store.SetDoneMany([a.Id, b.Id], true));
            Assert.True(store.Find(a.Id)!.Done && store.Find(b.Id)!.Done && !store.Find(c.Id)!.Done);
            Assert.Equal(0, store.SetDoneMany([a.Id, b.Id], true));

            Assert.Equal(2, store.RemoveMany([a.Id, c.Id]));
            Assert.Single(store.Items);
            Assert.True(store.Undo());
            Assert.Equal(3, store.Items.Count);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void A_bad_renewal_address_is_refused_and_a_good_one_is_completed_and_round_trips_through_csv()
    {
        var dir = TempDir();
        try
        {
            var store = new ObligationStore(dir);
            Assert.Throws<StoreException>(() => store.Add(new Obligation { Name = "X", RenewalLink = "javascript:alert(1)" }));

            var o = store.Add(new Obligation { Name = "License", DueDate = Today, RenewalLink = "state.example.gov/renew", Contact = "Dana, 555-0100" });
            Assert.Equal("https://state.example.gov/renew", o.RenewalLink);

            var csv = Exporters.ToCsv(store.Items, Today);
            var back = Assert.Single(Exporters.FromCsv(csv).Items);
            Assert.Equal("https://state.example.gov/renew", back.RenewalLink);
            Assert.Equal("Dana, 555-0100", back.Contact);
        }
        finally { Cleanup(dir); }
    }
}
