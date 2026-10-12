// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Globalization;
using System.Text;

namespace Duejoro.Core;

/// <summary>What happened when a file of records was read.</summary>
public sealed record ImportResult(IReadOnlyList<Obligation> Items, IReadOnlyList<string> Warnings);

/// <summary>Moving records in and out: a spreadsheet (CSV) and a calendar file (iCalendar), so nothing is locked inside Duejoro.</summary>
public static class Exporters
{
    static readonly string[] Header =
        ["Name", "Category", "Owner", "Issuer", "Reference", "Due date", "Status", "Days left", "Remind days before", "Repeats", "Cost", "Done", "Notes", "Renewal link", "Contact"];

    static string Q(string s) => s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    static string StatusText(DueStatus s) => s switch
    {
        DueStatus.Overdue => "Overdue", DueStatus.DueSoon => "Due soon", DueStatus.Upcoming => "Upcoming", DueStatus.NoDate => "No date", _ => "Done",
    };

    // ---- spreadsheet --------------------------------------------------------------------------------------------------

    public static string ToCsv(IEnumerable<Obligation> items, DateOnly today)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(',', Header).Append("\r\n");
        foreach (var o in items)
        {
            var days = DueMath.DaysLeft(o, today);
            sb.AppendJoin(',', new[]
            {
                Q(o.Name), Q(o.Category), Q(o.Owner), Q(o.Issuer), Q(o.Reference),
                o.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                StatusText(DueMath.StatusOf(o, today)), days?.ToString(CultureInfo.InvariantCulture) ?? "",
                o.RemindDaysBefore.ToString(CultureInfo.InvariantCulture),
                Q(DueMath.DescribeRepeat(o.RepeatUnit, o.RepeatEvery)),
                o.Cost?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                o.Done ? "yes" : "no", Q(o.Notes), Q(o.RenewalLink), Q(o.Contact),
            }).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Splits CSV text into rows of cells (quoted cells may hold commas, quotes and line breaks).</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        text = text.TrimStart('\uFEFF');
        // a semicolon-separated file (common where the comma is the decimal mark) is read too
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : firstLine.Count(c => c == '\t') > firstLine.Count(c => c == ',') ? '\t' : ',';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == delimiter) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows.Where(r => r.Any(c => c.Trim().Length > 0)).ToList();
    }

    static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy", "MMM d yyyy",
        "dd-MMM-yyyy", "d-MMM-yy", "yyyyMMdd",
    ];

    public static bool TryParseDate(string text, out DateOnly date)
    {
        text = text.Trim();
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        // a date with a time on it, as spreadsheets like to write them
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) { date = DateOnly.FromDateTime(dt); return true; }
        date = default;
        return false;
    }

    static string? Pick(IReadOnlyDictionary<string, int> columns, List<string> row, params string[] names)
    {
        foreach (var n in names)
            if (columns.TryGetValue(n, out var i) && i < row.Count && row[i].Trim().Length > 0) return row[i].Trim();
        return null;
    }

    /// <summary>
    /// Reads records from CSV text. Column names are matched loosely (Name or Title; Due date, Expiry, Expires, Renewal date...). Rows without a
    /// name are skipped with a warning; a date that cannot be read is left empty with a warning.
    /// </summary>
    public static ImportResult FromCsv(string text)
    {
        var rows = ParseCsv(text);
        var warnings = new List<string>();
        var items = new List<Obligation>();
        if (rows.Count == 0) return new ImportResult(items, ["The file is empty."]);
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows[0].Count; i++) columns[rows[0][i].Trim()] = i;
        var nameHeaders = new[] { "Name", "Title", "Item", "License", "Description", "Obligation" };
        if (!nameHeaders.Any(columns.ContainsKey))
        {
            // no recognizable header: treat the first column as the name and the second as the date
            warnings.Add("No column named Name was found, so the first column was used as the name and the second as the due date.");
            columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Name"] = 0, ["Due date"] = 1 };
        }
        else rows.RemoveAt(0);

        var line = 1;
        foreach (var row in rows)
        {
            line++;
            var name = Pick(columns, row, "Name", "Title", "Item", "License", "Description", "Obligation");
            if (name is null) { warnings.Add($"Row {line}: skipped because it has no name."); continue; }
            var o = new Obligation
            {
                Name = name,
                Category = Pick(columns, row, "Category", "Type", "Kind") ?? "",
                Owner = Pick(columns, row, "Owner", "Responsible", "Assigned to", "Person") ?? "",
                Issuer = Pick(columns, row, "Issuer", "Issued by", "Vendor", "Agency", "Provider", "Authority") ?? "",
                Reference = Pick(columns, row, "Reference", "Number", "Policy number", "License number", "Account", "ID") ?? "",
                Notes = Pick(columns, row, "Notes", "Note", "Comments", "Remarks") ?? "",
                Contact = Pick(columns, row, "Contact", "Contact person", "Phone", "Email", "Telephone") ?? "",
            };
            var linkText = Pick(columns, row, "Renewal link", "Link", "URL", "Website", "Web address", "Renewal URL", "Portal");
            if (linkText is not null)
            {
                if (Obligation.NormalizeLink(linkText) is { Length: > 0 } link) o.RenewalLink = link;
                else warnings.Add($"Row {line}: \"{linkText}\" is not a web address, so \"{name}\" has no renewal link.");
            }
            var dateText = Pick(columns, row, "Due date", "Due", "Expiry", "Expiration", "Expires", "Expiry date", "Expiration date", "Renewal date", "Renews", "Date", "End date");
            if (dateText is not null)
            {
                if (TryParseDate(dateText, out var d)) o.DueDate = d;
                else warnings.Add($"Row {line}: the date \"{dateText}\" could not be read, so \"{name}\" has no due date. Use year-month-day, such as 2026-09-30.");
            }
            if (int.TryParse(Pick(columns, row, "Remind days before", "Remind", "Reminder", "Notice days"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rd)) o.RemindDaysBefore = rd;
            if (decimal.TryParse(Pick(columns, row, "Cost", "Fee", "Amount", "Price", "Premium")?.TrimStart('$', '€', '£'), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) && cost >= 0) o.Cost = cost;
            var done = Pick(columns, row, "Done", "Closed", "Archived");
            o.Done = done is not null && done.Trim().ToLowerInvariant() is "yes" or "true" or "1" or "y" or "done";
            ParseRepeat(Pick(columns, row, "Repeats", "Repeat", "Renews every", "Frequency"), o);
            items.Add(o);
        }
        return new ImportResult(items, warnings);
    }

    static void ParseRepeat(string? text, Obligation o)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var t = text.Trim().ToLowerInvariant();
        if (t.StartsWith("does not") || t is "no" or "none" or "never" or "once") return;
        var n = 1;
        var digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length > 0 && int.TryParse(digits, out var parsed) && parsed > 0) n = parsed;
        RepeatUnit unit = t switch
        {
            _ when t.Contains("annual") || t.Contains("year") => RepeatUnit.Years,
            _ when t.Contains("quarter") => RepeatUnit.Months,
            _ when t.Contains("month") => RepeatUnit.Months,
            _ when t.Contains("week") => RepeatUnit.Weeks,
            _ when t.Contains("day") || t.Contains("daily") => RepeatUnit.Days,
            _ => RepeatUnit.None,
        };
        if (t.Contains("quarter")) n = 3 * (digits.Length > 0 ? n : 1);
        if (unit == RepeatUnit.None) return;
        o.RepeatUnit = unit; o.RepeatEvery = n;
    }

    // ---- calendar -----------------------------------------------------------------------------------------------------

    static string IcsEscape(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

    /// <summary>Lines of an iCalendar file may not be longer than 75 bytes; longer ones continue on the next line after a space.</summary>
    static IEnumerable<string> Fold(string line)
    {
        var bytes = 0; var sb = new StringBuilder();
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > (sb.Length == 0 || bytes == 0 ? 75 : 74) && sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear(); sb.Append(' '); bytes = 1;
            }
            sb.Append(rune.ToString()); bytes += size;
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    /// <summary>A calendar file with one all-day event per record that has a date, with an alert at the start of its reminder period.</summary>
    public static string ToIcs(IEnumerable<Obligation> items, DateTime generatedAt)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Tusivo//Duejoro//EN", "CALSCALE:GREGORIAN", "X-WR-CALNAME:Duejoro",
        };
        var stamp = generatedAt.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        foreach (var o in items.Where(i => i.DueDate is not null && !i.Done))
        {
            var d = o.DueDate!.Value;
            var description = new List<string>();
            if (o.Category.Length > 0) description.Add("Category: " + o.Category);
            if (o.RenewalLink.Length > 0) description.Add("Renew at: " + o.RenewalLink);
            if (o.Contact.Length > 0) description.Add("Contact: " + o.Contact);
            if (o.Owner.Length > 0) description.Add("Owner: " + o.Owner);
            if (o.Issuer.Length > 0) description.Add("Issued by: " + o.Issuer);
            if (o.Reference.Length > 0) description.Add("Reference: " + o.Reference);
            if (o.Notes.Length > 0) description.Add(o.Notes);
            lines.AddRange(
            [
                "BEGIN:VEVENT",
                $"UID:{o.Id:N}@duejoro",
                $"DTSTAMP:{stamp}",
                $"DTSTART;VALUE=DATE:{d:yyyyMMdd}",
                $"DTEND;VALUE=DATE:{d.AddDays(1):yyyyMMdd}",
                "SUMMARY:" + IcsEscape("Due: " + o.Name),
                "DESCRIPTION:" + IcsEscape(string.Join("\n", description)),
                "TRANSP:TRANSPARENT",
            ]);
            if (o.RemindDaysBefore > 0)
                lines.AddRange(["BEGIN:VALARM", "ACTION:DISPLAY", "DESCRIPTION:" + IcsEscape(o.Name + " is due on " + d.ToString("yyyy-MM-dd")), $"TRIGGER:-P{o.RemindDaysBefore}D", "END:VALARM"]);
            lines.Add("END:VEVENT");
        }
        lines.Add("END:VCALENDAR");
        return string.Join("\r\n", lines.SelectMany(Fold)) + "\r\n";
    }
}
