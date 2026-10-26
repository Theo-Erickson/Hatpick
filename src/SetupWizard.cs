using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Hatpick
{
    // First-time setup: what Hatpick is, signing in to Git Credential Manager, your hats,
    // and your repo folders. Can be run again from Help.
    class SetupWizard : ThemedWindow
    {
        readonly Settings settings;
        readonly List<Func<FrameworkElement>> pages = new List<Func<FrameworkElement>>();
        int page;
        Border host;
        StackPanel dots;
        Button back, next, skip;
        List<string> gcm = new List<string>();
        CheckBox tour, explorer;
        bool finished;

        SetupWizard(Window owner, Settings settings) : base(owner, "Set up Hatpick", 720)
        {
            this.settings = settings;
            SizeToContent = SizeToContent.Manual;
            Height = 600;
            gcm = Git.CredentialManagerAccounts();

            var root = new DockPanel();
            var bar = new DockPanel { Margin = new Thickness(28, 12, 28, 20) };
            dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(dots, Dock.Left);
            bar.Children.Add(dots);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            skip = Ui.Btn("Skip setup", Close, "BtnGhost");
            back = Ui.Btn("Back", delegate { Go(page - 1); });
            next = Ui.Btn("Next", delegate { if (page == pages.Count - 1) Finish(); else Go(page + 1); }, "BtnPrimary");
            next.IsDefault = true;
            foreach (var b in new[] { skip, back, next }) { b.Margin = new Thickness(8, 0, 0, 0); b.MinWidth = 90; b.Height = 34; btns.Children.Add(b); }
            bar.Children.Add(btns);
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);
            host = new Border { Padding = new Thickness(32, 26, 32, 8) };
            root.Children.Add(new ScrollViewer { Content = host, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;

            pages.Add(Welcome);
            pages.Add(SignIn);
            pages.Add(HatsStep);
            pages.Add(FoldersStep);
            pages.Add(Done);
            Go(0);
        }

        void Go(int i)
        {
            page = Math.Max(0, Math.Min(i, pages.Count - 1));
            host.Child = pages[page]();
            dots.Children.Clear();
            for (int d = 0; d < pages.Count; d++)
            {
                var e = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0) };
                e.SetResourceReference(Shape.FillProperty, d == page ? "Accent" : "Border");
                dots.Children.Add(e);
            }
            back.IsEnabled = page > 0;
            next.Content = page == pages.Count - 1 ? "Finish" : "Next";
            skip.Visibility = page == pages.Count - 1 ? Visibility.Collapsed : Visibility.Visible;
            next.Focus();
        }

        static StackPanel Page(string title, string intro)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(title, 24, "Text", FontWeights.Bold));
            if (intro != null) { var t = Ui.Text(intro, 14, "Sub"); t.Margin = new Thickness(0, 8, 0, 18); sp.Children.Add(t); }
            return sp;
        }

        FrameworkElement Welcome()
        {
            var sp = Page("Welcome to Hatpick", "Each of your GitHub accounts is a hat. Hatpick makes sure every repo wears the right one - for the name on your commits, and for the account you push with.");
            var hats = Ui.Row(14, Hats.Tile("hardhat", 64), Hats.Tile("beanie", 64), Hats.Tile("tophat", 64));
            hats.Margin = new Thickness(0, 0, 0, 20);
            sp.Children.Add(hats);
            sp.Children.Add(Ui.Text("In the next steps you'll:", 14, "Text", FontWeights.SemiBold));
            foreach (var line in new[] { "Check which GitHub accounts are signed in to Git Credential Manager", "Make a hat for each account", "Pick the folders where you keep repos, and a default hat for each" })
            {
                var t = Ui.Row(10, Ui.Icon("check", 16, "Accent", 2.2), Ui.Text(line, 14, "Sub"));
                t.Margin = new Thickness(0, 8, 0, 0);
                sp.Children.Add(t);
            }
            var safe = Ui.Panel(Ui.Text("Hatpick never signs in and never sees a password or token. Your sign-ins stay in Git Credential Manager (part of Git for Windows); " +
                "Hatpick only writes plain git settings that say which one to use, and everything it changes can be reset.", 13, "InfoText"), 14, "InfoBg", "InfoBorder", 10);
            safe.Margin = new Thickness(0, 20, 0, 0);
            sp.Children.Add(safe);
            return sp;
        }

        FrameworkElement SignIn()
        {
            var sp = Page("Sign in your accounts", "Git Credential Manager keeps your GitHub sign-ins. Sign each account in once and it remembers it.");
            if (!Git.GcmAvailable)
            {
                sp.Children.Add(Ui.Panel(Ui.Text("Git Credential Manager wasn't found. It comes with Git for Windows - install or update it from git-scm.com. You can still finish setup; pushes will just keep asking you to sign in.", 13, "WarnText"), 14, "WarnBg", "WarnBorder", 10));
                return sp;
            }
            sp.Children.Add(Ui.Text("Signed in right now", 13, "Muted", FontWeights.SemiBold));
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 16) };
            if (gcm.Count == 0) list.Children.Add(Ui.Text("No accounts yet.", 14, "Sub"));
            foreach (var g in gcm)
            {
                var r = Ui.Row(10, Ui.Icon("user", 18, "Muted"), Ui.MonoText(g, 14, "Text"));
                r.Margin = new Thickness(0, 4, 0, 4);
                list.Children.Add(r);
            }
            sp.Children.Add(list);
            var row = Ui.Row(8, Ui.Btn("Sign in an account...", delegate
            {
                try
                {
                    var p = Git.StartGcmLogin();
                    p.EnableRaisingEvents = true;
                    p.Exited += delegate { Dispatcher.BeginInvoke((Action)delegate { gcm = Git.CredentialManagerAccounts(); if (page == 1) Go(1); }); };
                }
                catch (Exception e) { Dialogs.Info(this, "Couldn't start Git Credential Manager", e.Message); }
            }), Ui.Btn("Refresh", delegate { gcm = Git.CredentialManagerAccounts(); Go(1); }));
            sp.Children.Add(row);
            var note = Ui.Text("\"Sign in an account\" opens a small console window and your browser. When the console closes, this list updates. Do it once per account; if an account is already listed, skip ahead.", 12.5, "Muted");
            note.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(note);
            return sp;
        }

        FrameworkElement HatsStep()
        {
            var sp = Page("Make your hats", "One hat per account. Use the email that's on that GitHub account so GitHub credits your commits.");
            foreach (var a in settings.accounts)
            {
                var acct = a;
                var row = new DockPanel();
                var tile = Hats.Tile(a.hat, 44);
                tile.Margin = new Thickness(0, 0, 12, 0);
                DockPanel.SetDock(tile, Dock.Left);
                row.Children.Add(tile);
                var btns = Ui.Row(6, Ui.Btn("Edit", delegate { AccountEditor.Edit(this, acct, settings.accounts, acct, gcm); Go(page); }),
                    Ui.Btn("Remove", delegate { settings.accounts.Remove(acct); foreach (var f in settings.folders.Where(f => f.account == acct.label)) f.account = ""; Go(page); }, "BtnGhost"));
                btns.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(btns, Dock.Right);
                row.Children.Add(btns);
                var info = Ui.Stack(2, Ui.Text(a.label, 15, "Text", FontWeights.SemiBold), Ui.MonoText(a.email + (string.IsNullOrEmpty(a.github) ? "" : "  ·  " + a.github), 12, "Muted"));
                info.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(info);
                var p = Ui.Panel(row, 12);
                p.Margin = new Thickness(0, 0, 0, 8);
                sp.Children.Add(p);
            }
            var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var g in gcm.Where(g => settings.AccountByGitHub(g) == null))
            {
                var user = g;
                var b = Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Make a hat for " + user, delegate { Add(user); });
                b.Margin = new Thickness(0, 0, 8, 8);
                actions.Children.Add(b);
            }
            var add = Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Add a hat", delegate { Add(null); });
            add.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(add);
            sp.Children.Add(actions);
            return sp;
        }

        void Add(string github)
        {
            var a = new Account { label = github ?? "", name = github ?? "", email = "", github = github ?? "", hat = HatKinds.DefaultFor(settings.accounts.Count) };
            if (AccountEditor.Edit(this, a, settings.accounts, null, gcm)) settings.accounts.Add(a);
            Go(page);
        }

        FrameworkElement FoldersStep()
        {
            var sp = Page("Pick your repo folders", "Watch the folders where you keep repos. Every repo inside is listed, and a folder's default hat applies to all of them - including repos you clone there later.");
            string gh = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub");
            if (settings.folders.Count == 0 && Directory.Exists(gh)) settings.folders.Add(new WatchedFolder { path = gh, account = "" });
            foreach (var f in settings.folders)
            {
                var folder = f;
                var choices = new List<Ui.Choice> { new Ui.Choice { Value = "", Label = "No default hat" } };
                foreach (var a in settings.accounts) choices.Add(new Ui.Choice { Value = a.label, Label = a.label, Sub = a.email, Hat = a.hat });
                var row = new DockPanel();
                var right = Ui.Row(6, Ui.Picker(choices, f.account ?? "", v => folder.account = v, 200),
                    Ui.Btn("Remove", delegate { settings.folders.Remove(folder); Go(page); }, "BtnGhost"));
                right.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(right, Dock.Right);
                row.Children.Add(right);
                row.Children.Add(Ui.Row(10, Ui.Icon("folder", 20, "Muted"), Ui.MonoText(f.path, 12.5, "Text")));
                var p = Ui.Panel(row, 12);
                p.Margin = new Thickness(0, 0, 0, 8);
                sp.Children.Add(p);
            }
            var add = Ui.IconBtn(Ui.Icon("plus", 14, "Text"), "Watch a folder", delegate
            {
                var path = FolderPicker.Pick(this, "Pick a folder that holds your repos");
                if (path != null && !settings.folders.Any(x => Settings.SamePath(x.path, path))) settings.folders.Add(new WatchedFolder { path = Settings.Norm(path), account = "" });
                Go(page);
            });
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(add);
            var note = Ui.Text("Repos that live somewhere else can be added one at a time later, from Folders.", 12.5, "Muted");
            note.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(note);
            return sp;
        }

        FrameworkElement Done()
        {
            int defaults = settings.folders.Count(f => !string.IsNullOrEmpty(f.account));
            var sp = Page("You're set", settings.accounts.Count + " hat(s), " + settings.folders.Count + " watched folder(s)" + (defaults > 0 ? ", " + defaults + " with a default hat." : ".") +
                " Next, Hatpick scans your folders and shows which hat each repo wears. Look for the ? buttons whenever something isn't clear.");
            tour = new CheckBox { Content = "Show me around (about a minute)", IsChecked = true, Margin = new Thickness(0, 0, 0, 12) };
            explorer = new CheckBox { Content = "Add \"Analyze with Hatpick\" to Explorer's right-click menu for folders", IsChecked = ExplorerMenu.Installed };
            sp.Children.Add(tour);
            sp.Children.Add(explorer);
            var note = Ui.Text("The right-click entry is per-user and needs no admin rights. You can change it later on the Folders page.", 12.5, "Muted");
            note.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(note);
            return sp;
        }

        void Finish()
        {
            try
            {
                if (explorer.IsChecked == true && !ExplorerMenu.Installed) ExplorerMenu.Install(Program.ExePath);
                else if (explorer.IsChecked != true && ExplorerMenu.Installed) ExplorerMenu.Remove();
            }
            catch { }
            finished = true;
            Close();
        }

        public static bool Run(Window owner, Settings settings, out bool startTour)
        {
            var w = new SetupWizard(owner, settings);
            w.ShowDialog();
            startTour = w.finished && w.tour != null && w.tour.IsChecked == true;
            return w.finished;
        }
    }
}
