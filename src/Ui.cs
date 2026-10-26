using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Hatpick
{
    // Small builders so the pages read like layout, not plumbing. Colors are always
    // theme keys (see Theme.cs), attached as dynamic references.
    static class Ui
    {
        public static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas");

        public static T Res<T>(T el, DependencyProperty p, string key) where T : FrameworkElement
        {
            el.SetResourceReference(p, key);
            return el;
        }

        public static Style Style(string key) { return (Style)Application.Current.FindResource(key); }

        public static TextBlock Text(string text, double size = 14, string brush = "Text", FontWeight? weight = null)
        {
            var t = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
            if (weight.HasValue) t.FontWeight = weight.Value;
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return t;
        }

        public static TextBlock MonoText(string text, double size = 12, string brush = "Sub")
        {
            var t = Text(text, size, brush);
            t.FontFamily = Mono;
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        public static TextBlock SectionTitle(string text)
        {
            var t = Text(text.ToUpperInvariant(), 12, "Muted", FontWeights.SemiBold);
            t.Margin = new Thickness(0, 0, 0, 0);
            return t;
        }

        public static Button Btn(string text, Action onClick, string style = "Btn")
        {
            var b = new Button { Content = text, Style = Style(style) };
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        public static Button IconBtn(UIElement icon, string text, Action onClick, string style = "Btn")
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(icon);
            var t = new TextBlock { Text = text, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(t);
            var b = new Button { Content = sp, Style = Style(style) };
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        public static Border Panel(UIElement child, double padding = 16, string bg = "Panel", string border = "Border", double radius = 12)
        {
            var b = new Border { Child = child, Padding = new Thickness(padding), CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
            b.SetResourceReference(Border.BackgroundProperty, bg);
            b.SetResourceReference(Border.BorderBrushProperty, border);
            return b;
        }

        public static StackPanel Row(double gap, params UIElement[] items)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < items.Length; i++)
            {
                var fe = items[i] as FrameworkElement;
                if (fe != null && i > 0) fe.Margin = new Thickness(gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                sp.Children.Add(items[i]);
            }
            return sp;
        }

        public static StackPanel Stack(double gap, params UIElement[] items)
        {
            var sp = new StackPanel();
            foreach (var it in items) Add(sp, it, gap);
            return sp;
        }

        public static void Add(StackPanel sp, UIElement el, double gapAbove)
        {
            var fe = el as FrameworkElement;
            if (fe != null && sp.Children.Count > 0) fe.Margin = new Thickness(fe.Margin.Left, gapAbove, fe.Margin.Right, fe.Margin.Bottom);
            sp.Children.Add(el);
        }

        // A grid that shows 3 / 2 / 1 columns depending on its width.
        public static UniformGrid Cards(double minCard = 280)
        {
            var g = new UniformGrid { Columns = 3 };
            g.SizeChanged += delegate
            {
                int cols = Math.Max(1, Math.Min(3, (int)(g.ActualWidth / minCard)));
                if (g.Columns != cols) g.Columns = cols;
            };
            return g;
        }

        public static FrameworkElement Gap(FrameworkElement el, double right, double bottom)
        {
            el.Margin = new Thickness(0, 0, right, bottom);
            return el;
        }

        public static Ellipse Dot(Level l, double size = 8)
        {
            var e = new Ellipse { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            e.SetResourceReference(Shape.FillProperty, LevelDot(l));
            return e;
        }

        public static string LevelDot(Level l)
        {
            switch (l)
            {
                case Level.Ok: return "DotOk";
                case Level.Info: return "DotInfo";
                case Level.Warn: return "DotWarn";
                default: return "DotErr";
            }
        }

        // ---------- line icons (24x24, stroked in a theme color) ----------

        static readonly Dictionary<string, string> IconPaths = new Dictionary<string, string>
        {
            { "overview", "M4.5 3h4.5a1.5 1.5 0 0 1 1.5 1.5v4.5a1.5 1.5 0 0 1-1.5 1.5H4.5A1.5 1.5 0 0 1 3 9V4.5A1.5 1.5 0 0 1 4.5 3z M15 3h4.5A1.5 1.5 0 0 1 21 4.5V9a1.5 1.5 0 0 1-1.5 1.5H15A1.5 1.5 0 0 1 13.5 9V4.5A1.5 1.5 0 0 1 15 3z M4.5 13.5h4.5a1.5 1.5 0 0 1 1.5 1.5v4.5A1.5 1.5 0 0 1 9 21H4.5A1.5 1.5 0 0 1 3 19.5V15a1.5 1.5 0 0 1 1.5-1.5z M15 13.5h4.5A1.5 1.5 0 0 1 21 15v4.5a1.5 1.5 0 0 1-1.5 1.5H15a1.5 1.5 0 0 1-1.5-1.5V15A1.5 1.5 0 0 1 15 13.5z" },
            { "repos", "M6 3.5a2.5 2.5 0 1 1 0 5a2.5 2.5 0 1 1 0-5z M6 15.5a2.5 2.5 0 1 1 0 5a2.5 2.5 0 1 1 0-5z M18 5.5a2.5 2.5 0 1 1 0 5a2.5 2.5 0 1 1 0-5z M6 8.5v7 M18 10.5c0 4-6 3-11 6" },
            { "hats", "M4 16a8 8 0 0 1 16 0 M2.5 16.5h19 M12 8v8" },
            { "folders", "M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5z" },
            { "reset", "M4 12a8 8 0 1 0 2.3-5.6 M4 4v4h4" },
            { "theme", "M12 4a8 8 0 1 1 0 16a8 8 0 1 1 0-16z M12 4v16" },
            { "help", "M12 3a9 9 0 1 1 0 18a9 9 0 1 1 0-18z M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.5V14 M12 17.5v.01" },
            { "search", "M11 4a7 7 0 1 1 0 14a7 7 0 1 1 0-14z M20 20l-4-4" },
            { "plus", "M12 5v14 M5 12h14" },
            { "key", "M8 10a4 4 0 1 1 0 8a4 4 0 1 1 0-8z M11 11l8-8 M16 6l2 2 M14 8l2 2" },
            { "cloud", "M7 18a5 5 0 0 1-.6-10A6 6 0 0 1 18 9a4.5 4.5 0 0 1-.5 9z" },
            { "chevron", "M6 9l6 6 6-6" },
            { "check", "M5 12l5 5 9-10" },
            { "folder", "M3 6.5A1.5 1.5 0 0 1 4.5 5H9l2 2h8.5A1.5 1.5 0 0 1 21 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5z" },
            { "external", "M14 4h6v6 M20 4l-9 9 M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5" },
            { "terminal", "M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z M7 10l3 2-3 2 M12 15h5" },
            { "rescan", "M20 12a8 8 0 1 1-2.3-5.6 M20 4v4h-4" },
            { "user", "M12 4a4 4 0 1 1 0 8a4 4 0 1 1 0-8z M4 20c0-3.5 3.6-6 8-6s8 2.5 8 6" },
            { "close", "M6 6l12 12 M18 6L6 18" },
        };

        public static FrameworkElement Icon(string name, double size = 18, string brush = "Muted", double stroke = 1.8)
        {
            var p = new Path
            {
                Data = Geometry.Parse(IconPaths[name]), StrokeThickness = stroke,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
            };
            p.SetResourceReference(Shape.StrokeProperty, brush);
            var c = new Canvas { Width = 24, Height = 24 };
            c.Children.Add(p);
            return new Viewbox { Width = size, Height = size, Child = c, VerticalAlignment = VerticalAlignment.Center };
        }

        // Filled status glyphs.
        public static FrameworkElement StatusIcon(Level l, double size = 18)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            if (l == Level.Warn)
            {
                c.Children.Add(Fill(new Path { Data = Geometry.Parse("M12 3 2 20h20z") }, "DotWarn"));
                c.Children.Add(Stroke(new Path { Data = Geometry.Parse("M12 10v4 M12 17v.01") }, "Bg", 2.2));
            }
            else if (l == Level.Error)
            {
                c.Children.Add(Fill(new Ellipse { Width = 20, Height = 20 }, "DotErr", 2, 2));
                c.Children.Add(Stroke(new Path { Data = Geometry.Parse("M12 7v6 M12 16.5v.01") }, "Bg", 2.2));
            }
            else if (l == Level.Info)
            {
                c.Children.Add(Fill(new Ellipse { Width = 20, Height = 20 }, "DotInfo", 2, 2));
                c.Children.Add(Stroke(new Path { Data = Geometry.Parse("M12 11v6 M12 7.5v.01") }, "Bg", 2.2));
            }
            else
            {
                c.Children.Add(Fill(new Ellipse { Width = 20, Height = 20 }, "DotOk", 2, 2));
                c.Children.Add(Stroke(new Path { Data = Geometry.Parse("M7.5 12.5l3 3 6-6.5") }, "Bg", 2.2));
            }
            return new Viewbox { Width = size, Height = size, Child = c, VerticalAlignment = VerticalAlignment.Center };
        }

        static Shape Fill(Shape s, string key, double left = 0, double top = 0)
        {
            s.SetResourceReference(Shape.FillProperty, key);
            Canvas.SetLeft(s, left); Canvas.SetTop(s, top);
            return s;
        }

        static Shape Stroke(Shape s, string key, double w)
        {
            s.SetResourceReference(Shape.StrokeProperty, key);
            s.StrokeThickness = w;
            s.StrokeStartLineCap = PenLineCap.Round; s.StrokeEndLineCap = PenLineCap.Round;
            return s;
        }

        // ---------- "?" help ----------

        public static Button HelpButton(string topic)
        {
            HelpText.Topic t;
            if (topic == null || !HelpText.Topics.TryGetValue(topic, out t)) return null;
            var b = new Button { Style = Style("HelpBtn"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            b.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Help: " + t.Title);
            b.Click += delegate { ShowHelp(b, t); };
            return b;
        }

        public static void ShowHelp(UIElement anchor, HelpText.Topic t)
        {
            var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 6 };
            var body = Stack(8, Text(t.Title, 15, "Text", FontWeights.SemiBold), Text(t.Body, 13, "Sub"));
            var ok = Btn("Got it", delegate { popup.IsOpen = false; }, "BtnLink");
            ok.HorizontalAlignment = HorizontalAlignment.Left;
            ok.Margin = new Thickness(-4, 10, 0, 0);
            body.Children.Add(ok);
            var card = Panel(body, 16);
            card.MaxWidth = 400;
            card.Margin = new Thickness(0, 0, 12, 12);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 };
            popup.Child = card;
            popup.IsOpen = true;
        }

        public static StackPanel TitleWithHelp(TextBlock title, string topic)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(title);
            var h = HelpButton(topic);
            if (h != null) sp.Children.Add(h);
            return sp;
        }

        // A dropdown made of our own pieces, so it follows the theme everywhere.
        public class Choice { public string Value, Label, Sub, Hat; }

        public static Button Picker(List<Choice> choices, string selected, Action<string> onPick, double width = 260)
        {
            var btn = new Button { Style = Style("Btn"), Width = width, Height = 36, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 0, 10, 0) };
            Action<string> show = null;
            show = v =>
            {
                Choice c = choices.Find(x => x.Value == v) ?? choices[0];
                var g = new DockPanel { LastChildFill = true };
                var chev = Icon("chevron", 14);
                DockPanel.SetDock(chev, Dock.Right);
                g.Children.Add(chev);
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(c.Hat != null ? Hats.Icon(c.Hat, 20) : (UIElement)new Border { Width = 0 });
                var t = new TextBlock { Text = c.Label, Margin = new Thickness(c.Hat != null ? 8 : 0, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                row.Children.Add(t);
                g.Children.Add(row);
                btn.Content = g;
                btn.Tag = v;
            };
            show(selected);
            btn.Click += delegate
            {
                var popup = new Popup { PlacementTarget = btn, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4 };
                var list = new StackPanel();
                foreach (var ch in choices)
                {
                    var choice = ch;
                    var r = new StackPanel { Orientation = Orientation.Horizontal };
                    if (choice.Hat != null) { var hi = Hats.Icon(choice.Hat, 22); hi.Margin = new Thickness(0, 0, 10, 0); r.Children.Add(hi); }
                    var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    texts.Children.Add(Text(choice.Label, 13, "Text", (string)btn.Tag == choice.Value ? FontWeights.SemiBold : FontWeights.Normal));
                    if (!string.IsNullOrEmpty(choice.Sub)) texts.Children.Add(Text(choice.Sub, 11.5, "Muted"));
                    r.Children.Add(texts);
                    var item = new Button { Style = Style("Row"), Content = r, MinHeight = 40 };
                    item.Click += delegate { popup.IsOpen = false; show(choice.Value); onPick(choice.Value); };
                    list.Children.Add(item);
                }
                var card = Panel(list, 6);
                card.MinWidth = width;
                card.Margin = new Thickness(0, 0, 12, 12);
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 };
                popup.Child = card;
                popup.IsOpen = true;
            };
            return btn;
        }
    }

    // The hat pictures, drawn from simple shapes so they stay crisp at any size.
    static class Hats
    {
        public static string[] Kinds { get { return HatKinds.All; } }
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        { { "hardhat", "Hard hat" }, { "beanie", "Beanie" }, { "tophat", "Top hat" }, { "cap", "Baseball cap" }, { "bowler", "Bowler" } };

        public static string TintKey(string kind)
        {
            switch (kind)
            {
                case "hardhat": return "TintYellow";
                case "beanie": return "TintRed";
                case "cap": return "TintBlue";
                case "bowler": return "TintBrown";
                default: return "TintDark";
            }
        }

        static SolidColorBrush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }

        static void P(Canvas c, string data, string fill)
        {
            c.Children.Add(new Path { Data = Geometry.Parse(data), Fill = B(fill) });
        }

        static void R(Canvas c, double x, double y, double w, double h, double r, string fill)
        {
            var rect = new Rectangle { Width = w, Height = h, RadiusX = r, RadiusY = r, Fill = B(fill) };
            Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
            c.Children.Add(rect);
        }

        static void E(Canvas c, double cx, double cy, double rx, double ry, string fill)
        {
            var e = new Ellipse { Width = rx * 2, Height = ry * 2, Fill = B(fill) };
            Canvas.SetLeft(e, cx - rx); Canvas.SetTop(e, cy - ry);
            c.Children.Add(e);
        }

        // kind "none" = a dashed empty circle (no hat).
        public static FrameworkElement Icon(string kind, double size)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            switch (kind)
            {
                case "hardhat":
                    P(c, "M4 16a8 8 0 0 1 16 0z", "#f5c02a");
                    R(c, 10.4, 7.2, 3.2, 8.8, 1.6, "#ffd95a");
                    R(c, 2, 15.5, 20, 3.2, 1.6, "#d9a514");
                    break;
                case "beanie":
                    P(c, "M5 15a7 7 0 0 1 14 0z", "#d64545");
                    R(c, 4, 13.5, 16, 5, 2, "#a83232");
                    E(c, 12, 6.4, 2.3, 2.3, "#f4efe6");
                    break;
                case "tophat":
                    R(c, 7, 4, 10, 12.5, 1, "#2e2e3a");
                    E(c, 12, 4.2, 5, 1.2, "#4a4a5a");
                    R(c, 7, 12.6, 10, 2.6, 0, "#ec7a30");
                    E(c, 12, 17, 9.5, 2, "#2e2e3a");
                    break;
                case "cap":
                    P(c, "M5 16a7 7 0 0 1 14 0z", "#3468c4");
                    P(c, "M14 15.2h7.5a1.6 1.6 0 0 1-1.6 1.8H14z", "#24508f");
                    E(c, 12, 8.6, 1.1, 1.1, "#24508f");
                    break;
                case "bowler":
                    P(c, "M6.5 15a5.5 5.5 0 0 1 11 0z", "#6b4a2e");
                    R(c, 6.5, 12.6, 11, 2, 0, "#2e2118");
                    E(c, 12, 15.6, 9, 1.7, "#6b4a2e");
                    break;
                default:
                    var ring = new Ellipse { Width = 18, Height = 18, StrokeThickness = 1.8, StrokeDashArray = new DoubleCollection { 2, 2 } };
                    ring.SetResourceReference(Shape.StrokeProperty, "Dashed");
                    Canvas.SetLeft(ring, 3); Canvas.SetTop(ring, 3);
                    c.Children.Add(ring);
                    break;
            }
            return new Viewbox { Width = size, Height = size, Child = c, VerticalAlignment = VerticalAlignment.Center };
        }

        // Hat on a soft tinted tile, used on cards.
        public static Border Tile(string kind, double size)
        {
            var b = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.24), Child = Icon(kind, size * 0.72) };
            b.SetResourceReference(Border.BackgroundProperty, kind == "none" ? "Input" : TintKey(kind));
            return b;
        }
    }
}
