using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Hatpick
{
    // A dialog window that follows the theme (background, fonts, dark title bar).
    class ThemedWindow : Window
    {
        public ThemedWindow(Window owner, string title, double width)
        {
            Title = title;
            Owner = owner;
            Width = width;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            FontSize = 14;
            SetResourceReference(BackgroundProperty, "Bg");
            SetResourceReference(ForegroundProperty, "Text");
            SourceInitialized += delegate { Theme.DarkTitleBar(this); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        // Standard layout: content, then a right-aligned button row.
        public void Layout(UIElement content, params Button[] buttons)
        {
            var root = new DockPanel { Margin = new Thickness(24, 20, 24, 20) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); b.MinWidth = 90; row.Children.Add(b); }
            DockPanel.SetDock(row, Dock.Bottom);
            root.Children.Add(row);
            root.Children.Add(content);
            Content = root;
        }
    }

    static class Dialogs
    {
        public static bool Ask(Window owner, string title, string message, string ok = "OK", string cancel = "Cancel", bool primary = true)
        {
            return Ask(owner, title, Ui.Text(message, 14, "Sub"), ok, cancel, primary);
        }

        public static bool Ask(Window owner, string title, UIElement body, string ok, string cancel, bool primary)
        {
            var w = new ThemedWindow(owner, title, 520);
            bool result = false;
            var okBtn = Ui.Btn(ok, delegate { result = true; w.Close(); }, primary ? "BtnPrimary" : "Btn");
            okBtn.IsDefault = true;
            var content = Ui.Stack(12, Ui.Text(title, 18, "Text", FontWeights.SemiBold), body);
            var buttons = cancel != null
                ? new[] { Ui.Btn(cancel, delegate { w.Close(); }), okBtn }
                : new[] { okBtn };
            w.Layout(content, buttons);
            w.ShowDialog();
            return result;
        }

        public static void Info(Window owner, string title, string message)
        {
            Ask(owner, title, message, "OK", null, true);
        }

        public static FrameworkElement IssueList(IEnumerable<Issue> issues)
        {
            var sp = new StackPanel();
            foreach (var i in issues)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                var ic = Ui.StatusIcon(i.Level, 18);
                ic.Margin = new Thickness(0, 1, 10, 0);
                ic.VerticalAlignment = VerticalAlignment.Top;
                DockPanel.SetDock(ic, Dock.Left);
                row.Children.Add(ic);
                row.Children.Add(Ui.Text(i.Text, 13.5, i.Level >= Level.Warn ? "Text" : "Sub"));
                sp.Children.Add(row);
            }
            return sp;
        }
    }

    // Add or edit a hat (account): the kind of hat, its colors or a picture, and the account details.
    class AccountEditor : ThemedWindow
    {
        static readonly string[] Palette =
        {
            "#f5c02a", "#ec7a30", "#d64545", "#c2417a", "#8e5cd9", "#3468c4", "#2a9fd6", "#2bb5a0",
            "#4caf50", "#8bc34a", "#6b4a2e", "#a0785a", "#f4efe6", "#9e9e9e", "#4a4a5a", "#1e1e24",
        };

        readonly Account target;
        readonly List<Account> all;
        readonly Account self;
        TextBox label, name, email, github;
        string hat;
        readonly Dictionary<string, string> colors;
        string image;          // picture file name in Hats.ImageDir (kept on save)
        string newPicture;     // full path of a picture picked this time, copied on save
        bool saved;

        Border preview;
        StackPanel colorRows;
        TextBlock pictureNote;
        Button removePicture;

        AccountEditor(Window owner, Account target, List<Account> all, Account self, List<string> gcm)
            : base(owner, self == null ? "Add a hat" : "Edit hat", 600)
        {
            this.target = target; this.all = all; this.self = self;
            hat = string.IsNullOrEmpty(target.hat) ? HatKinds.DefaultFor(all.Count) : target.hat;
            colors = target.colors != null ? new Dictionary<string, string>(target.colors) : new Dictionary<string, string>();
            image = Hats.ImagePath(target) != null ? target.image : null;

            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(self == null ? "Add a hat" : "Edit " + target.label, 20, "Text", FontWeights.SemiBold));
            sp.Children.Add(Gap(Ui.Text("A hat is one of your accounts: who you commit as, and who you push as.", 13, "Muted"), 4, 14));

            // preview on the left, hat kinds + colors + picture on the right
            var look = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            preview = new Border { Width = 112, Height = 112, CornerRadius = new CornerRadius(26), Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(preview, Dock.Left);
            look.Children.Add(preview);

            var right = new StackPanel();
            right.Children.Add(Ui.Text("Hat", 12, "Muted"));
            var hats = new WrapPanel { Margin = new Thickness(0, 6, 0, 12) };
            Action refreshKinds = null;
            refreshKinds = delegate
            {
                hats.Children.Clear();
                foreach (var k in HatKinds.All)
                {
                    var kind = k;
                    var b = new Button { Style = Ui.Style("Card"), Width = 52, Height = 52, Padding = new Thickness(5), Margin = new Thickness(0, 0, 6, 6), ToolTip = Hats.Names[kind] };
                    b.Content = Hats.Icon(kind, 34, colors);
                    if (kind == hat) { b.SetResourceReference(BorderBrushProperty, "Accent"); b.BorderThickness = new Thickness(2); }
                    b.Click += delegate { hat = kind; refreshKinds(); Refresh(); };
                    System.Windows.Automation.AutomationProperties.SetName(b, Hats.Names[kind]);
                    hats.Children.Add(b);
                }
            };
            refreshKinds();
            right.Children.Add(hats);

            right.Children.Add(Ui.Text("Colors", 12, "Muted"));
            colorRows = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
            right.Children.Add(colorRows);

            right.Children.Add(Ui.Text("Picture", 12, "Muted"));
            removePicture = Ui.Btn("Remove picture", delegate { image = null; newPicture = null; Refresh(); }, "BtnGhost");
            var pics = Ui.Row(8, Ui.Btn("Upload picture...", PickPicture), removePicture);
            pics.Margin = new Thickness(0, 6, 0, 4);
            right.Children.Add(pics);
            pictureNote = Ui.Text("", 11.5, "Faint");
            right.Children.Add(pictureNote);
            look.Children.Add(right);
            sp.Children.Add(look);

            // colors are redrawn for the hat kind; keep the swatches in sync with the kind picker
            Action redrawAll = delegate { refreshKinds(); Refresh(); };
            colorsChanged = redrawAll;

            label = Field(sp, "Label", target.label, "What Hatpick calls it, e.g. Work or Personal.");
            name = Field(sp, "Commit name", target.name, "Shown as the author of your commits.");
            email = Field(sp, "Commit email", target.email, "Use the email on this GitHub account (GitHub > Settings > Emails), or its private noreply address.");
            github = Field(sp, "GitHub username", target.github, "Who Git Credential Manager signs in as when you push.");
            var signedIn = gcm.Where(g => !all.Any(a => a != self && string.Equals(a.github, g, StringComparison.OrdinalIgnoreCase))).ToList();
            if (signedIn.Count > 0)
            {
                var chips = new WrapPanel { Margin = new Thickness(0, -4, 0, 10) };
                chips.Children.Add(Gap(Ui.Text("Signed in:", 12, "Muted"), 0, 0));
                foreach (var g in signedIn)
                {
                    var user = g;
                    var c = Ui.Btn(user, delegate { github.Text = user; if (name.Text.Trim().Length == 0) name.Text = user; if (label.Text.Trim().Length == 0) label.Text = user; }, "Btn");
                    c.Height = 26; c.Margin = new Thickness(8, 0, 0, 0);
                    chips.Children.Add(c);
                }
                sp.Children.Add(chips);
            }

            Refresh();
            var ok = Ui.Btn("Save", Save, "BtnPrimary");
            ok.IsDefault = true;
            Layout(new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 640 }, Ui.Btn("Cancel", Close), ok);
            Loaded += delegate { label.Focus(); };
        }

        Action colorsChanged;

        string PicturePath { get { return newPicture ?? (image != null ? System.IO.Path.Combine(Hats.ImageDir, image) : null); } }

        // Redraws the preview, the color rows and the picture note.
        void Refresh()
        {
            string pic = PicturePath;
            FrameworkElement art;
            if (pic != null)
            {
                art = LoadPicture(pic, 80);
                preview.SetResourceReference(Border.BackgroundProperty, "Input");
            }
            else
            {
                art = Hats.Icon(hat, 80, colors);
                string main;
                if (colors.TryGetValue("main", out main))
                {
                    var c = ((SolidColorBrush)new BrushConverter().ConvertFromString(main)).Color;
                    preview.Background = new SolidColorBrush(Color.FromArgb(58, c.R, c.G, c.B));
                }
                else preview.SetResourceReference(Border.BackgroundProperty, Hats.TintKey(hat));
            }
            art.HorizontalAlignment = HorizontalAlignment.Center;
            art.VerticalAlignment = VerticalAlignment.Center;
            preview.Child = art;

            colorRows.Children.Clear();
            colorRows.Opacity = pic != null ? 0.45 : 1;
            colorRows.IsEnabled = pic == null;
            Hats.Part[] parts;
            if (Hats.Parts.TryGetValue(hat, out parts))
                foreach (var p in parts) colorRows.Children.Add(ColorRow(p));

            removePicture.Visibility = pic != null ? Visibility.Visible : Visibility.Collapsed;
            pictureNote.Text = pic != null
                ? "Using " + System.IO.Path.GetFileName(pic) + " instead of a drawn hat. Remove it to use colors again."
                : "PNG, JPG or ICO. Square pictures look best. Used instead of the drawn hat.";
        }

        FrameworkElement ColorRow(Hats.Part part)
        {
            string current = Hats.Color(hat, colors, part.Key);
            bool custom = colors.ContainsKey(part.Key);

            var swatch = new Button { Width = 34, Height = 26, Padding = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "Pick a color for the " + part.Name.ToLower() };
            swatch.Style = Ui.Style("Btn");
            swatch.Content = new Border { Width = 22, Height = 16, CornerRadius = new CornerRadius(4), Background = Brush(current), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), BorderThickness = new Thickness(1) };
            System.Windows.Automation.AutomationProperties.SetName(swatch, part.Name + " color");

            var hex = new TextBox { Text = current, Width = 92, Height = 28, Margin = new Thickness(8, 0, 0, 0), FontFamily = new FontFamily("Cascadia Mono, Consolas"), VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(hex, part.Name + " color, hex");
            Action commitHex = delegate
            {
                string v = NormalizeHex(hex.Text);
                if (v == null) { hex.Text = current; return; }
                if (!string.Equals(v, current, StringComparison.OrdinalIgnoreCase)) { colors[part.Key] = v; colorsChanged(); }
            };
            hex.LostKeyboardFocus += delegate { commitHex(); };
            hex.KeyDown += (s, e) => { if (e.Key == Key.Enter) { commitHex(); e.Handled = true; } };

            var popup = new System.Windows.Controls.Primitives.Popup { PlacementTarget = swatch, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
            var grid = new WrapPanel { Width = 8 * 28 };
            foreach (var c in Palette)
            {
                var col = c;
                var b = new Button { Width = 24, Height = 24, Margin = new Thickness(2), Padding = new Thickness(0), Cursor = Cursors.Hand, ToolTip = col, Style = Ui.Style("Btn") };
                b.Content = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Brush(col) };
                System.Windows.Automation.AutomationProperties.SetName(b, col);
                b.Click += delegate { popup.IsOpen = false; colors[part.Key] = col; colorsChanged(); };
                grid.Children.Add(b);
            }
            var card = new Border { Padding = new Thickness(8), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = grid, Margin = new Thickness(0, 4, 0, 0) };
            card.SetResourceReference(Border.BackgroundProperty, "Panel");
            card.SetResourceReference(Border.BorderBrushProperty, "Border");
            popup.Child = card;
            swatch.Click += delegate { popup.IsOpen = true; };

            var row = Ui.Row(0, Gap(Ui.Text(part.Name, 13, "Sub"), 0, 0), swatch, hex);
            ((FrameworkElement)row.Children[0]).Width = 74;
            ((FrameworkElement)row.Children[0]).VerticalAlignment = VerticalAlignment.Center;
            if (custom)
            {
                var reset = Ui.Btn("Reset", delegate { colors.Remove(part.Key); colorsChanged(); }, "BtnGhost");
                reset.Height = 26; reset.Margin = new Thickness(6, 0, 0, 0);
                reset.ToolTip = "Back to " + Hats.Color(hat, null, part.Key);
                row.Children.Add(reset);
            }
            row.Margin = new Thickness(0, 0, 0, 6);
            row.Children.Add(popup);
            return row;
        }

        static SolidColorBrush Brush(string hex)
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex); }
            catch { return new SolidColorBrush(Colors.Gray); }
        }

        // "#abc", "abc", "#aabbcc" -> "#aabbcc"; anything else -> null
        public static string NormalizeHex(string s)
        {
            s = (s ?? "").Trim().TrimStart('#');
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            if (s.Length != 6) return null;
            foreach (char ch in s) if (!Uri.IsHexDigit(ch)) return null;
            return "#" + s.ToLowerInvariant();
        }

        static FrameworkElement LoadPicture(string path, double size)
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.DecodePixelWidth = (int)(size * 2);
                bmp.EndInit();
                bmp.Freeze();
                return new Image { Source = bmp, Width = size, Height = size, Stretch = Stretch.Uniform };
            }
            catch { return Hats.Icon("none", size); }
        }

        void PickPicture()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pick a picture for this hat",
                Filter = "Pictures (*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif",
            };
            if (dlg.ShowDialog(this) != true) return;
            var info = new System.IO.FileInfo(dlg.FileName);
            if (info.Length > 4 * 1024 * 1024) { Dialogs.Info(this, "Picture too big", "Pick a picture under 4 MB. Hats are drawn small, so 256 x 256 is plenty."); return; }
            newPicture = dlg.FileName;
            Refresh();
        }

        static FrameworkElement Gap(FrameworkElement e, double top, double bottom) { e.Margin = new Thickness(0, top, 0, bottom); return e; }

        TextBox Field(StackPanel sp, string caption, string value, string hint)
        {
            sp.Children.Add(Ui.Text(caption, 12, "Muted"));
            var t = new TextBox { Text = value ?? "", Margin = new Thickness(0, 4, 0, 2), Height = 34 };
            System.Windows.Automation.AutomationProperties.SetName(t, caption);
            sp.Children.Add(t);
            sp.Children.Add(Gap(Ui.Text(hint, 11.5, "Faint"), 0, 12));
            return t;
        }

        void Save()
        {
            string l = label.Text.Trim(), n = name.Text.Trim(), e = email.Text.Trim(), g = github.Text.Trim();
            if (l.Length == 0 || n.Length == 0 || e.Length == 0) { Dialogs.Info(this, "Almost there", "Label, commit name and commit email are all needed."); return; }
            if (all.Any(a => a != self && string.Equals(a.label, l, StringComparison.OrdinalIgnoreCase))) { Dialogs.Info(this, "Name taken", "There's already a hat called '" + l + "'."); return; }
            if (all.Any(a => a != self && string.Equals(a.email, e, StringComparison.OrdinalIgnoreCase))) { Dialogs.Info(this, "Email in use", "Another hat already uses " + e + ". Hatpick tells hats apart by email, so each needs its own."); return; }

            string oldImage = target.image;
            if (newPicture != null)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(Hats.ImageDir);
                    string file = Guid.NewGuid().ToString("N").Substring(0, 12) + System.IO.Path.GetExtension(newPicture).ToLowerInvariant();
                    System.IO.File.Copy(newPicture, System.IO.Path.Combine(Hats.ImageDir, file));
                    image = file;
                }
                catch (Exception ex) { Dialogs.Info(this, "Couldn't save the picture", ex.Message); return; }
            }
            // tidy up a picture this hat no longer uses
            if (!string.IsNullOrEmpty(oldImage) && oldImage != image && !all.Any(a => a != target && a.image == oldImage))
                try { System.IO.File.Delete(System.IO.Path.Combine(Hats.ImageDir, oldImage)); } catch { }

            target.label = l; target.name = n; target.email = e; target.github = g; target.hat = hat;
            target.colors = colors.Count > 0 ? colors : null;
            target.image = image;
            saved = true;
            Close();
        }

        public static bool Edit(Window owner, Account target, List<Account> all, Account self, List<string> gcm)
        {
            var w = new AccountEditor(owner, target, all, self, gcm);
            w.ShowDialog();
            return w.saved;
        }
    }
}
