// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Duejoro.App;

/// <summary>
/// The toolbar icons: bare, bold, round-ended line drawings on a 24 by 24 grid, one color per group of tools (the same approach as Ledjoro
/// and Pagjoro). They are vector shapes, so they stay sharp at any screen scaling and need no image files.
/// Use in XAML as <c>Content="{local:Icon New}"</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(UIElement))]
public sealed class IconExtension : MarkupExtension
{
    public IconExtension() { }
    public IconExtension(string key) { Key = key; }

    public string Key { get; set; } = "";
    public double Size { get; set; } = 18;

    // group colors: deeper for light themes, lighter for dark themes
    enum Group { Graphite, Red, Orange, Green, Purple, Blue, Slate, Pink }
    static readonly Color[][] Palette =
    [
        [Color.FromRgb(55, 65, 81),    Color.FromRgb(209, 213, 219)],
        [Color.FromRgb(185, 28, 28),   Color.FromRgb(248, 113, 113)],
        [Color.FromRgb(234, 108, 12),  Color.FromRgb(251, 146, 60)],
        [Color.FromRgb(22, 163, 74),   Color.FromRgb(74, 222, 128)],
        [Color.FromRgb(124, 58, 237),  Color.FromRgb(167, 139, 250)],
        [Color.FromRgb(6, 119, 163),   Color.FromRgb(77, 196, 232)],
        [Color.FromRgb(100, 116, 139), Color.FromRgb(148, 163, 184)],
        [Color.FromRgb(219, 39, 119),  Color.FromRgb(244, 114, 182)],
    ];

    // key -> (group, outline path, optional solid path)
    static readonly Dictionary<string, (Group Group, string Line, string? Solid)> Shapes = new()
    {
        ["New"]      = (Group.Green, "M12 5 V19 M5 12 H19", null),
        ["Edit"]     = (Group.Orange, "M4 20 L5 15 L16 4 L20 8 L9 19 Z M14 6 L18 10", null),
        ["Renew"]    = (Group.Green, "M20 12 A8 8 0 1 1 16 5 M20 4 V9 H15", null),
        ["Check"]    = (Group.Blue, "M4 12.5 L9.5 18 L20 6.5", null),
        ["Attach"]   = (Group.Purple, "M16 7 L8 15 A2.5 2.5 0 0 0 11.5 18.5 L19 11 A4.5 4.5 0 0 0 12.6 4.6 L5.5 11.7", null),
        ["Delete"]   = (Group.Red, "M4 7 H20 M9 7 V4 H15 V7 M6 7 L7 20 H17 L18 7 M10 11 V16 M14 11 V16", null),
        ["Undo"]     = (Group.Slate, "M9 5 L4 10 L9 15 M4 10 H15 A5 5 0 0 1 15 20 H10", null),
        ["Redo"]     = (Group.Slate, "M15 5 L20 10 L15 15 M20 10 H9 A5 5 0 0 0 9 20 H14", null),
        ["Import"]   = (Group.Blue, "M12 3 V14 M7 10 L12 15 L17 10 M4 15 V20 H20 V15", null),
        ["Export"]   = (Group.Blue, "M12 15 V4 M7 8 L12 3 L17 8 M4 15 V20 H20 V15", null),
        ["Calendar"] = (Group.Orange, "M4 6 H20 V20 H4 Z M4 10 H20 M8 3 V7 M16 3 V7", null),
        ["Keyboard"] = (Group.Slate, "M3 6 H21 V18 H3 Z M7 10 H7.01 M11 10 H11.01 M15 10 H15.01 M18 10 H18.01 M7 14 H17", null),
        ["Bell"]     = (Group.Orange, "M6 17 H18 L16.5 14 V10 A4.5 4.5 0 0 0 7.5 10 V14 Z M10 20 H14", null),
        ["Find"]     = (Group.Blue, "M10.5 3.5 A7 7 0 1 0 10.5 17.5 A7 7 0 1 0 10.5 3.5 Z M15.5 15.5 L21 21", null),
    };

    static bool dark = SystemIsDark();

    static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception) { return false; }
    }

    /// <summary>True when the window has a dark background: icons use their lighter colors. Setting it recolors every icon already on screen.</summary>
    public static bool Dark
    {
        get => dark;
        set
        {
            dark = value;
            live.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var w in live) if (w.TryGetTarget(out var icon)) icon.Apply();
        }
    }

    sealed class Live(Group group, Path line, Path? solid)
    {
        public void Apply()
        {
            var brush = new SolidColorBrush(Palette[(int)group][dark ? 1 : 0]); brush.Freeze();
            line.Stroke = brush;
            if (solid is not null) solid.Fill = brush;
        }
    }
    static readonly List<WeakReference<Live>> live = [];

    public override object ProvideValue(IServiceProvider serviceProvider) => Create(Key, Size);

    /// <summary>Builds the icon for <paramref name="key"/>; an unknown key gives an empty element.</summary>
    public static UIElement Create(string key, double size = 18)
    {
        if (!Shapes.TryGetValue(key, out var shape)) return new Border { Width = size, Height = size };
        var grid = new Grid { Width = size, Height = size, IsHitTestVisible = false };
        var host = new Viewbox { Stretch = Stretch.Uniform, Child = new Canvas { Width = 24, Height = 24 } };
        var canvas = (Canvas)host.Child;
        var line = new Path
        {
            Data = Geometry.Parse(shape.Line), StrokeThickness = 2.3,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        };
        canvas.Children.Add(line);
        Path? solid = null;
        if (shape.Solid is not null) { solid = new Path { Data = Geometry.Parse(shape.Solid) }; canvas.Children.Add(solid); }
        var icon = new Live(shape.Group, line, solid);
        icon.Apply();
        live.Add(new WeakReference<Live>(icon));
        // keep the recoloring object alive as long as the element is
        grid.Tag = icon;
        grid.Children.Add(host);
        return grid;
    }
}
