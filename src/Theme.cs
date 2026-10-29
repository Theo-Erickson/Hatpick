using System;
using System.Collections.Generic;
using Avalonia.Styling;

namespace Hatpick
{
    // Themes are just palettes. Every color in the UI is a named brush in the app's
    // resources, referenced dynamically, so switching theme swaps the brushes and the
    // whole window repaints. Adding a theme = adding one more palette below.
    static class AppTheme
    {
        public static readonly string[] Names = { "dark", "light" };
        public static string Current = "dark";

        static readonly Dictionary<string, Dictionary<string, string>> Palettes = new Dictionary<string, Dictionary<string, string>>
        {
            { "dark", new Dictionary<string, string> {
                {"Bg","#10131c"}, {"Rail","#0b0d14"}, {"RailActive","#1f2433"}, {"RailIcon","#9aa3b8"}, {"RailAccent","#f08a4b"},
                {"Side","#151925"}, {"Panel","#1a1e2b"}, {"PanelHover","#222739"}, {"Input","#0f121b"},
                {"Border","#2a3045"}, {"Dashed","#4a5370"}, {"Text","#e6e8ef"}, {"Sub","#c3c9d8"}, {"Muted","#9aa3b8"}, {"Faint","#6c7590"},
                {"Link","#8fb4ff"}, {"Accent","#f08a4b"}, {"AccentText","#1a1006"}, {"SelRow","#232a3d"},
                {"WarnBg","#2a2412"}, {"WarnBorder","#5a4a1a"}, {"WarnText","#f3e3b4"},
                {"ErrBg","#2c1618"}, {"ErrBorder","#62302f"}, {"ErrText","#f6c9c6"},
                {"InfoBg","#16213a"}, {"InfoBorder","#2c3f68"}, {"InfoText","#c9d8ff"},
                {"OkBg","#173a2a"}, {"OkText","#7fe0ad"},
                {"WinBg","#3a2414"}, {"WinText","#ffb88a"}, {"WinBorder","#6b3a1c"},
                {"DotOk","#4cc38a"}, {"DotWarn","#e8b339"}, {"DotErr","#f2706b"}, {"DotInfo","#6f9bff"},
                {"TintYellow","#3a3217"}, {"TintRed","#3a1d1f"}, {"TintDark","#2a2d3a"}, {"TintBlue","#1c2a4a"}, {"TintBrown","#33261c"},
                {"Shade","#B3000000"}, {"TourCard","#1a1e2b"}, {"TourText","#e6e8ef"}, {"Scroll","#3a4258"} } },
            { "light", new Dictionary<string, string> {
                {"Bg","#f3f4f8"}, {"Rail","#2a3352"}, {"RailActive","#3b4670"}, {"RailIcon","#c4cbe0"}, {"RailAccent","#ffffff"},
                {"Side","#ffffff"}, {"Panel","#ffffff"}, {"PanelHover","#f3f5fb"}, {"Input","#f6f7fb"},
                {"Border","#dde1ec"}, {"Dashed","#aab2c8"}, {"Text","#1d2233"}, {"Sub","#3b4256"}, {"Muted","#586078"}, {"Faint","#8a92a8"},
                {"Link","#2a4bb5"}, {"Accent","#2a3352"}, {"AccentText","#ffffff"}, {"SelRow","#e9ecf5"},
                {"WarnBg","#fff6d8"}, {"WarnBorder","#f0dc9a"}, {"WarnText","#4d3800"},
                {"ErrBg","#fde9e7"}, {"ErrBorder","#f3c4bf"}, {"ErrText","#7a1d15"},
                {"InfoBg","#eaf0ff"}, {"InfoBorder","#c9d6f5"}, {"InfoText","#1e3a7a"},
                {"OkBg","#dcf3e6"}, {"OkText","#17603a"},
                {"WinBg","#fff0e6"}, {"WinText","#8a3a0e"}, {"WinBorder","#f3c6a5"},
                {"DotOk","#1f8a52"}, {"DotWarn","#b07800"}, {"DotErr","#c62f24"}, {"DotInfo","#2f5fc4"},
                {"TintYellow","#fff3c9"}, {"TintRed","#fde4e4"}, {"TintDark","#e6e8ef"}, {"TintBlue","#e3ebff"}, {"TintBrown","#f1e6dc"},
                {"Shade","#8C1D2233"}, {"TourCard","#1d2233"}, {"TourText","#ffffff"}, {"Scroll","#c4cad9"} } },
        };

        // Fluent's own brushes for the controls Hatpick doesn't restyle, pointed at ours.
        static readonly Dictionary<string, string> FluentKeys = new Dictionary<string, string>
        {
            {"TextControlForeground","Text"}, {"TextControlForegroundPointerOver","Text"}, {"TextControlForegroundFocused","Text"},
            {"TextControlBackground","Input"}, {"TextControlBackgroundPointerOver","Input"}, {"TextControlBackgroundFocused","Input"},
            {"TextControlBorderBrush","Border"}, {"TextControlBorderBrushPointerOver","Dashed"}, {"TextControlBorderBrushFocused","Accent"},
            {"TextControlPlaceholderForeground","Faint"},
            {"ToolTipBackground","Panel"}, {"ToolTipForeground","Text"}, {"ToolTipBorderBrush","Border"},
        };

        public static void Init(Application app)
        {
            app.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
            app.Styles.Add(new HatpickStyles());
            Apply(app, Current);
        }

        public static void Apply(Application app, string name)
        {
            Dictionary<string, string> p;
            if (!Palettes.TryGetValue(name, out p)) { name = "dark"; p = Palettes["dark"]; }
            Current = name;
            foreach (var kv in p) app.Resources[kv.Key] = new SolidColorBrush(Color.Parse(kv.Value));
            foreach (var kv in FluentKeys) app.Resources[kv.Key] = app.Resources[kv.Value];
            // Fluent's own parts and the window title bar (Windows, macOS) follow this.
            app.RequestedThemeVariant = name == "light" ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        public static IBrush Brush(string key)
        {
            object b;
            return Application.Current.Resources.TryGetValue(key, out b) ? b as IBrush : null;
        }
    }
}
