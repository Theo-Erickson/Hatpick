using Avalonia.Markup.Xaml;

namespace Hatpick
{
    // Loads Styles.axaml (control looks). Added to the app after the Fluent theme, so it wins.
    public partial class HatpickStyles : Avalonia.Styling.Styles
    {
        public HatpickStyles() { AvaloniaXamlLoader.Load(this); }
    }
}
