// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Duejoro.Core;

/// <summary>How often a record comes round again.</summary>
public enum RepeatUnit { None = 0, Days = 1, Weeks = 2, Months = 3, Years = 4 }

/// <summary>Where a record stands, worked out from its date and today's date.</summary>
public enum DueStatus
{
    /// <summary>The date has passed and it has not been renewed.</summary>
    Overdue = 0,
    /// <summary>Inside the reminder period.</summary>
    DueSoon = 1,
    /// <summary>Further away than the reminder period.</summary>
    Upcoming = 2,
    /// <summary>No date set (for example a contract with no end).</summary>
    NoDate = 3,
    /// <summary>Put away: finished, canceled or no longer needed.</summary>
    Done = 4,
}

/// <summary>A file that belongs to a record (a scan of the license, the policy, the contract). The copy lives in the data folder.</summary>
public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The name it had when it was attached.</summary>
    public string FileName { get; set; } = "";
    /// <summary>Where the copy is, relative to the data folder.</summary>
    public string StoredPath { get; set; } = "";
    public long Size { get; set; }
    public DateTime Added { get; set; } = DateTime.Now;
}

/// <summary>One thing that happened to a record: it was renewed, a note was added, it was put away.</summary>
public sealed class HistoryEntry
{
    public DateTime When { get; set; } = DateTime.Now;
    public string What { get; set; } = "";
}

/// <summary>
/// Something that has a date the business must not miss: a license, a permit, an insurance policy, a certification, a contract, a lease,
/// a subscription, a warranty, a filing.
/// </summary>
public sealed class Obligation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    /// <summary>The person or team responsible for it.</summary>
    public string Owner { get; set; } = "";
    /// <summary>Who issued it or who it is with (an agency, an insurer, a vendor).</summary>
    public string Issuer { get; set; } = "";
    /// <summary>A license, policy or account number.</summary>
    public string Reference { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    /// <summary>How many days before the date it should start to be flagged.</summary>
    public int RemindDaysBefore { get; set; } = 30;
    public RepeatUnit RepeatUnit { get; set; } = RepeatUnit.None;
    public int RepeatEvery { get; set; } = 1;
    /// <summary>What it costs each time (renewal fee, premium), if anything.</summary>
    public decimal? Cost { get; set; }
    public string Notes { get; set; } = "";
    public bool Done { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;
    public List<Attachment> Attachments { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];

    /// <summary>A copy that can be edited without touching the original.</summary>
    public Obligation Clone() => new()
    {
        Id = Id, Name = Name, Category = Category, Owner = Owner, Issuer = Issuer, Reference = Reference, DueDate = DueDate,
        RemindDaysBefore = RemindDaysBefore, RepeatUnit = RepeatUnit, RepeatEvery = RepeatEvery, Cost = Cost, Notes = Notes, Done = Done,
        Created = Created, Modified = Modified,
        Attachments = Attachments.Select(a => new Attachment { Id = a.Id, FileName = a.FileName, StoredPath = a.StoredPath, Size = a.Size, Added = a.Added }).ToList(),
        History = History.Select(h => new HistoryEntry { When = h.When, What = h.What }).ToList(),
    };
}
