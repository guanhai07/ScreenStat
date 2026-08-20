using System.Drawing;
using System.Windows.Forms;
using ScreenStat.App.Services;

namespace ScreenStat.App.Infrastructure;

/// <summary>
/// Paints the tray menu in the app theme. The menu is WinForms, so it ignores
/// everything in the WPF resource dictionaries and would otherwise show up
/// bright white under a dark theme — on the app's main entry point.
/// Colours only: WinForms menus cannot do rounded corners or acrylic.
/// </summary>
internal sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
{
    private readonly SystemThemeService _themeService;

    public ThemedToolStripRenderer(SystemThemeService themeService)
        : base(new ThemedColorTable(themeService))
    {
        _themeService = themeService;
        RoundedEdges = false;
    }

    private Color TextColor => _themeService.IsDark
        ? Color.FromArgb(255, 255, 255)
        : Color.FromArgb(26, 26, 26);

    private Color DisabledTextColor => _themeService.IsDark
        ? Color.FromArgb(120, 120, 120)
        : Color.FromArgb(150, 150, 150);

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : DisabledTextColor;
        base.OnRenderItemText(e);
    }

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly SystemThemeService _themeService;

        public ThemedColorTable(SystemThemeService themeService)
        {
            _themeService = themeService;
            UseSystemColors = false;
        }

        private bool IsDark => _themeService.IsDark;

        private Color Surface => IsDark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
        private Color Hover => IsDark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(234, 234, 234);
        private Color Edge => IsDark ? Color.FromArgb(65, 65, 65) : Color.FromArgb(219, 219, 219);

        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientMiddle => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color MenuBorder => Edge;
        public override Color SeparatorLight => Edge;
        public override Color SeparatorDark => Edge;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
    }
}
