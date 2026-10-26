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

    // Add or edit a hat (account).
    class AccountEditor : ThemedWindow
    {
        readonly Account target;
        readonly List<Account> all;
        readonly Account self;
        TextBox label, name, email, github;
        string hat;
        bool saved;

        AccountEditor(Window owner, Account target, List<Account> all, Account self, List<string> gcm)
            : base(owner, self == null ? "Add a hat" : "Edit hat", 560)
        {
            this.target = target; this.all = all; this.self = self;
            hat = string.IsNullOrEmpty(target.hat) ? HatKinds.DefaultFor(all.Count) : target.hat;

            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(self == null ? "Add a hat" : "Edit " + target.label, 20, "Text", FontWeights.SemiBold));
            sp.Children.Add(Gap(Ui.Text("A hat is one of your accounts: who you commit as, and who you push as.", 13, "Muted"), 4, 14));

            // hat picture
            sp.Children.Add(Ui.Text("Hat", 12, "Muted"));
            var hats = new WrapPanel { Margin = new Thickness(0, 6, 0, 14) };
            Action refresh = null;
            refresh = delegate
            {
                hats.Children.Clear();
                foreach (var k in HatKinds.All)
                {
                    var kind = k;
                    var b = new Button { Style = Ui.Style("Card"), Width = 64, Height = 64, Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0), ToolTip = Hats.Names[kind] };
                    b.Content = Hats.Icon(kind, 40);
                    if (kind == hat) { b.SetResourceReference(BorderBrushProperty, "Accent"); b.BorderThickness = new Thickness(2); }
                    b.Click += delegate { hat = kind; refresh(); };
                    System.Windows.Automation.AutomationProperties.SetName(b, Hats.Names[kind]);
                    hats.Children.Add(b);
                }
            };
            refresh();
            sp.Children.Add(hats);

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

            var ok = Ui.Btn("Save", Save, "BtnPrimary");
            ok.IsDefault = true;
            Layout(sp, Ui.Btn("Cancel", Close), ok);
            Loaded += delegate { label.Focus(); };
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
            target.label = l; target.name = n; target.email = e; target.github = g; target.hat = hat;
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
