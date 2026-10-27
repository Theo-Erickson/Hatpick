using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Hatpick
{
    // Hats, Folders and Reset pages.
    partial class MainWindow
    {
        // ---------- Hats ----------

        string selectedHat;   // label of the hat picked on the rack

        FrameworkElement HatsPage()
        {
            var sp = new StackPanel();
            sp.Children.Add(Header("Hats", "hats",
                Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Add a hat", delegate { AddAccount(); }, "BtnPrimary")));

            var rack = HatRack();
            sp.Children.Add(rack);
            Marks["rack"] = rack;

            var sel = settings.AccountByLabel(selectedHat) ?? settings.accounts.FirstOrDefault();
            if (sel != null)
            {
                selectedHat = sel.label;
                var row = AccountRow(sel);
                row.Margin = new Thickness(0, 14, 0, 0);
                sp.Children.Add(row);
            }
            else
                sp.Children.Add(Ui.Panel(Ui.Stack(8, Ui.Text("No hats yet", 16, "Text", FontWeights.SemiBold),
                    Ui.Text("Add one for each GitHub account you use - for example Work and Personal. Each gets a hat on the rack.", 13.5, "Sub")), 20));

            // Git Credential Manager
            var gcmBox = new StackPanel();
            gcmBox.Children.Add(Ui.TitleWithHelp(Ui.Text("Signed in to Git Credential Manager", 15, "Text", FontWeights.SemiBold), "signin"));
            if (!Git.GcmAvailable)
                gcmBox.Children.Add(Ui.Text("Git Credential Manager wasn't found. It comes with Git for Windows - install or update it from git-scm.com.", 13, "Sub"));
            else
            {
                if (gcm.Count == 0) gcmBox.Children.Add(Ui.Text("No GitHub accounts are signed in yet.", 13, "Sub"));
                foreach (var g in gcm)
                {
                    var user = g;
                    var hat = settings.AccountByGitHub(g);
                    var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
                    if (hat == null)
                    {
                        var make = Ui.Btn("Make a hat", delegate { AddAccount(user); });
                        DockPanel.SetDock(make, Dock.Right);
                        row.Children.Add(make);
                    }
                    var who = Ui.Row(10, hat != null ? Hats.For(hat, 22) : Ui.Icon("user", 20, "Muted"),
                        Ui.MonoText(user, 13, "Text"), Ui.Text(hat != null ? "worn as " + hat.label : "not a hat yet", 12, "Muted"));
                    row.Children.Add(who);
                    gcmBox.Children.Add(row);
                }
            }
            var signIn = Ui.Btn("Sign in another account...", SignInAccount);
            signIn.HorizontalAlignment = HorizontalAlignment.Left;
            signIn.Margin = new Thickness(0, 14, 0, 0);
            gcmBox.Children.Add(signIn);
            gcmBox.Children.Add(Ui.Text("Hatpick only reads account names. Your sign-ins stay in Git Credential Manager.", 12, "Faint"));
            var gcmPanel = Ui.Panel(gcmBox, 18);
            gcmPanel.Margin = new Thickness(0, 14, 0, 0);
            sp.Children.Add(gcmPanel);
            return PageScroll(sp);
        }

        // A wall-mounted hat rack: one peg per hat, plus an empty peg for adding one.
        // It grows with your hats and scrolls sideways when it runs out of room.
        Border HatRack()
        {
            const double Slot = 132;
            var g = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 6, 10, 4) };
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });   // hats
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });    // pegs
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });    // the bar
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // labels
            int n = settings.accounts.Count + 1;
            for (int i = 0; i < n; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Slot) });

            var wood = new LinearGradientBrush(Color.FromRgb(0xa8, 0x76, 0x44), Color.FromRgb(0x6b, 0x44, 0x23), 90);
            var bar = new Border { CornerRadius = new CornerRadius(9), Background = wood, Margin = new Thickness(-8, 0, -8, 0) };
            bar.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 3, Opacity = 0.35 };
            Grid.SetRow(bar, 2); Grid.SetColumnSpan(bar, n);
            g.Children.Add(bar);
            foreach (var end in new[] { HorizontalAlignment.Left, HorizontalAlignment.Right })   // mounting screws
            {
                var screw = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(0xd8, 0xc0, 0x9a)), HorizontalAlignment = end, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) };
                Grid.SetRow(screw, 2); Grid.SetColumnSpan(screw, n);
                g.Children.Add(screw);
            }

            for (int i = 0; i < n; i++)
            {
                bool isAdd = i == settings.accounts.Count;
                var a = isAdd ? null : settings.accounts[i];
                bool on = a != null && a.label == selectedHat || a != null && selectedHat == null && i == 0;

                var peg = new Border { Width = 12, CornerRadius = new CornerRadius(3, 3, 0, 0), Background = wood, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetRow(peg, 1); Grid.SetColumn(peg, i);
                g.Children.Add(peg);

                var hatBtn = new Button { Style = Ui.Style("Row"), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, MinHeight = 0 };
                if (isAdd)
                {
                    var plus = new Grid { Width = 72, Height = 72 };
                    var ring = new Ellipse { StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 3, 3 } };
                    ring.SetResourceReference(Shape.StrokeProperty, "Dashed");
                    plus.Children.Add(ring);
                    var ic = Ui.Icon("plus", 26, "Muted");
                    ic.HorizontalAlignment = HorizontalAlignment.Center;
                    plus.Children.Add(ic);
                    hatBtn.Content = plus;
                    hatBtn.ToolTip = "Add a hat";
                    hatBtn.Click += delegate { AddAccount(); };
                }
                else
                {
                    var acct = a;
                    var hat = Hats.For(a, 80);
                    hatBtn.Content = hat;
                    hatBtn.ToolTip = a.label + "  -  " + a.email + "\nClick to see it, double-click to edit";
                    if (on)
                    {
                        hatBtn.RenderTransform = new TranslateTransform(0, -6);   // the picked hat lifts off its peg a little
                        hat.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 6, Opacity = 0.45 };
                    }
                    hatBtn.Click += delegate { selectedHat = acct.label; Render(); };
                    hatBtn.MouseDoubleClick += delegate { EditAccount(acct); };
                }
                System.Windows.Automation.AutomationProperties.SetName(hatBtn, isAdd ? "Add a hat" : a.label + " hat");
                Grid.SetRow(hatBtn, 0); Grid.SetColumn(hatBtn, i);
                g.Children.Add(hatBtn);

                int count = a == null ? 0 : reports.Count(r => string.Equals(r.Email, a.email, StringComparison.OrdinalIgnoreCase));
                var label = Ui.Stack(1, Ui.Text(isAdd ? "Add a hat" : a.label, 13.5, on ? "Accent" : isAdd ? "Muted" : "Text", on ? FontWeights.Bold : FontWeights.SemiBold),
                    Ui.Text(isAdd ? " " : count + (count == 1 ? " repo" : " repos"), 11.5, "Muted"));
                foreach (TextBlock t in label.Children) { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; }
                label.Margin = new Thickness(4, 8, 4, 0);
                Grid.SetRow(label, 3); Grid.SetColumn(label, i);
                g.Children.Add(label);
            }

            var scroll = new ScrollViewer { Content = g, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 0, 4) };
            var title = Ui.TitleWithHelp(Ui.Text("Your hat rack", 15, "Text", FontWeights.SemiBold), "hats");
            var hint = Ui.Text("Click a hat to see it below, double-click to edit it.", 12, "Muted");
            hint.Margin = new Thickness(0, 2, 0, 8);
            return Ui.Panel(Ui.Stack(0, title, hint, scroll), 18);
        }

        Border AccountRow(Account a)
        {
            int commits = reports.Count(r => string.Equals(r.Email, a.email, StringComparison.OrdinalIgnoreCase));
            int folders = settings.folders.Count(f => f.account == a.label);
            var row = new DockPanel();
            var tile = Hats.TileFor(a, 60);
            tile.Margin = new Thickness(0, 0, 16, 0);
            DockPanel.SetDock(tile, Dock.Left);
            row.Children.Add(tile);

            var btns = Ui.Row(6, Ui.Btn("Edit hat", delegate { EditAccount(a); }, "BtnPrimary"), Ui.Btn("Remove", delegate { RemoveAccount(a); }, "BtnGhost"));
            btns.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(btns, Dock.Right);
            row.Children.Add(btns);

            bool signed = gcm.Any(g => string.Equals(g, a.github, StringComparison.OrdinalIgnoreCase));
            var info = Ui.Stack(3,
                Ui.Text(a.label, 18, "Text", FontWeights.SemiBold),
                Ui.MonoText(a.name + " <" + a.email + ">", 12.5, "Sub"),
                Ui.Text((string.IsNullOrEmpty(a.github) ? "No GitHub username" : "GitHub: " + a.github + (Git.GcmAvailable ? (signed ? "  ·  signed in" : "  ·  not signed in yet") : "")) +
                        "  ·  " + commits + " repo(s)" + (folders > 0 ? "  ·  default for " + folders + " folder(s)" : ""), 12, "Muted"));
            info.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(info);
            var b = Ui.Panel(row, 16, "Panel", "Accent");
            b.Margin = new Thickness(0, 0, 0, 10);
            return b;
        }

        // ---------- Folders ----------

        FrameworkElement FoldersPage()
        {
            var sp = new StackPanel();
            sp.Children.Add(Header("Folders", "folders",
                Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Watch a folder", PickAndWatch, "BtnPrimary"),
                Ui.Btn("Add a single repo", PickSingle),
                Ui.Btn("Analyze a folder", delegate { var p = FolderPicker.Pick(this, "Pick a folder to analyze"); if (p != null) OpenPath(p); })));

            var explain = Ui.Panel(Ui.Stack(6,
                Ui.Text("Watched folders vs. single repos", 14, "Text", FontWeights.SemiBold),
                Ui.Text("Watch a folder when it holds several repos, like Documents\\GitHub. Every repo inside is listed, and the folder's default hat applies to all of them - including repos you clone there later. " +
                        "Add a single repo when it lives somewhere else; you pick its hat directly. A repo's own setting always beats the folder default.", 13, "Sub")), 16, "InfoBg", "InfoBorder", 10);
            explain.Margin = new Thickness(0, 0, 0, 20);
            sp.Children.Add(explain);

            sp.Children.Add(Ui.SectionTitle("Watched folders"));
            var watched = new StackPanel { Margin = new Thickness(0, 10, 0, 22) };
            if (settings.folders.Count == 0) watched.Children.Add(Ui.Text("None yet.", 13, "Muted"));
            foreach (var f in settings.folders) watched.Children.Add(FolderRow(f));
            sp.Children.Add(watched);

            sp.Children.Add(Ui.SectionTitle("Single repos"));
            var singles = new StackPanel { Margin = new Thickness(0, 10, 0, 22) };
            if (settings.repos.Count == 0) singles.Children.Add(Ui.Text("None yet.", 13, "Muted"));
            foreach (var p in settings.repos)
            {
                var r = Find(p);
                var row = new DockPanel();
                var remove = Ui.Btn("Remove", delegate { if (r != null) RemoveSingle(r); else { settings.repos.RemoveAll(x => Settings.SamePath(x, p)); settings.Save(); Rescan(); } }, "BtnGhost");
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(remove);
                row.Children.Add(Ui.Row(10, Ui.Icon("repos", 18, "Muted"), Ui.Stack(1, Ui.Text(System.IO.Path.GetFileName(p.TrimEnd('\\')), 14, "Text", FontWeights.SemiBold), Ui.MonoText(p, 11.5, "Muted"))));
                var b = Ui.Panel(row, 12);
                b.Margin = new Thickness(0, 0, 0, 8);
                singles.Children.Add(b);
            }
            sp.Children.Add(singles);

            var explorer = new CheckBox { Content = "Add \"Analyze with Hatpick\" to Explorer's right-click menu for folders", IsChecked = ExplorerMenu.Installed };
            explorer.Click += delegate
            {
                try { if (explorer.IsChecked == true) ExplorerMenu.Install(Program.ExePath); else ExplorerMenu.Remove(); }
                catch (Exception e) { Dialogs.Info(this, "Couldn't change the right-click menu", e.Message); }
                explorer.IsChecked = ExplorerMenu.Installed;
            };
            sp.Children.Add(Ui.Panel(Ui.Stack(6, explorer, Ui.Text("Per-user, no admin rights needed. On Windows 11 it's under \"Show more options\". You can also drop any folder onto this window.", 12, "Faint")), 16));
            return PageScroll(sp);
        }

        Border FolderRow(WatchedFolder f)
        {
            int count = reports.Count(r => r.Source == RepoSource.Watched && Settings.SamePath(r.WatchedFolder, f.path));
            var row = new DockPanel();
            var picker = Ui.Picker(HatChoices("No default hat", false), f.account ?? "", v =>
            {
                f.account = v;
                settings.Save();
                SyncFolders();
                Rescan();
            }, 230);
            var right = Ui.Row(8, picker, Ui.Btn("Stop watching", delegate
            {
                if (!Dialogs.Ask(this, "Stop watching?", "Its repos leave the list and its default hat stops applying. Hats you set on single repos inside it stay.", "Stop watching")) return;
                settings.folders.Remove(f);
                settings.Save();
                SyncFolders();
                Rescan();
            }, "BtnGhost"));
            right.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);
            row.Children.Add(Ui.Row(10, Ui.Icon("folder", 20, "Muted"),
                Ui.Stack(1, Ui.MonoText(f.path, 13, "Text"), Ui.Text(count + " repo(s)  ·  default hat applies to new repos too", 12, "Muted"))));
            var b = Ui.Panel(row, 14);
            b.Margin = new Thickness(0, 0, 0, 8);
            return b;
        }

        // ---------- Reset ----------

        FrameworkElement ResetPage()
        {
            var sp = new StackPanel();
            sp.Children.Add(Header("Reset", "reset"));
            var explain = Ui.Panel(Ui.Text("Undo what Hatpick changed. Each checked repo goes back to exactly what it had before Hatpick first changed it. " +
                "Your commits, branches and Git Credential Manager sign-ins aren't touched. (To undo a commit rewrite, use \"Undo last rewrite\" on that repo's page.)", 13, "Sub"),
                16, "InfoBg", "InfoBorder", 10);
            explain.Margin = new Thickness(0, 0, 0, 20);
            sp.Children.Add(explain);

            var managed = settings.managed.Concat(reports.Where(r => r.Managed).Select(r => r.Path))
                .Select(Settings.Norm).Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).OrderBy(p => p).ToList();
            var boxes = new List<CheckBox>();
            sp.Children.Add(Ui.SectionTitle("Repos Hatpick changed"));
            var list = new StackPanel { Margin = new Thickness(0, 10, 0, 20) };
            if (managed.Count == 0) list.Children.Add(Ui.Text("None - Hatpick hasn't changed any repos.", 13, "Muted"));
            foreach (var p in managed)
            {
                var cb = new CheckBox { IsChecked = true, Tag = p, Margin = new Thickness(0, 0, 0, 10),
                    Content = Ui.Stack(1, Ui.Text(System.IO.Path.GetFileName(p), 14, "Text", FontWeights.SemiBold), Ui.MonoText(p, 11.5, "Muted")) };
                boxes.Add(cb);
                list.Children.Add(cb);
            }
            sp.Children.Add(list);

            int defaults = settings.folders.Count(f => settings.AccountByLabel(f.account) != null);
            var folderBox = new CheckBox { IsChecked = defaults > 0, IsEnabled = defaults > 0, Margin = new Thickness(0, 0, 0, 20),
                Content = Ui.Text(defaults > 0 ? "Also remove the " + defaults + " folder default(s) from your global .gitconfig" : "No folder defaults to remove", 13.5, "Text") };
            sp.Children.Add(folderBox);

            var go = Ui.Btn("Reset selected", delegate
            {
                var chosen = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToList();
                bool folders = folderBox.IsChecked == true && folderBox.IsEnabled;
                if (chosen.Count == 0 && !folders) return;
                if (!Dialogs.Ask(this, "Reset?", "Reset " + chosen.Count + " repo(s)" + (folders ? " and remove " + defaults + " folder default(s)" : "") + "?", "Reset")) return;
                var errors = new List<string>();
                foreach (var p in chosen)
                {
                    try { Git.Reset(p); settings.UnmarkManaged(p); }
                    catch (Exception e) { errors.Add(System.IO.Path.GetFileName(p) + ": " + e.Message); }
                }
                settings.managed.RemoveAll(m => !Directory.Exists(m));
                if (folders)
                {
                    foreach (var w in settings.folders) w.account = "";
                    try
                    {
                        Git.RemoveFolderDefaults();
                        if (Directory.Exists(Git.IncludeDir)) foreach (var file in Directory.GetFiles(Git.IncludeDir, "*.gitconfig")) File.Delete(file);
                    }
                    catch (Exception e) { errors.Add("Folder defaults: " + e.Message); }
                }
                settings.Save();
                Dialogs.Info(this, "Reset done", errors.Count == 0 ? "Everything you picked is back to how it was." : "Some didn't work:\n" + string.Join("\n", errors));
                Rescan();
            }, "BtnPrimary");
            go.HorizontalAlignment = HorizontalAlignment.Left;
            go.Height = 36; go.Padding = new Thickness(18, 0, 18, 0);
            sp.Children.Add(go);
            return PageScroll(sp);
        }
    }
}
