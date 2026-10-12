// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Duejoro.Core;
using Microsoft.Win32;

namespace Duejoro.App;

/// <summary>One line of the list: a record with its status worked out for display.</summary>
public sealed class Row
{
    public Row(Obligation item, DateOnly today)
    {
        Item = item;
        var status = DueMath.StatusOf(item, today);
        Status = status.ToString();
        StatusOrder = (int)status;
        StatusText = status switch
        {
            DueStatus.Overdue => "Overdue", DueStatus.DueSoon => "Due soon", DueStatus.Upcoming => "Upcoming", DueStatus.NoDate => "No date", _ => "Done",
        };
        StatusBrush = BrushFor(status);
        Name = item.Name; Category = item.Category; Owner = item.Owner;
        DueText = item.DueDate?.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) ?? "";
        WhenText = status == DueStatus.Done ? "" : DueMath.Describe(DueMath.DaysLeft(item, today)) + (DueMath.IsSnoozed(item, today) ? " (snoozed)" : "");
        DueSort = item.DueDate?.DayNumber ?? int.MaxValue;
    }

    public Obligation Item { get; }
    public Guid Id => Item.Id;
    public string Name { get; }
    public string Category { get; }
    public string Owner { get; }
    public string DueText { get; }
    public string WhenText { get; }
    public string Status { get; }
    public int StatusOrder { get; }
    public string StatusText { get; }
    public Brush StatusBrush { get; }
    public int DueSort { get; }

    static readonly Brush Red = Frozen("#DC2626"), Amber = Frozen("#D97706"), Green = Frozen("#16A34A"), Grey = Frozen("#6B7280"), Slate = Frozen("#475569");
    static Brush Frozen(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    public static Brush BrushFor(DueStatus s) => s switch
    {
        DueStatus.Overdue => Red, DueStatus.DueSoon => Amber, DueStatus.Upcoming => Green, DueStatus.NoDate => Grey, _ => Slate,
    };
}

public partial class MainWindow : Window
{
    readonly ObligationStore store = new();
    readonly ObservableCollection<Row> rows = [];
    bool updatingUi;
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    static readonly string[] StatusChoices =
        ["All active items", "Needs attention (overdue and due soon)", "Overdue", "Due soon", "Upcoming", "No date", "Done", "Everything, including done"];
    const string AllCategories = "All categories";

    public MainWindow()
    {
        InitializeComponent();
        var savedWindow = UsableWindow(settings.Window);
        StartupBounds.Apply(this, adjustFrame: savedWindow is null);
        if (savedWindow is { } sw) PlaceWindow(sw);
        Title = AppInfo.Name;
        VersionStatus.Text = "Duejoro " + AppInfo.Version;
        List.ItemsSource = rows;
        updatingUi = true;
        StatusBox.ItemsSource = StatusChoices; StatusBox.SelectedIndex = 0;
        updatingUi = false;
        store.Changed += () => { Refresh(); };
        InitTheme();
        Loaded += (_, _) =>
        {
            Refresh();
            if (store.LoadNotice is { } notice) Dialogs.Info(this, "Duejoro", notice, error: true);
            AnnounceAtStart();
            InitTray();
        };
    }

    // ---- showing the records ------------------------------------------------------------------------------------------

    string SearchText => SearchBox.Text.Trim();

    bool Matches(Obligation o, DueStatus status)
    {
        var cat = CategoryBox.SelectedItem as string;
        if (cat is not null && cat != AllCategories && !string.Equals(o.Category, cat, StringComparison.OrdinalIgnoreCase)) return false;
        var q = SearchText;
        if (q.Length > 0)
        {
            var hay = string.Join('\n', o.Name, o.Category, o.Owner, o.Issuer, o.Reference, o.Notes);
            if (!q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(part => hay.Contains(part, StringComparison.CurrentCultureIgnoreCase))) return false;
        }
        return StatusBox.SelectedIndex switch
        {
            0 => status != DueStatus.Done,
            1 => status is DueStatus.Overdue or DueStatus.DueSoon && !DueMath.IsSnoozed(o, Today),
            2 => status == DueStatus.Overdue,
            3 => status == DueStatus.DueSoon,
            4 => status == DueStatus.Upcoming,
            5 => status == DueStatus.NoDate,
            6 => status == DueStatus.Done,
            _ => true,
        };
    }

    void Refresh()
    {
        var today = Today;
        var all = store.Items;
        var selectedIds = List.SelectedItems.OfType<Row>().Select(r => r.Id).ToHashSet();

        // counts for the cards (always over everything that is active, whatever the filter)
        var active = all.Where(o => !o.Done).ToList();
        CountOverdue.Text = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.Overdue).ToString();
        CountSoon.Text = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.DueSoon).ToString();
        CountUpcoming.Text = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.Upcoming).ToString();
        CountAll.Text = active.Count.ToString();
        var costSoon = active.Where(o => o.Cost is not null && o.DueDate is { } d && d.DayNumber - today.DayNumber is >= 0 and <= 90).Sum(o => o.Cost!.Value);
        var costOver = active.Where(o => o.Cost is not null && DueMath.StatusOf(o, today) == DueStatus.Overdue).Sum(o => o.Cost!.Value);
        CostSummary.Text = costSoon > 0 || costOver > 0
            ? $"Renewals in the next 90 days: about {costSoon.ToString("C0", CultureInfo.CurrentCulture)}" + (costOver > 0 ? $"   ·   overdue: {costOver.ToString("C0", CultureInfo.CurrentCulture)}" : "")
            : "";

        // the categories in use
        var categories = all.Select(o => o.Category).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase).ToList();
        var chosenCategory = CategoryBox.SelectedItem as string;
        updatingUi = true;
        CategoryBox.ItemsSource = new[] { AllCategories }.Concat(categories).ToList();
        CategoryBox.SelectedItem = chosenCategory is not null && categories.Contains(chosenCategory, StringComparer.OrdinalIgnoreCase) ? chosenCategory : AllCategories;
        updatingUi = false;

        rows.Clear();
        foreach (var o in all.Select(o => new Row(o, today))
                     .Where(r => Matches(r.Item, (DueStatus)r.StatusOrder))
                     .OrderBy(r => r.StatusOrder == (int)DueStatus.Done ? 1 : 0).ThenBy(r => r.DueSort).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
            rows.Add(o);

        var empty = all.Count == 0;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = DetailsPane.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Cards.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        FilterNote.Text = !empty && rows.Count != all.Count ? $"Showing {rows.Count} of {all.Count}" : "";
        CountStatus.Text = empty ? "" : $"{active.Count} active, {all.Count - active.Count} done";

        foreach (var r in rows.Where(r => selectedIds.Contains(r.Id))) List.SelectedItems.Add(r);
        ShowDetails();
        UndoItem.IsEnabled = store.CanUndo; RedoItem.IsEnabled = store.CanRedo;
    }

    void AnnounceAtStart()
    {
        var today = Today;
        var active = store.Items.Where(o => DueMath.NeedsAttention(o, today)).ToList();
        var over = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.Overdue);
        var soon = active.Count(o => DueMath.StatusOf(o, today) == DueStatus.DueSoon);
        if (over + soon == 0) { MessageStatus.Text = store.Items.Count == 0 ? "" : "Nothing needs attention right now."; return; }
        MessageStatus.Text = (over > 0 ? $"{over} overdue" : "") + (over > 0 && soon > 0 ? " and " : "") + (soon > 0 ? $"{soon} due soon" : "") + ". Click a card above to see them.";
    }

    // ---- details ------------------------------------------------------------------------------------------------------

    /// <summary>The chosen item, or null when none or several are chosen.</summary>
    Row? Selected => List.SelectedItems.Count == 1 ? List.SelectedItems[0] as Row : null;

    IEnumerable<Row> Chosen => List.SelectedItems.OfType<Row>();

    /// <summary>Says why nothing happened when the action needs exactly one item.</summary>
    bool NeedOne()
    {
        if (Selected is not null) return true;
        MessageStatus.Text = List.SelectedItems.Count > 1 ? "Choose just one item for that." : "Choose an item in the list first.";
        return false;
    }

    void ShowDetails()
    {
        if (Selected is not { } row)
        {
            var n = List.SelectedItems.Count;
            NoSelection.Text = n > 1
                ? $"{n} items chosen.\n\nMark as Renewed renews each by its own repeat, Put Away puts them all away, and Delete removes them. Each is one step you can undo."
                : "Choose an item to see its details.";
            NoSelection.Visibility = Visibility.Visible; DetailsScroll.Visibility = Visibility.Collapsed;
            return;
        }
        NoSelection.Visibility = Visibility.Collapsed; DetailsScroll.Visibility = Visibility.Visible;
        var o = row.Item; var today = Today;
        DName.Text = o.Name;
        DChip.Background = row.StatusBrush; DChipText.Text = row.StatusText;
        var days = DueMath.DaysLeft(o, today);
        DWhen.Text = o.Done ? "Put away" : o.DueDate is { } d
            ? $"{d.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture)} ({DueMath.Describe(days)})" : "No date";
        RenewButton.IsEnabled = !o.Done;
        SnoozeButton.IsEnabled = !o.Done;
        LinkButton.Visibility = o.RenewalLink.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        Facts.Children.Clear(); Facts.RowDefinitions.Clear();
        void Fact(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var r = Facts.RowDefinitions.Count;
            Facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Opacity = 0.7, Margin = new Thickness(0, 0, 8, 5) };
            var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(l, r); Grid.SetRow(v, r); Grid.SetColumn(v, 1);
            Facts.Children.Add(l); Facts.Children.Add(v);
        }
        Fact("Category", o.Category);
        Fact("Responsible", o.Owner);
        Fact("Issued by", o.Issuer);
        Fact("Number", o.Reference);
        Fact("Flagged", o.DueDate is null ? "" : $"{o.RemindDaysBefore} days before");
        Fact("Comes round", o.RepeatUnit == RepeatUnit.None ? "" : DueMath.DescribeRepeat(o.RepeatUnit, o.RepeatEvery));
        Fact("Cost", o.Cost?.ToString("C2", CultureInfo.CurrentCulture) ?? "");
        Fact("Contact", o.Contact);
        Fact("Snoozed until", DueMath.IsSnoozed(o, today) ? o.SnoozedUntil!.Value.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) : "");
        Fact("Notes", o.Notes);

        Files.Items.Clear();
        foreach (var a in o.Attachments)
            Files.Items.Add(new ListBoxItem { Content = $"{a.FileName}   ({Size(a.Size)})", Tag = a, ToolTip = store.FullPath(a) });
        History.ItemsSource = o.History.OrderByDescending(h => h.When).Select(h => $"{h.When.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}: {h.What}").ToList();
    }

    static string Size(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.#} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : $"{bytes} bytes";

    void OnListSelected(object sender, SelectionChangedEventArgs e) => ShowDetails();

    void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindParent<DataGridRow>(d) is not null) OnEdit(sender, e);
    }

    static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d is not null) { if (d is T t) return t; d = VisualTreeHelper.GetParent(d); }
        return null;
    }

    void OnListKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnEdit(sender, e); e.Handled = true; }
        else if (e.Key == Key.Delete) { OnDelete(sender, e); e.Handled = true; }
    }

    // ---- filters ------------------------------------------------------------------------------------------------------

    void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (updatingUi || !IsLoaded) return;
        ShowDoneItem.IsChecked = StatusBox.SelectedIndex == 7;
        Refresh();
    }

    void OnFocusSearch(object sender, RoutedEventArgs e) { SearchBox.Focus(); SearchBox.SelectAll(); }

    void OnClearFilters(object sender, RoutedEventArgs e)
    {
        updatingUi = true;
        SearchBox.Text = ""; StatusBox.SelectedIndex = 0; CategoryBox.SelectedIndex = 0;
        updatingUi = false;
        ShowDoneItem.IsChecked = false;
        Refresh();
    }

    void OnShowDone(object sender, RoutedEventArgs e)
    {
        updatingUi = true;
        StatusBox.SelectedIndex = ShowDoneItem.IsChecked ? 7 : 0;
        updatingUi = false;
        Refresh();
    }

    void ShowOnly(int statusIndex)
    {
        updatingUi = true; StatusBox.SelectedIndex = statusIndex; updatingUi = false;
        ShowDoneItem.IsChecked = false;
        Refresh();
    }

    void OnCardOverdue(object sender, MouseButtonEventArgs e) => ShowOnly(2);
    void OnCardSoon(object sender, MouseButtonEventArgs e) => ShowOnly(3);
    void OnCardUpcoming(object sender, MouseButtonEventArgs e) => ShowOnly(4);
    void OnCardAll(object sender, MouseButtonEventArgs e) => ShowOnly(0);

    // ---- actions ------------------------------------------------------------------------------------------------------

    void Fail(StoreException ex) => Dialogs.Info(this, "Duejoro", ex.Message, error: true);

    void OnNew(object sender, RoutedEventArgs e)
    {
        var fresh = new Obligation { RemindDaysBefore = 30 };
        var cats = store.Items.Select(i => i.Category);
        var edited = EditWindow.Show(this, fresh, cats, isNew: true);
        if (edited is null) return;
        try
        {
            var added = store.Add(edited);
            Select(added.Id);
            MessageStatus.Text = $"Added \"{added.Name}\".";
        }
        catch (StoreException ex) { Fail(ex); }
    }

    void Select(Guid id)
    {
        if (rows.FirstOrDefault(r => r.Id == id) is { } r) { List.SelectedItem = r; List.ScrollIntoView(r); }
    }

    void OnEdit(object sender, RoutedEventArgs e)
    {
        if (!NeedOne() || Selected is not { } row) return;
        var edited = EditWindow.Show(this, row.Item.Clone(), store.Items.Select(i => i.Category), isNew: false);
        if (edited is null) return;
        try { store.Update(edited); Select(edited.Id); MessageStatus.Text = "Saved."; }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnRenew(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItems.Count > 1) { RenewChosen(); return; }
        if (!NeedOne() || Selected is not { } row) return;
        if (row.Item.Done) { MessageStatus.Text = "That item is put away. Bring it back first (Edit, Put Away as Done)."; return; }
        var answer = EditWindow.ShowRenew(this, row.Item, Today);
        if (answer is null) return;
        try
        {
            var next = store.Renew(row.Id, answer.Value.Date, Today, answer.Value.Note);
            Select(row.Id);
            MessageStatus.Text = next is { } d ? $"Renewed. Next due {d.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)}." : "Put away as done.";
        }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnToggleDone(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItems.Count > 1)
        {
            var ids = Chosen.Select(r => r.Id).ToList();
            var bringBack = Chosen.All(r => r.Item.Done);
            try
            {
                var n = store.SetDoneMany(ids, !bringBack);
                MessageStatus.Text = bringBack ? $"{n} item(s) are back on the list." : $"{n} item(s) put away as done. They show under Show, Done.";
            }
            catch (StoreException ex) { Fail(ex); }
            return;
        }
        if (!NeedOne() || Selected is not { } row) return;
        try
        {
            store.SetDone(row.Id, !row.Item.Done);
            MessageStatus.Text = row.Item.Done ? $"\"{row.Name}\" is back on the list." : $"\"{row.Name}\" is put away as done. It shows under Show, Done.";
        }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnDelete(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItems.Count > 1)
        {
            var count = List.SelectedItems.Count;
            if (!Dialogs.Confirm(this, "Delete Items", $"Delete these {count} items and their history?\n\nYou can bring them back right away with Undo (Ctrl+Z). Attached files stay in the Documents folder.")) return;
            try { MessageStatus.Text = $"Deleted {store.RemoveMany(Chosen.Select(r => r.Id).ToList())} item(s). Press Ctrl+Z to bring them back."; }
            catch (StoreException ex) { Fail(ex); }
            return;
        }
        if (!NeedOne() || Selected is not { } row) return;
        if (!Dialogs.Confirm(this, "Delete Item", $"Delete \"{row.Name}\" and its history?\n\nYou can bring it back right away with Undo (Ctrl+Z). Attached files stay in the Documents folder.")) return;
        try { store.Remove(row.Id); MessageStatus.Text = $"Deleted \"{row.Name}\". Press Ctrl+Z to bring it back."; }
        catch (StoreException ex) { Fail(ex); }
    }

    void RenewChosen()
    {
        var chosen = Chosen.ToList();
        var can = chosen.Count(r => !r.Item.Done && DueMath.NextDue(r.Item, Today) is not null);
        if (can == 0)
        {
            Dialogs.Info(this, "Mark as Renewed", "None of the chosen items has both a date and a repeat, so there is nothing to move on. Renew them one at a time to give each a new date.");
            return;
        }
        var skipped = chosen.Count - can;
        var message = $"Renew {can} item(s), each moved on by its own repeat period?" +
            (skipped > 0 ? $"\n\n{skipped} of the chosen items do not repeat, have no date or are put away, and will be left as they are." : "") +
            "\n\nYou can undo this with Ctrl+Z.";
        if (!Dialogs.Confirm(this, "Mark as Renewed", message)) return;
        try { MessageStatus.Text = $"Renewed {store.RenewMany(chosen.Select(r => r.Id).ToList(), Today)} item(s)."; }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnSnooze(object sender, RoutedEventArgs e)
    {
        if (!NeedOne() || Selected is not { } row) return;
        if (row.Item.Done) { MessageStatus.Text = "That item is put away, so there is nothing to snooze."; return; }
        var answer = EditWindow.ShowSnooze(this, row.Item, Today);
        if (answer.Cancelled) return;
        try
        {
            store.Snooze(row.Id, answer.Until, Today);
            Select(row.Id);
            MessageStatus.Text = answer.Until is { } d ? $"Snoozed until {d.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)}." : "Snooze cleared.";
        }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnOpenLink(object sender, RoutedEventArgs e)
    {
        if (!NeedOne() || Selected is not { } row) return;
        if (Obligation.NormalizeLink(row.Item.RenewalLink) is not { Length: > 0 } url) { MessageStatus.Text = "That item has no renewal address. Add one with Edit."; return; }
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Dialogs.Info(this, "Duejoro", "Windows could not open that address: " + ex.Message, error: true); }
    }

    void OnUndo(object sender, RoutedEventArgs e)
    {
        try { MessageStatus.Text = store.Undo() ? "Undone." : "There is nothing to undo."; }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnRedo(object sender, RoutedEventArgs e)
    {
        try { MessageStatus.Text = store.Redo() ? "Redone." : "There is nothing to redo."; }
        catch (StoreException ex) { Fail(ex); }
    }

    // ---- files --------------------------------------------------------------------------------------------------------

    void OnAttach(object sender, RoutedEventArgs e)
    {
        if (!NeedOne() || Selected is not { } row) return;
        var d = new OpenFileDialog { Multiselect = true, Title = $"Attach files to \"{row.Name}\"" };
        if (d.ShowDialog(this) != true) return;
        try
        {
            foreach (var path in d.FileNames) store.Attach(row.Id, path);
            Select(row.Id);
            MessageStatus.Text = $"Attached {d.FileNames.Length} file(s). Copies are kept in the Documents folder, so the originals can be moved or deleted.";
        }
        catch (StoreException ex) { Fail(ex); }
    }

    Attachment? SelectedFile => (Files.SelectedItem as ListBoxItem)?.Tag as Attachment;

    void OnFileOpen(object sender, RoutedEventArgs e)
    {
        if (SelectedFile is not { } a) { MessageStatus.Text = "Choose a file in the list first."; return; }
        var path = store.FullPath(a);
        if (!File.Exists(path)) { Dialogs.Info(this, "Duejoro", "That file is no longer where Duejoro put it:\n" + path, error: true); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Dialogs.Info(this, "Duejoro", "Windows could not open that file: " + ex.Message, error: true); }
    }

    void OnFileDetach(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || SelectedFile is not { } a) { MessageStatus.Text = "Choose a file in the list first."; return; }
        try { store.Detach(row.Id, a.Id); Select(row.Id); }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnOpenDataFolder(object sender, RoutedEventArgs e) => OpenFolder(store.DataFolder);

    static void OpenFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    // ---- moving data in and out --------------------------------------------------------------------------------------

    void OnImport(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "Spreadsheet files (*.csv)|*.csv|All files|*.*", Title = "Choose the spreadsheet to import (save it as CSV from Excel first)" };
        if (d.ShowDialog(this) != true) return;
        ImportResult result;
        try { result = Exporters.FromCsv(File.ReadAllText(d.FileName)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dialogs.Info(this, "Import", "The file could not be read: " + ex.Message, error: true); return; }
        if (result.Items.Count == 0)
        {
            Dialogs.Info(this, "Import", "Nothing could be read from that file." + (result.Warnings.Count > 0 ? "\n\n" + string.Join("\n", result.Warnings.Take(6)) : ""), error: true);
            return;
        }
        var message = $"Found {result.Items.Count} item(s) in {Path.GetFileName(d.FileName)}. Add them?";
        if (result.Warnings.Count > 0)
            message += $"\n\n{result.Warnings.Count} thing(s) to know:\n" + string.Join("\n", result.Warnings.Take(8)) + (result.Warnings.Count > 8 ? "\n..." : "");
        if (!Dialogs.Confirm(this, "Import", message + "\n\nYou can undo the whole import with Ctrl+Z.")) return;
        try { MessageStatus.Text = $"Imported {store.AddMany(result.Items)} item(s)."; }
        catch (StoreException ex) { Fail(ex); }
    }

    void OnExportCsv(object sender, RoutedEventArgs e)
    {
        if (store.Items.Count == 0) { Dialogs.Info(this, "Export", "There is nothing to export yet."); return; }
        var d = new SaveFileDialog { Filter = "Spreadsheet (*.csv)|*.csv", FileName = $"Duejoro {Today:yyyy-MM-dd}.csv", Title = "Save the list as" };
        if (d.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(d.FileName, Exporters.ToCsv(store.Items.OrderBy(i => i.DueDate ?? DateOnly.MaxValue), Today), new UTF8Encoding(true));
            MessageStatus.Text = $"Saved {store.Items.Count} item(s) to {Path.GetFileName(d.FileName)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dialogs.Info(this, "Export", ex.Message, error: true); }
    }

    void OnExportCalendar(object sender, RoutedEventArgs e)
    {
        var count = store.Items.Count(i => !i.Done && i.DueDate is not null);
        if (count == 0) { Dialogs.Info(this, "Export", "There are no dated items to put in a calendar."); return; }
        var d = new SaveFileDialog { Filter = "Calendar file (*.ics)|*.ics", FileName = "Duejoro.ics", Title = "Save the calendar file as" };
        if (d.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(d.FileName, Exporters.ToIcs(store.Items, DateTime.Now), new UTF8Encoding(false));
            Dialogs.Info(this, "Calendar file saved",
                $"{count} item(s) were saved to {Path.GetFileName(d.FileName)}.\n\nOpen the file to add them to Outlook or Apple Calendar, or use Import in Google Calendar's settings. Each one is an all-day event with an alert at the start of its reminder period.\n\nThe file is a snapshot: save it again after you change things.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dialogs.Info(this, "Export", ex.Message, error: true); }
    }

    // ---- window -------------------------------------------------------------------------------------------------------

    void OnExit(object sender, RoutedEventArgs e) { exiting = true; Close(); }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        RememberWindow();
        if (!exiting && settings.TrayReminders) { e.Cancel = true; HideToTray(); return; }
        DisposeTray();
    }

    static WindowPlacement? UsableWindow(WindowPlacement? saved)
    {
        if (saved is null) return null;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var overlap = Rect.Intersect(screen, new Rect(saved.Left, saved.Top, saved.Width, saved.Height));
        return !overlap.IsEmpty && overlap.Width >= 200 && overlap.Height >= 100 ? saved : null;
    }

    void PlaceWindow(WindowPlacement p)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = p.Left; Top = p.Top; Width = Math.Max(MinWidth, p.Width); Height = Math.Max(MinHeight, p.Height);
        if (p.Maximized) WindowState = WindowState.Maximized;
    }

    void RememberWindow()
    {
        try
        {
            var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (b.Width < 200 || b.Height < 150 || double.IsNaN(b.Left) || double.IsNaN(b.Top)) return;
            settings.SetWindow(new WindowPlacement(b.Left, b.Top, b.Width, b.Height, WindowState == WindowState.Maximized));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { }
    }
}
