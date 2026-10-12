// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Text.RegularExpressions;
using Duejoro.Core;

namespace Duejoro.Tests;

public class CoreTests
{
    [Fact]
    public void Version_is_a_semantic_version_without_a_build_hash()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$", AppInfo.Version);
        Assert.DoesNotContain('+', AppInfo.Version);
    }

    [Fact]
    public void Name_and_links_are_set()
    {
        Assert.Equal("Duejoro", AppInfo.Name);
        Assert.StartsWith("https://github.com/Tusivo/", AppInfo.Repository);
        Assert.Equal("info@tusivo.com", AppInfo.ContactEmail);
    }

    [Fact]
    public void A_test_run_never_creates_a_data_folder_beside_the_test_host()
    {
        // the portable folder is only used when the program itself is Duejoro.exe
        var host = Environment.ProcessPath!;
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(host)!, AppPaths.FolderName)));
        Assert.False(AppPaths.IsPortable);
    }
}

/// <summary>
/// The clean-room policy (LEGAL.md) says the product must not use another company's names or marks. This test keeps the source code honest:
/// it fails if a trademark we have promised not to use shows up in the program's own files.
/// </summary>
public class PolicyTests
{
    static readonly string[] Forbidden = { "Calendly", "Acuity", "Ironclad", "ContractWorks", "Smartsheet" };

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Duejoro.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (Duejoro.sln).");
    }

    [Fact]
    public void Program_source_does_not_use_other_companies_marks()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            if (!Regex.IsMatch(file, @"\.(cs|xaml|csproj|json|txt|md|manifest)$", RegexOptions.IgnoreCase)) continue;
            var text = File.ReadAllText(file);
            foreach (var word in Forbidden)
                if (text.Contains(word, StringComparison.OrdinalIgnoreCase)) offenders.Add($"{Path.GetRelativePath(src, file)}: {word}");
        }
        Assert.True(offenders.Count == 0, "These files use another company's name: " + string.Join("; ", offenders));
    }

    [Fact]
    public void The_license_file_is_the_agpl()
    {
        var license = File.ReadAllText(Path.Combine(RepoRoot(), "LICENSE"));
        Assert.Contains("GNU AFFERO GENERAL PUBLIC LICENSE", license);
    }
}

public class SettingsTests
{
    [Fact]
    public void TrayReminders_AreOffByDefault_AndRemembered()
    {
        var dir = Path.Combine(Path.GetTempPath(), "duejoro-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.False(new AppSettings(dir).TrayReminders);
            var s = new AppSettings(dir);
            s.SetTheme("night");
            s.SetTrayReminders(true);
            var again = new AppSettings(dir);
            Assert.True(again.TrayReminders);
            Assert.Equal("night", again.Theme);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
