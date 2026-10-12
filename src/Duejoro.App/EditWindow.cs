// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Duejoro.Core;

namespace Duejoro.App;

/// <summary>The window for adding or changing a record, built in code so that it follows the program's theme.</summary>
static class EditWindow
{
    public static readonly string[] CommonCategories =
        ["License", "Permit", "Insurance", "Certification", "Contract", "Lease", "Subscription", "Warranty", "Registration", "Tax filing", "Inspection", "Training", "Other"];

    /// <summary>Shows the editor for <paramref name="source"/> (a copy that may be changed freely). Returns the edited copy, or null if cancelled.</summary>
    public static Obligation? Show(Window owner, Obligation source, IEnumerable<string> existingCategories, bool isNew)
    {
        var w = new Window
        {
            Title = isNew ? "New Item" : "Edit Item", Width = 700, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
        };
        Dialogs.Style(w);
        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        TextBox Field(string label, string value, double width = double.NaN)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
            var box = new TextBox { Text = value };
            if (!double.IsNaN(width)) { box.Width = width; box.HorizontalAlignment = HorizontalAlignment.Left; }
            panel.Children.Add(box);
            return box;
        }

        var name = Field("What is it? (for example: Business license, Fire insurance policy, Forklift certification)", source.Name);

        panel.Children.Add(new TextBlock { Text = "Category", Margin = new Thickness(0, 8, 0, 3) });
        var category = new ComboBox { IsEditable = true, Text = source.Category };
        foreach (var c in CommonCategories.Concat(existingCategories).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase))
            category.Items.Add(c);
        category.Text = source.Category;
        panel.Children.Add(category);

        var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        panel.Children.Add(row);
        TextBox Half(string label, string value, int col, Grid? into = null)
        {
            var host = new StackPanel();
            host.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
            var box = new TextBox { Text = value };
            host.Children.Add(box);
            Grid.SetColumn(host, col); (into ?? row).Children.Add(host);
            return box;
        }
        var owner1 = Half("Who is responsible", source.Owner, 0);
        var issuer = Half("Issued by or with (agency, insurer, vendor)", source.Issuer, 2);
        var reference = Field("License, policy or account number", source.Reference);

        var row2 = new Grid();
        row2.ColumnDefinitions.Add(new ColumnDefinition()); row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); row2.ColumnDefinitions.Add(new ColumnDefinition());
        panel.Children.Add(row2);
        var link = Half("Where it is renewed (web address, optional)", source.RenewalLink, 0, row2);
        var contact = Half("Who to contact (name, phone or email, optional)", source.Contact, 2, row2);

        // the date
        panel.Children.Add(new TextBlock { Text = "Due, expires or renews on", Margin = new Thickness(0, 12, 0, 3) });
        var dateRow = new StackPanel { Orientation = Orientation.Horizontal };
        var date = new DatePicker { SelectedDate = source.DueDate?.ToDateTime(TimeOnly.MinValue), Width = 190 };
        var noDate = new CheckBox { Content = "No date (for example a contract with no end)", IsChecked = source.DueDate is null && !isNew && false, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        noDate.IsChecked = !isNew && source.DueDate is null;
        noDate.Click += (_, _) => { date.IsEnabled = noDate.IsChecked != true; if (noDate.IsChecked == true) date.SelectedDate = null; };
        date.IsEnabled = noDate.IsChecked != true;
        dateRow.Children.Add(date); dateRow.Children.Add(noDate);
        panel.Children.Add(dateRow);

        // reminders and repeats
        var line2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        line2.Children.Add(new TextBlock { Text = "Flag it", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var remind = new TextBox { Text = source.RemindDaysBefore.ToString(CultureInfo.InvariantCulture), Width = 56, TextAlignment = TextAlignment.Center };
        line2.Children.Add(remind);
        line2.Children.Add(new TextBlock { Text = "days before the date", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 22, 0) });
        line2.Children.Add(new TextBlock { Text = "Comes round", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var repeat = new ComboBox { Width = 160 };
        repeat.Items.Add("only once"); repeat.Items.Add("every ... days"); repeat.Items.Add("every ... weeks"); repeat.Items.Add("every ... months"); repeat.Items.Add("every ... years");
        repeat.SelectedIndex = (int)source.RepeatUnit;
        line2.Children.Add(repeat);
        var every = new TextBox { Text = source.RepeatEvery.ToString(CultureInfo.InvariantCulture), Width = 48, TextAlignment = TextAlignment.Center, Margin = new Thickness(8, 0, 0, 0), IsEnabled = source.RepeatUnit != RepeatUnit.None };
        line2.Children.Add(every);
        repeat.SelectionChanged += (_, _) => every.IsEnabled = repeat.SelectedIndex > 0;
        panel.Children.Add(line2);

        var cost = Field("What it costs each time (optional)", source.Cost?.ToString("0.##", CultureInfo.CurrentCulture) ?? "", 160);
        var notes = Field("Notes", source.Notes);
        notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.MinHeight = 76; notes.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        Obligation? result = null;
        var buttons = Dialogs.Buttons(w, "Save", true, () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { Dialogs.Info(w, "Duejoro", "Give it a name."); name.Focus(); return false; }
            if (noDate.IsChecked != true && date.SelectedDate is null) { Dialogs.Info(w, "Duejoro", "Choose the date, or tick \"No date\"."); return false; }
            if (!int.TryParse(remind.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rd) || rd < 0 || rd > DueMath.MaxRemindDays)
            { Dialogs.Info(w, "Duejoro", $"Type how many days before to flag it, from 0 to {DueMath.MaxRemindDays}."); remind.Focus(); return false; }
            var ev = 1;
            if (repeat.SelectedIndex > 0 && (!int.TryParse(every.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ev) || ev < 1 || ev > 999))
            { Dialogs.Info(w, "Duejoro", "Type how often it comes round, such as 1 or 2."); every.Focus(); return false; }
            if (Obligation.NormalizeLink(link.Text) is not { } normalizedLink)
            { Dialogs.Info(w, "Duejoro", "The renewal address must be a web address, such as https://example.com/renew. Leave it empty if there is none."); link.Focus(); return false; }
            decimal? c = null;
            if (cost.Text.Trim().Length > 0)
            {
                if (!decimal.TryParse(cost.Text.Trim().TrimStart('$', '€', '£'), NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) &&
                    !decimal.TryParse(cost.Text.Trim().TrimStart('$', '€', '£'), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) || parsed < 0)
                { Dialogs.Info(w, "Duejoro", "Type the cost as a number, such as 125 or 89.50."); cost.Focus(); return false; }
                c = parsed;
            }
            var o = source.Clone();
            o.Name = name.Text.Trim(); o.Category = category.Text.Trim(); o.Owner = owner1.Text.Trim(); o.Issuer = issuer.Text.Trim(); o.Reference = reference.Text.Trim();
            o.DueDate = noDate.IsChecked == true ? null : DateOnly.FromDateTime(date.SelectedDate!.Value);
            o.RemindDaysBefore = rd;
            o.RepeatUnit = (RepeatUnit)repeat.SelectedIndex; o.RepeatEvery = repeat.SelectedIndex > 0 ? ev : 1;
            o.Cost = c; o.Notes = notes.Text.Trim();
            o.RenewalLink = normalizedLink; o.Contact = contact.Text.Trim();
            result = o;
            return true;
        });
        panel.Children.Add(buttons);
        w.Content = panel;
        w.Loaded += (_, _) => name.Focus();
        return w.ShowDialog() == true ? result : null;
    }

    /// <summary>Asks how long to stop flagging an item. Returns null if cancelled; otherwise the day to stay quiet until, or null inside to clear a snooze.</summary>
    public static (DateOnly? Until, bool Cancelled) ShowSnooze(Window owner, Obligation o, DateOnly today)
    {
        var w = Dialogs.Make(owner, "Snooze", 500);
        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Stop flagging \"{o.Name}\" until:", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        });
        var choices = new (string Label, DateOnly Day)[]
        {
            ("Tomorrow", today.AddDays(1)), ("In 3 days", today.AddDays(3)), ("In a week", today.AddDays(7)),
            ("In 2 weeks", today.AddDays(14)), ("In a month", today.AddMonths(1)),
        };
        var pick = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var c in choices) pick.Items.Add($"{c.Label}  ({c.Day.ToString("MMM d", CultureInfo.CurrentCulture)})");
        pick.Items.Add("A day I choose...");
        pick.SelectedIndex = 2;
        panel.Children.Add(pick);
        var date = new DatePicker { SelectedDate = today.AddDays(7).ToDateTime(TimeOnly.MinValue), Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), IsEnabled = false };
        pick.SelectionChanged += (_, _) => date.IsEnabled = pick.SelectedIndex == choices.Length;
        panel.Children.Add(date);
        panel.Children.Add(new TextBlock
        {
            Text = "The due date does not change. The item stays in the list and keeps its status; it is only left out of the \"needs attention\" count, the start-up message and the tray reminders.",
            Opacity = 0.75, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
        });
        if (o.SnoozedUntil is { } current && current >= today)
            panel.Children.Add(new TextBlock { Text = $"It is snoozed until {current.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)}.", Margin = new Thickness(0, 8, 0, 0) });

        (DateOnly? Until, bool Cancelled) result = (null, true);
        var buttons = Dialogs.Buttons(w, "Snooze", true, () =>
        {
            DateOnly day;
            if (pick.SelectedIndex < choices.Length) day = choices[pick.SelectedIndex].Day;
            else if (date.SelectedDate is { } picked) day = DateOnly.FromDateTime(picked);
            else { Dialogs.Info(w, "Duejoro", "Choose the day."); return false; }
            if (day <= today || day > today.AddDays(366)) { Dialogs.Info(w, "Duejoro", "Choose a day after today and within a year."); return false; }
            result = (day, false);
            return true;
        });
        if (o.SnoozedUntil is { } cur && cur >= today)
        {
            var clear = new Button { Content = "Stop Snoozing", Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(0, 0, 8, 0) };
            clear.Click += (_, _) => { result = (null, false); w.DialogResult = true; };
            buttons.Children.Insert(0, clear);
        }
        panel.Children.Add(buttons);
        w.Content = panel;
        return w.ShowDialog() == true ? result : (null, true);
    }

    /// <summary>Asks for the new date when something is renewed. Returns (date, note, putAwayInstead) or null if cancelled.</summary>
    public static (DateOnly? Date, string Note, bool PutAway)? ShowRenew(Window owner, Obligation o, DateOnly today)
    {
        var suggested = DueMath.NextDue(o, today);
        var w = Dialogs.Make(owner, "Mark as Renewed", 520);
        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        panel.Children.Add(new TextBlock
        {
            Text = $"\"{o.Name}\" was due {(o.DueDate is { } d ? d.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) : "with no date")}. When is it due next?",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        });
        var date = new DatePicker { SelectedDate = (suggested ?? today.AddYears(1)).ToDateTime(TimeOnly.MinValue), Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(date);
        if (o.RepeatUnit != RepeatUnit.None)
            panel.Children.Add(new TextBlock { Text = $"Suggested from the repeat setting ({DueMath.DescribeRepeat(o.RepeatUnit, o.RepeatEvery)}); change it if the new date is different.", Opacity = 0.75, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        var putAway = new CheckBox { Content = "It does not come round again: put it away as done", Margin = new Thickness(0, 12, 0, 0), IsChecked = o.RepeatUnit == RepeatUnit.None && false };
        putAway.Click += (_, _) => date.IsEnabled = putAway.IsChecked != true;
        panel.Children.Add(putAway);
        panel.Children.Add(new TextBlock { Text = "A note for the history (optional, for example the receipt or confirmation number)", Margin = new Thickness(0, 12, 0, 3) });
        var note = new TextBox();
        panel.Children.Add(note);

        (DateOnly? Date, string Note, bool PutAway)? result = null;
        panel.Children.Add(Dialogs.Buttons(w, "Renew", true, () =>
        {
            if (putAway.IsChecked == true) { result = (null, note.Text.Trim(), true); return true; }
            if (date.SelectedDate is null) { Dialogs.Info(w, "Duejoro", "Choose the new date."); return false; }
            result = (DateOnly.FromDateTime(date.SelectedDate.Value), note.Text.Trim(), false);
            return true;
        }));
        w.Content = panel;
        return w.ShowDialog() == true ? result : null;
    }
}
