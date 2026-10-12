// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duejoro.Core;

/// <summary>Something went wrong with the records file, in words a person can act on.</summary>
public sealed class StoreException(string message) : Exception(message);

/// <summary>
/// All of the records, kept in one file (<c>duejoro.json</c>) in the data folder, written safely (a new file is made, then swapped in) with a
/// dated backup before each session's first change. Changes can be undone and redone while the program is open. Nothing is ever deleted from
/// disk by Duejoro: a record that is removed leaves its attached files where they are.
/// </summary>
public sealed class ObligationStore
{
    const int Format = 1;
    const int MaxUndo = 50;
    const int MaxBackups = 20;

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    sealed record FileShape(int Version, List<Obligation> Items);

    readonly string folder;
    readonly string file;
    readonly Func<DateTime> now;
    List<Obligation> items = [];
    readonly List<string> undo = [];
    readonly List<string> redo = [];
    bool backedUpThisSession;

    public ObligationStore(string? folder = null, Func<DateTime>? clock = null)
    {
        this.folder = folder ?? AppPaths.DataFolder;
        now = clock ?? (() => DateTime.Now);
        file = Path.Combine(this.folder, "duejoro.json");
        Load();
    }

    public event Action? Changed;

    public IReadOnlyList<Obligation> Items => items;
    public string DataFolder => folder;
    public string FilePath => file;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    /// <summary>Set when the main file could not be read and an earlier copy was used instead (the message to show the person).</summary>
    public string? LoadNotice { get; private set; }

    public Obligation? Find(Guid id) => items.FirstOrDefault(i => i.Id == id);

    // ---- loading and saving -------------------------------------------------------------------------------------------

    void Load()
    {
        Directory.CreateDirectory(folder);
        if (!File.Exists(file)) return;
        try { items = Parse(File.ReadAllText(file)); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            // set the damaged file aside (it is kept, never deleted) and fall back to the newest backup
            var damaged = Path.Combine(folder, $"duejoro.damaged-{now():yyyyMMdd-HHmmss}.json");
            try { File.Move(file, damaged); } catch (IOException) { /* the original stays */ }
            var backup = Directory.Exists(BackupFolder)
                ? Directory.EnumerateFiles(BackupFolder, "duejoro-*.json").OrderByDescending(f => f, StringComparer.Ordinal).FirstOrDefault()
                : null;
            if (backup is not null)
            {
                try
                {
                    items = Parse(File.ReadAllText(backup));
                    LoadNotice = $"The records file could not be read, so the newest backup was opened ({Path.GetFileName(backup)}). The damaged file was kept as {Path.GetFileName(damaged)}.";
                    return;
                }
                catch (Exception ex2) when (ex2 is JsonException or IOException or InvalidOperationException) { }
            }
            items = [];
            LoadNotice = $"The records file could not be read and there is no usable backup. The damaged file was kept as {Path.GetFileName(damaged)} in the data folder.";
        }
    }

    string BackupFolder => Path.Combine(folder, "Backups");

    static List<Obligation> Parse(string json)
    {
        var shape = JsonSerializer.Deserialize<FileShape>(json, Json) ?? throw new JsonException("empty");
        return shape.Items ?? [];
    }

    string Serialize() => JsonSerializer.Serialize(new FileShape(Format, items), Json);

    void Save()
    {
        try
        {
            Directory.CreateDirectory(folder);
            if (!backedUpThisSession && File.Exists(file))
            {
                Directory.CreateDirectory(BackupFolder);
                File.Copy(file, Path.Combine(BackupFolder, $"duejoro-{now():yyyyMMdd-HHmmss}.json"), overwrite: true);
                foreach (var old in Directory.EnumerateFiles(BackupFolder, "duejoro-*.json").OrderByDescending(f => f, StringComparer.Ordinal).Skip(MaxBackups))
                    File.Delete(old);
                backedUpThisSession = true;
            }
            var temp = file + ".tmp";
            File.WriteAllText(temp, Serialize());
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StoreException("The records could not be saved: " + ex.Message + " Your changes are still on screen; free some space or check that the folder is writable, then make a change again.");
        }
    }

    /// <summary>Remember how things were, run the change, save, tell the window.</summary>
    void Change(Action change)
    {
        var before = Serialize();
        change();
        undo.Add(before);
        if (undo.Count > MaxUndo) undo.RemoveAt(0);
        redo.Clear();
        Save();
        Changed?.Invoke();
    }

    // ---- changing records ---------------------------------------------------------------------------------------------

    public Obligation Add(Obligation o)
    {
        Validate(o);
        Change(() =>
        {
            o.Created = o.Modified = now();
            o.History.Add(new HistoryEntry { When = now(), What = "Added" });
            items.Add(o);
        });
        return o;
    }

    /// <summary>Replaces the record that has the same id with <paramref name="edited"/>.</summary>
    public void Update(Obligation edited)
    {
        Validate(edited);
        var index = items.FindIndex(i => i.Id == edited.Id);
        if (index < 0) throw new StoreException("That record no longer exists.");
        Change(() =>
        {
            edited.Modified = now();
            edited.History.Add(new HistoryEntry { When = now(), What = "Changed" });
            items[index] = edited;
        });
    }

    public void Remove(Guid id)
    {
        if (Find(id) is null) return;
        Change(() => items.RemoveAll(i => i.Id == id));
    }

    /// <summary>
    /// Marks a record as renewed: the date moves on (to <paramref name="newDue"/>, or by its repeat period) and the event is written to its history.
    /// A record that does not repeat and is given no new date is put away as done instead.
    /// </summary>
    public DateOnly? Renew(Guid id, DateOnly? newDue, DateOnly today, string note = "")
    {
        var o = Find(id) ?? throw new StoreException("That record no longer exists.");
        var next = newDue ?? DueMath.NextDue(o, today);
        var was = o.DueDate;
        Change(() =>
        {
            if (next is { } d) o.DueDate = d; else o.Done = true;
            o.Modified = now();
            var text = next is { } nd
                ? $"Renewed{(was is { } w ? $" (was due {w:yyyy-MM-dd})" : "")}; next due {nd:yyyy-MM-dd}"
                : "Marked as done";
            o.History.Add(new HistoryEntry { When = now(), What = string.IsNullOrWhiteSpace(note) ? text : text + ". " + note.Trim() });
        });
        return next;
    }

    public void SetDone(Guid id, bool done)
    {
        var o = Find(id) ?? throw new StoreException("That record no longer exists.");
        if (o.Done == done) return;
        Change(() =>
        {
            o.Done = done; o.Modified = now();
            o.History.Add(new HistoryEntry { When = now(), What = done ? "Put away as done" : "Brought back" });
        });
    }

    // ---- attached files -----------------------------------------------------------------------------------------------

    public string DocumentsFolderFor(Guid id) => Path.Combine(folder, "Documents", id.ToString("N"));

    /// <summary>Copies a file into the data folder and lists it on the record.</summary>
    public Attachment Attach(Guid id, string sourcePath)
    {
        var o = Find(id) ?? throw new StoreException("That record no longer exists.");
        if (!File.Exists(sourcePath)) throw new StoreException("That file could not be found.");
        var info = new FileInfo(sourcePath);
        if (info.Length > 500L * 1024 * 1024) throw new StoreException("That file is larger than 500 MB, which is more than Duejoro should hold. Keep it where it is and note its place in the record instead.");
        var dir = DocumentsFolderFor(id);
        string target;
        try
        {
            Directory.CreateDirectory(dir);
            var name = SafeFileName(info.Name);
            target = Path.Combine(dir, name);
            for (var n = 2; File.Exists(target); n++)
                target = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}");
            File.Copy(sourcePath, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StoreException("The file could not be copied: " + ex.Message);
        }
        var attachment = new Attachment
        {
            FileName = info.Name, StoredPath = Path.GetRelativePath(folder, target), Size = info.Length, Added = now(),
        };
        Change(() =>
        {
            o.Attachments.Add(attachment);
            o.Modified = now();
            o.History.Add(new HistoryEntry { When = now(), What = "Attached " + info.Name });
        });
        return attachment;
    }

    /// <summary>Takes a file off the record's list. The copy stays in the Documents folder.</summary>
    public void Detach(Guid id, Guid attachmentId)
    {
        var o = Find(id) ?? throw new StoreException("That record no longer exists.");
        var a = o.Attachments.FirstOrDefault(x => x.Id == attachmentId);
        if (a is null) return;
        Change(() =>
        {
            o.Attachments.Remove(a);
            o.Modified = now();
            o.History.Add(new HistoryEntry { When = now(), What = "Removed " + a.FileName + " from the list (the file stays in the Documents folder)" });
        });
    }

    public string FullPath(Attachment a) => Path.GetFullPath(Path.Combine(folder, a.StoredPath));

    static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "file" : clean;
    }

    // ---- undo and redo ------------------------------------------------------------------------------------------------

    public bool Undo()
    {
        if (undo.Count == 0) return false;
        var current = Serialize();
        var previous = undo[^1]; undo.RemoveAt(undo.Count - 1);
        items = Parse(previous);
        redo.Add(current);
        Save();
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (redo.Count == 0) return false;
        var current = Serialize();
        var next = redo[^1]; redo.RemoveAt(redo.Count - 1);
        items = Parse(next);
        undo.Add(current);
        Save();
        Changed?.Invoke();
        return true;
    }

    // ---- bulk (used by import) ------------------------------------------------------------------------------------------

    /// <summary>Adds many records as one change (a single undo takes them all away again).</summary>
    public int AddMany(IEnumerable<Obligation> many)
    {
        var list = many.ToList();
        foreach (var o in list) Validate(o);
        if (list.Count == 0) return 0;
        Change(() =>
        {
            foreach (var o in list)
            {
                o.Created = o.Modified = now();
                o.History.Add(new HistoryEntry { When = now(), What = "Imported" });
                items.Add(o);
            }
        });
        return list.Count;
    }

    static void Validate(Obligation o)
    {
        if (string.IsNullOrWhiteSpace(o.Name)) throw new StoreException("Give it a name.");
        o.Name = o.Name.Trim();
        o.Category = o.Category.Trim(); o.Owner = o.Owner.Trim(); o.Issuer = o.Issuer.Trim(); o.Reference = o.Reference.Trim();
        o.RemindDaysBefore = Math.Clamp(o.RemindDaysBefore, 0, DueMath.MaxRemindDays);
        o.RepeatEvery = Math.Clamp(o.RepeatEvery, 1, 999);
        if (o.Cost is < 0) throw new StoreException("A cost cannot be negative.");
    }
}
