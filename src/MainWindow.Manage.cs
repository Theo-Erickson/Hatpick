using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Hatpick
{
    // Hats, Folders and Reset pages.
    partial class MainWindow
    {
        // ---------- Hats ----------

        FrameworkElement HatsPage()
        {
            var sp = new StackPanel();
            sp.Children.Add(Header("Hats", "hats",
                Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Add a hat", delegate { AddAccount(); }, "BtnPrimary")));

            if (settings.accounts.Count == 0)
                sp.Children.Add(Ui.Panel(Ui.Stack(8, Ui.Text("No hats yet", 16, "Text", FontWeights.SemiBold),
                    Ui.Text("Add one for each GitHub account you use - for example Work and Personal.", 13.5, "Sub")), 20));

            foreach (var a in settings.accounts) sp.Children.Add(AccountRow(a));

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
                    var who = Ui.Row(10, hat != null ? Hats.Icon(hat.hat, 22) : Ui.Icon("user", 20, "Muted"),
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

        Border AccountRow(Account a)
        {
            int commits = reports.Count(r => string.Equals(r.Email, a.email, StringComparison.OrdinalIgnoreCase));
            int folders = settings.folders.Count(f => f.account == a.label);
            var row = new DockPanel();
            var tile = Hats.Tile(a.hat, 60);
            tile.Margin = new Thickness(0, 0, 16, 0);
            DockPanel.SetDock(tile, Dock.Left);
            row.Children.Add(tile);

            var btns = Ui.Row(6, Ui.Btn("Edit", delegate { EditAccount(a); }), Ui.Btn("Remove", delegate { RemoveAccount(a); }, "BtnGhost"));
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
            var b = Ui.Panel(row, 16);
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
                row.Children.Add(Ui.Row(10, Ui.Icon("repos", 18, "Muted"), Ui.Stack(1, Ui.Text(Path.GetFileName(p.TrimEnd('\\')), 14, "Text", FontWeights.SemiBold), Ui.MonoText(p, 11.5, "Muted"))));
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
                    Content = Ui.Stack(1, Ui.Text(Path.GetFileName(p), 14, "Text", FontWeights.SemiBold), Ui.MonoText(p, 11.5, "Muted")) };
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
                    catch (Exception e) { errors.Add(Path.GetFileName(p) + ": " + e.Message); }
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
