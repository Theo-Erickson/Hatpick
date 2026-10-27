using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Hatpick
{
    // Overview: your hats, what needs attention, and every repo as a card showing the
    // hat it commits with and the hat it pushes with.
    partial class MainWindow
    {
        FrameworkElement OverviewPage()
        {
            var sp = new StackPanel();
            var searchBox = new TextBox { Width = 240, Height = 32, Text = search, ToolTip = "Search repos" };
            System.Windows.Automation.AutomationProperties.SetName(searchBox, "Search repos");
            var cardsHost = new StackPanel();
            searchBox.TextChanged += delegate { search = searchBox.Text; FillRepoGroups(cardsHost); };
            Marks["search"] = searchBox;
            sp.Children.Add(Header("Overview", "overview",
                searchBox,
                Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Watch folder", PickAndWatch),
                Ui.IconBtn(Ui.Icon("rescan", 14, "Text"), "Rescan", Rescan)));

            // your hats
            var hatsTitle = Ui.TitleWithHelp(Ui.SectionTitle("Your hats"), "hats");
            sp.Children.Add(hatsTitle);
            var hatGrid = Ui.Cards(260);
            hatGrid.Margin = new Thickness(0, 10, 0, 26);
            foreach (var a in settings.accounts) hatGrid.Children.Add(HatCard(a));
            var add = new Button { Style = Ui.Style("Card"), MinHeight = 82, Margin = new Thickness(0, 0, 12, 12) };
            add.SetResourceReference(Control.BorderBrushProperty, "Dashed");
            add.Background = System.Windows.Media.Brushes.Transparent;
            add.Content = CenterRow(Ui.Icon("plus", 16, "Muted"), Ui.Text("Add a hat", 13, "Muted"));
            add.Click += delegate { AddAccount(); };
            hatGrid.Children.Add(add);
            sp.Children.Add(hatGrid);
            Marks["hats"] = hatGrid;

            if (!scannedOnce) { sp.Children.Add(Ui.Text("Looking for repos...", 14, "Muted")); return PageScroll(sp); }

            // needs attention
            var attention = reports.Where(r => r.Level >= Level.Warn).OrderByDescending(r => r.Level).ToList();
            if (attention.Count > 0)
            {
                sp.Children.Add(Ui.TitleWithHelp(Ui.SectionTitle("Needs attention"), "attention"));
                var list = new StackPanel { Margin = new Thickness(0, 10, 0, 26) };
                foreach (var r in attention) list.Children.Add(AttentionRow(r));
                sp.Children.Add(list);
                Marks["attention"] = list;
            }

            if (reports.Count == 0)
            {
                var empty = Ui.Panel(Ui.Stack(10,
                    Ui.Text("No repos yet", 16, "Text", FontWeights.SemiBold),
                    Ui.Text("Watch the folder where you keep your repos (GitHub Desktop uses Documents\\GitHub), or drop any repo folder onto this window.", 13.5, "Sub"),
                    Ui.Row(8, Ui.Btn("Watch a folder", PickAndWatch, "BtnPrimary"), Ui.Btn("Add a single repo", PickSingle))), 22);
                sp.Children.Add(empty);
                return PageScroll(sp);
            }

            FillRepoGroups(cardsHost);
            sp.Children.Add(cardsHost);
            Marks["repos"] = cardsHost;

            var footer = Ui.Text(reports.Count + " repos  ·  " + GcmSummary(), 12, "Muted");
            footer.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(footer);
            return PageScroll(sp);
        }

        string GcmSummary()
        {
            if (!Git.GcmAvailable) return "Git Credential Manager not found";
            if (gcm.Count == 0) return "Git Credential Manager: no GitHub accounts signed in";
            return "Git Credential Manager: " + string.Join(", ", gcm) + " signed in";
        }

        static StackPanel CenterRow(params UIElement[] items)
        {
            var r = Ui.Row(8, items);
            r.HorizontalAlignment = HorizontalAlignment.Center;
            r.VerticalAlignment = VerticalAlignment.Center;
            return r;
        }

        Button HatCard(Account a)
        {
            int count = reports.Count(r => string.Equals(r.Email, a.email, StringComparison.OrdinalIgnoreCase));
            var g = new DockPanel();
            var tile = Hats.TileFor(a, 52);
            tile.Margin = new Thickness(0, 0, 14, 0);
            DockPanel.SetDock(tile, Dock.Left);
            g.Children.Add(tile);
            var num = Ui.Stack(0, Ui.Text(count.ToString(), 18, "Text", FontWeights.SemiBold), Ui.Text(count == 1 ? "repo" : "repos", 12, "Muted"));
            num.VerticalAlignment = VerticalAlignment.Center;
            ((TextBlock)num.Children[0]).HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(num, Dock.Right);
            g.Children.Add(num);
            var who = Ui.Stack(2, Ui.Text(a.label, 16, "Text", FontWeights.SemiBold), Ui.MonoText(string.IsNullOrEmpty(a.github) ? a.email : a.github, 12, "Muted"));
            who.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(who);
            var b = new Button { Style = Ui.Style("Card"), Content = g, Margin = new Thickness(0, 0, 12, 12) };
            b.Click += delegate { Navigate("hats"); };
            return b;
        }

        Border AttentionRow(RepoReport r)
        {
            var issue = r.Issues.OrderByDescending(i => i.Level).First();
            bool err = r.Level == Level.Error;
            var row = new DockPanel();
            var icon = Ui.StatusIcon(r.Level, 18);
            icon.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var fix = QuickFix(r);
            if (fix != null) actions.Children.Add(fix);
            var details = Ui.Btn("Details", delegate { Navigate("repos", r.Path); }, "BtnLink");
            details.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(details);
            DockPanel.SetDock(actions, Dock.Right);
            row.Children.Add(actions);

            var name = Ui.Text(r.DisplayName, 14, "Text", FontWeights.SemiBold);
            var msg = Ui.Text(issue.Text, 13, err ? "ErrText" : "WarnText");
            var texts = Ui.Stack(2, name, msg);
            texts.Margin = new Thickness(0, 0, 12, 0);
            row.Children.Add(texts);

            var b = Ui.Panel(row, 12, err ? "ErrBg" : "WarnBg", err ? "ErrBorder" : "WarnBorder", 10);
            b.Margin = new Thickness(0, 0, 0, 8);
            return b;
        }

        // The one-click fix for the most common problems.
        Button QuickFix(RepoReport r)
        {
            var commit = CommitHat(r);
            bool pushMatters = r.RemoteUrl.Length == 0 || r.IsGitHubHttps;
            if (r.Error != null) return null;
            if (r.Email.Length == 0 || r.Name.Length == 0)
            {
                Button pick = null;
                pick = Ui.Btn("Pick a hat...", delegate { PickHatFor(r, pick); });
                return pick;
            }
            if (commit != null && pushMatters && !string.IsNullOrEmpty(commit.github) &&
                !string.Equals(commit.github, r.PushUser, StringComparison.OrdinalIgnoreCase) && r.UrlUser.Length == 0)
                return Ui.Btn("Wear " + commit.label + " for both", delegate { Apply(r, r.LocalIdentity ? commit : null, commit.github); }, "BtnPrimary");
            return null;
        }

        void PickHatFor(RepoReport r, Button anchor)
        {
            var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4 };
            var list = new StackPanel();
            foreach (var a in settings.accounts)
            {
                var acct = a;
                var row = Ui.Row(10, Hats.For(a, 22), Ui.Stack(0, Ui.Text(a.label, 13, "Text", FontWeights.SemiBold), Ui.Text(a.email, 11.5, "Muted")));
                var b = new Button { Style = Ui.Style("Row"), Content = row, MinHeight = 40 };
                b.Click += delegate { popup.IsOpen = false; Apply(r, acct, acct.github); };
                list.Children.Add(b);
            }
            if (settings.accounts.Count == 0) list.Children.Add(Ui.Btn("Add a hat first", delegate { popup.IsOpen = false; AddAccount(); }, "BtnLink"));
            var card = Ui.Panel(list, 6);
            card.MinWidth = 220;
            card.Margin = new Thickness(0, 0, 12, 12);
            popup.Child = card;
            popup.IsOpen = true;
        }

        void FillRepoGroups(StackPanel into)
        {
            into.Children.Clear();
            string q = search.Trim();
            Func<RepoReport, bool> match = r => q.Length == 0 || r.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                HatLabel(r).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || r.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

            foreach (var f in settings.folders)
            {
                var folder = f;
                var items = reports.Where(r => r.Source == RepoSource.Watched && Settings.SamePath(r.WatchedFolder, folder.path) && match(r)).ToList();
                var def = settings.AccountByLabel(f.account);
                AddGroup(into, f.path, def != null ? "wears " + def.label + " by default" : "no default hat", items);
            }
            AddGroup(into, "Single repos", "set one at a time", reports.Where(r => r.Source == RepoSource.Single && match(r)).ToList());
            AddGroup(into, "Analyzed, not in your list", "dropped or opened from Explorer", reports.Where(r => r.Source == RepoSource.Opened && match(r)).ToList());
            if (into.Children.Count == 0 && q.Length > 0) into.Children.Add(Ui.Text("No repos match \"" + q + "\".", 13, "Muted"));
        }

        void AddGroup(StackPanel into, string title, string note, List<RepoReport> items)
        {
            if (items.Count == 0) return;
            var head = Ui.Row(10, Ui.SectionTitle(title), Ui.Text(note, 12, "Faint"));
            head.Margin = new Thickness(0, 0, 0, 10);
            into.Children.Add(head);
            var grid = Ui.Cards(280);
            grid.Margin = new Thickness(0, 0, 0, 18);
            foreach (var r in items) grid.Children.Add(RepoCard(r));
            into.Children.Add(grid);
        }

        Button RepoCard(RepoReport r)
        {
            var sp = new StackPanel();
            var top = new DockPanel();
            var chip = StatusChip(r);
            DockPanel.SetDock(chip, Dock.Right);
            top.Children.Add(chip);
            var name = Ui.Text(r.DisplayName, 15, "Text", FontWeights.SemiBold);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            top.Children.Add(name);
            sp.Children.Add(top);

            var hats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            hats.Children.Add(CommitHatIcon(r, 22));
            hats.Children.Add(Pad(Ui.Text("commits", 12, "Muted"), 6, 14));
            if (r.IsSsh) hats.Children.Add(Ui.Icon("key", 20, "Muted"));
            else hats.Children.Add(PushHatIcon(r, 22));
            hats.Children.Add(Pad(Ui.Text(r.IsSsh ? "pushes with SSH key" : "pushes", 12, "Muted"), 6, 0));
            sp.Children.Add(hats);

            var note = Ui.Text(CardNote(r), 12, "Faint");
            note.Margin = new Thickness(0, 8, 0, 0);
            note.TextTrimming = TextTrimming.CharacterEllipsis;
            sp.Children.Add(note);

            var b = new Button { Style = Ui.Style("Card"), Content = sp, Margin = new Thickness(0, 0, 12, 12), ToolTip = r.Path };
            if (r.Level == Level.Warn) b.SetResourceReference(Control.BorderBrushProperty, "Accent");
            b.Click += delegate { Navigate("repos", r.Path); };
            return b;
        }

        static FrameworkElement Pad(FrameworkElement e, double left, double right)
        {
            e.Margin = new Thickness(left, 0, right, 0);
            e.VerticalAlignment = VerticalAlignment.Center;
            return e;
        }

        // The hat a repo commits / pushes with. An account Hatpick doesn't know shows as a person.
        FrameworkElement CommitHatIcon(RepoReport r, double size)
        {
            if (r.Error != null || r.Email.Length == 0) return Hats.Icon("none", size);
            var a = CommitHat(r);
            return a != null ? Hats.For(a, size) : Ui.Icon("user", size, "Muted");
        }

        FrameworkElement PushHatIcon(RepoReport r, double size)
        {
            if (r.PushUser.Length == 0) return Hats.Icon("none", size);
            var a = PushHat(r);
            return a != null ? Hats.For(a, size) : Ui.Icon("user", size, "Muted");
        }

        string CardNote(RepoReport r)
        {
            if (r.Error != null) return r.Error;
            if (r.Email.Length == 0) return "Commits will fail until it wears a hat";
            if (r.IsSsh) return "Your SSH key decides who pushes";
            if (r.LocalIdentity || r.LocalPush) return "Set on this repo";
            if (r.IdentitySetBy == "Folder default") return "From the folder default";
            return "From your global settings";
        }

        Border StatusChip(RepoReport r)
        {
            string text, bg, fg;
            var commit = CommitHat(r);
            var push = PushHat(r);
            if (r.Level == Level.Error) { text = r.Error != null ? "Error" : "No hat"; bg = "ErrBg"; fg = "ErrText"; }
            else if (r.Level == Level.Warn)
            {
                bool two = commit != null && push != null && commit != push;
                text = two ? "Two hats" : "Check"; bg = "WarnBg"; fg = "WarnText";
            }
            else if (r.Level == Level.Info) { text = r.IsSsh ? "SSH remote" : "Notes"; bg = "InfoBg"; fg = "InfoText"; }
            else { text = "All set"; bg = "OkBg"; fg = "OkText"; }
            var t = Ui.Text(text, 11, fg);
            t.TextWrapping = TextWrapping.NoWrap;
            var b = new Border { Child = t, Padding = new Thickness(8, 2, 8, 3), CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            b.SetResourceReference(Border.BackgroundProperty, bg);
            return b;
        }
    }
}
