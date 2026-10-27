using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace Hatpick
{
    // The window: a slim rail on the left and one page at a time on the right.
    // This file holds the shell, scanning and the actions; each page lives in its own file.
    partial class MainWindow : Window
    {
        static readonly string[] SkipDirs = { "node_modules", "Binaries", "Intermediate", "Saved", "DerivedDataCache", "Library", "obj", "bin" };
        const int MaxDepth = 4;

        readonly Settings settings;
        List<RepoReport> reports = new List<RepoReport>();
        readonly List<string> opened = new List<string>();      // analyzed folders that aren't saved in the list
        List<string> gcm = new List<string>();
        bool scanning, scannedOnce;
        string page = "overview";
        string selected;                                         // repo shown on the Repos page
        string search = "";
        readonly string startPath;

        Grid root;
        StackPanel railTop, railBottom;
        Border host;
        Grid overlay;                                            // used by the tour
        readonly Dictionary<string, FrameworkElement> railButtons = new Dictionary<string, FrameworkElement>();
        public readonly Dictionary<string, FrameworkElement> Marks = new Dictionary<string, FrameworkElement>();   // tour targets

        public MainWindow(Settings settings, string startPath)
        {
            this.settings = settings;
            this.startPath = startPath;
            Title = Program.Title;
            Width = 1240; Height = 820; MinWidth = 900; MinHeight = 600;
            if (Demo.On) { Width = 1280; Height = 800; ResizeMode = Demo.Autoplay != null ? ResizeMode.NoResize : ResizeMode.CanResize; }
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            FontSize = 14;
            SetResourceReference(BackgroundProperty, "Bg");
            SetResourceReference(ForegroundProperty, "Text");
            UseLayoutRounding = true;
            AllowDrop = true;
            Drop += OnDrop;

            root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rail = new DockPanel { LastChildFill = false };
            var railBorder = new Border { Child = rail, Padding = new Thickness(0, 12, 0, 12) };
            railBorder.SetResourceReference(Border.BackgroundProperty, "Rail");
            railTop = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            railBottom = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            DockPanel.SetDock(railTop, Dock.Top);
            DockPanel.SetDock(railBottom, Dock.Bottom);
            rail.Children.Add(railTop);
            rail.Children.Add(railBottom);
            root.Children.Add(railBorder);

            host = new Border();
            Grid.SetColumn(host, 1);
            root.Children.Add(host);

            overlay = new Grid { Visibility = Visibility.Collapsed };
            Grid.SetColumnSpan(overlay, 2);
            root.Children.Add(overlay);

            Content = root;
            SourceInitialized += delegate
            {
                Theme.DarkTitleBar(this);
                var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                if (src != null) src.AddHook(WndProc);
            };
            Loaded += delegate { OnFirstShown(); };
            KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.F5) Rescan(); };
            BuildRail();
            Render();
        }

        // ---------- startup ----------

        void OnFirstShown()
        {
            if (!Settings.Exists && Settings.OldExists)
            {
                settings.Save();
                SyncFolders();    // carry folder defaults over from an earlier name
            }
            if (!settings.setupDone) RunSetup();
            else Rescan();
            if (startPath != null) OpenPath(startPath);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Program.WM_COPYDATA)
            {
                string path = Program.ReadCopyData(lParam);
                Dispatcher.BeginInvoke((Action)delegate
                {
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Activate();
                    OpenPath(path);
                });
                handled = true;
                return (IntPtr)1;
            }
            return IntPtr.Zero;
        }

        // ---------- rail and navigation ----------

        void BuildRail()
        {
            railTop.Children.Clear();
            railBottom.Children.Clear();
            railButtons.Clear();
            // The badge shows the hat of the repo you're looking at, or Hatpick's own hard hat.
            var current = page == "repos" ? Find(selected) : null;
            FrameworkElement logo;
            string tip = "Hatpick";
            if (current != null)
            {
                var acct = CommitHat(current);
                logo = acct != null ? Hats.For(acct, 26) : current.Email.Length == 0 ? Hats.Icon("none", 26) : Ui.Icon("user", 22, "RailIcon");
                tip = current.DisplayName + " wears " + (acct != null ? acct.label : current.Email.Length == 0 ? "no hat" : current.Email);
            }
            else logo = Hats.Icon("hardhat", 26);
            var logoTile = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Margin = new Thickness(0, 0, 0, 14), Child = logo };
            logoTile.Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x33, 0x52));
            logoTile.ToolTip = tip;
            railTop.Children.Add(logoTile);
            RailButton(railTop, "overview", "Overview", "overview");
            RailButton(railTop, "repos", "Repos", "repos");
            RailButton(railTop, "hats", "Hats (your accounts)", "hats");
            RailButton(railTop, "folders", "Folders", "folders");
            RailButton(railTop, "reset", "Reset", "reset");

            var theme = RailIconButton("theme", settings.theme == "dark" ? "Switch to the light theme" : "Switch to the dark theme", ToggleTheme);
            railBottom.Children.Add(theme);
            Marks["theme"] = theme;
            var help = RailIconButton("help", "Help and tour", null);
            help.Click += delegate { ShowHelpMenu(help); };
            railBottom.Children.Add(help);
            Marks["help"] = help;
        }

        void RailButton(Panel into, string key, string tip, string icon)
        {
            bool on = page == key;
            var b = new Button { Style = Ui.Style("RailBtn"), ToolTip = tip, Content = Ui.Icon(icon, 20, on ? "RailAccent" : "RailIcon") };
            if (on) b.SetResourceReference(BackgroundProperty, "RailActive");
            System.Windows.Automation.AutomationProperties.SetName(b, tip);
            b.Click += delegate { Navigate(key); };
            into.Children.Add(b);
            railButtons[key] = b;
            Marks["rail-" + key] = b;
        }

        Button RailIconButton(string icon, string tip, Action onClick)
        {
            var b = new Button { Style = Ui.Style("RailBtn"), ToolTip = tip, Content = Ui.Icon(icon, 20, "RailIcon") };
            System.Windows.Automation.AutomationProperties.SetName(b, tip);
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        // Used by the demo's autoplay (Demo.cs).
        public void DemoShow(string to, string repo, string hat)
        {
            page = to;
            if (repo != null) selected = repo;
            if (hat != null) selectedHat = hat;
            Render();
        }

        public void Navigate(string to, string repo = null)
        {
            page = to;
            if (repo != null) selected = repo;
            BuildRail();
            Render();
        }

        void Render()
        {
            BuildRail();   // the badge follows the selected repo
            Marks.Keys.Where(k => !k.StartsWith("rail-") && k != "theme" && k != "help").ToList().ForEach(k => Marks.Remove(k));
            FrameworkElement view;
            switch (page)
            {
                case "repos": view = RepoPage(); break;
                case "hats": view = HatsPage(); break;
                case "folders": view = FoldersPage(); break;
                case "reset": view = ResetPage(); break;
                default: view = OverviewPage(); break;
            }
            host.Child = view;
        }

        // A page: scrolls vertically, comfortable margins, max width so lines don't get too long.
        static ScrollViewer PageScroll(UIElement content)
        {
            var inner = new Border { Padding = new Thickness(32, 22, 32, 32), Child = content, MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Stretch };
            return new ScrollViewer { Content = inner, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        // Page title row: title on the left, small buttons on the right.
        static DockPanel Header(string title, string helpTopic, params UIElement[] right)
        {
            var d = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 22) };
            var t = Ui.TitleWithHelp(Ui.Text(title, 24, "Text", FontWeights.Bold), helpTopic);
            DockPanel.SetDock(t, Dock.Left);
            d.Children.Add(t);
            var r = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var el in right) { var fe = el as FrameworkElement; if (fe != null) fe.Margin = new Thickness(6, 0, 0, 0); r.Children.Add(el); }
            DockPanel.SetDock(r, Dock.Right);
            d.Children.Add(r);
            return d;
        }

        // ---------- scanning ----------

        class Target { public string Path; public RepoSource Source; public string Watched; }

        public void Rescan()
        {
            if (scanning) return;
            scanning = true;
            var snapshot = settings.Copy();
            var openedNow = opened.ToList();
            Task.Factory.StartNew(() =>
            {
                var accounts = Git.CredentialManagerAccounts();
                var targets = FindTargets(snapshot, openedNow);
                var result = new RepoReport[targets.Count];
                Parallel.For(0, targets.Count, new ParallelOptions { MaxDegreeOfParallelism = 6 }, i =>
                {
                    var t = targets[i];
                    result[i] = Analyzer.Analyze(t.Path, t.Source, t.Watched, snapshot, accounts);
                });
                return Tuple.Create(accounts, result.ToList());
            }).ContinueWith(t =>
            {
                scanning = false;
                if (t.Exception != null) { Program.Report(t.Exception.InnerException); return; }
                gcm = t.Result.Item1;
                reports = t.Result.Item2;
                if (!scannedOnce) Demo.Start(this);
                scannedOnce = true;
                if (selected == null && reports.Count > 0) selected = reports[0].Path;
                Render();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        static List<Target> FindTargets(Settings s, List<string> openedNow)
        {
            var found = new List<Target>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in s.folders)
            {
                var hits = new List<string>();
                Walk(f.path, 0, hits);
                foreach (var h in hits.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    if (seen.Add(Settings.Norm(h))) found.Add(new Target { Path = Settings.Norm(h), Source = RepoSource.Watched, Watched = f.path });
            }
            foreach (var r in s.repos)
                if (seen.Add(Settings.Norm(r))) found.Add(new Target { Path = Settings.Norm(r), Source = RepoSource.Single });
            foreach (var r in openedNow)
                if (seen.Add(Settings.Norm(r))) found.Add(new Target { Path = Settings.Norm(r), Source = RepoSource.Opened });
            return found;
        }

        static bool IsRepo(string dir)
        {
            string g = Path.Combine(dir, ".git");
            return Directory.Exists(g) || File.Exists(g);   // .git is a file in worktrees and submodules
        }

        static void Walk(string dir, int depth, List<string> hits)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                if (IsRepo(dir)) { hits.Add(dir); return; }   // don't descend into repos
                if (depth >= MaxDepth) return;
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    string n = Path.GetFileName(sub);
                    if (n.StartsWith(".") || SkipDirs.Contains(n, StringComparer.OrdinalIgnoreCase)) continue;
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue;
                    Walk(sub, depth + 1, hits);
                }
            }
            catch { }   // access denied etc.
        }

        void Reanalyze(string path)
        {
            int i = reports.FindIndex(r => Settings.SamePath(r.Path, path));
            if (i < 0) { Rescan(); return; }
            var old = reports[i];
            reports[i] = Analyzer.Analyze(old.Path, old.Source, old.WatchedFolder, settings, gcm);
            commitCache.Remove(Settings.Norm(path));
            Render();
        }

        RepoReport Find(string path) { return path == null ? null : reports.FirstOrDefault(r => Settings.SamePath(r.Path, path)); }

        // ---------- what a repo wears, for display ----------

        Account CommitHat(RepoReport r) { return settings.AccountByEmail(r.Email); }
        Account PushHat(RepoReport r) { return settings.AccountByGitHub(r.PushUser); }

        string CommitHatKind(RepoReport r)
        {
            if (r.Error != null || r.Email.Length == 0) return "none";
            var a = CommitHat(r);
            return a != null ? a.hat : "unknown";
        }

        string HatLabel(RepoReport r)
        {
            if (r.Error != null) return "Can't read";
            if (r.Email.Length == 0) return "No hat";
            var a = CommitHat(r);
            return a != null ? a.label : r.Email;
        }

        // ---------- actions ----------

        void Apply(RepoReport r, Account commitAs, string pushAs)
        {
            try
            {
                Git.Assign(r.Path, commitAs, pushAs);
                settings.MarkManaged(r.Path);
                settings.Save();
            }
            catch (Exception e) { Dialogs.Info(this, "Couldn't change " + r.DisplayName, e.Message); }
            Reanalyze(r.Path);
        }

        void ResetRepo(RepoReport r)
        {
            if (!r.Managed) { Dialogs.Info(this, "Nothing to reset", "Hatpick hasn't changed " + r.DisplayName + "."); return; }
            if (!Dialogs.Ask(this, "Reset " + r.DisplayName + "?", "Its commit name, email and push account go back to exactly what the repo had before Hatpick first changed it.", "Reset")) return;
            try { Git.Reset(r.Path); settings.UnmarkManaged(r.Path); settings.Save(); }
            catch (Exception e) { Dialogs.Info(this, "Couldn't reset", e.Message); }
            Reanalyze(r.Path);
        }

        void OpenFolder(RepoReport r)
        {
            if (Directory.Exists(r.Path)) Process.Start("explorer.exe", "\"" + r.Path + "\"");
        }

        void OpenInDesktop(RepoReport r)
        {
            try { Process.Start("x-github-client://openLocalRepo/" + Uri.EscapeDataString(r.Path)); }
            catch (Exception e) { Dialogs.Info(this, "Couldn't open GitHub Desktop", e.Message); }
        }

        void OpenGitBash(RepoReport r)
        {
            try { Process.Start(new ProcessStartInfo(Git.GitBash, "--cd=\"" + r.Path + "\"") { UseShellExecute = false }); }
            catch (Exception e) { Dialogs.Info(this, "Couldn't open Git Bash", e.Message); }
        }

        void SyncFolders()
        {
            try { Git.SyncFolderDefaults(settings); }
            catch (Exception e) { Dialogs.Info(this, "Couldn't update folder defaults", e.Message); }
        }

        public void WatchFolder(string path)
        {
            if (settings.folders.Any(f => Settings.SamePath(f.path, path))) { Dialogs.Info(this, "Already watched", path + " is already a watched folder."); return; }
            settings.folders.Add(new WatchedFolder { path = Settings.Norm(path), account = "" });
            settings.Save();
            Rescan();
        }

        void PickAndWatch()
        {
            string p = FolderPicker.Pick(this, "Pick a folder that holds your repos");
            if (p != null) WatchFolder(p);
        }

        void PickSingle()
        {
            string p = FolderPicker.Pick(this, "Pick a repo folder (or any folder inside one)");
            if (p == null) return;
            string top = Git.Toplevel(p);
            if (top == null) { Dialogs.Info(this, "Not a repo", "That folder isn't inside a git repo."); return; }
            AddSingle(top);
        }

        void AddSingle(string top)
        {
            if (!settings.repos.Any(x => Settings.SamePath(x, top))) settings.repos.Add(Settings.Norm(top));
            opened.RemoveAll(x => Settings.SamePath(x, top));
            settings.Save();
            selected = top;
            Rescan();
        }

        void RemoveSingle(RepoReport r)
        {
            settings.repos.RemoveAll(p => Settings.SamePath(p, r.Path));
            settings.Save();
            if (r.Managed && Dialogs.Ask(this, "Reset it too?", r.DisplayName + " is off your list. Hatpick changed its settings; they stay unless you reset it. Reset it now?", "Reset", "Keep settings"))
            { try { Git.Reset(r.Path); settings.UnmarkManaged(r.Path); settings.Save(); } catch { } }
            Rescan();
        }

        // Analyze any folder: a repo (or a folder inside one) opens on the Repos page;
        // a plain folder full of repos is offered as a watched folder.
        public void OpenPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path)) { Dialogs.Info(this, "Folder not found", path); return; }
            string top = Git.Toplevel(path);
            if (top != null)
            {
                if (Find(top) == null && !opened.Any(x => Settings.SamePath(x, top))) opened.Add(top);
                selected = top;
                page = "repos";
                BuildRail();
                if (Find(top) == null) Rescan(); else Render();
                return;
            }
            var hits = new List<string>();
            Walk(path, 0, hits);
            if (hits.Count == 0) { Dialogs.Info(this, "No repos here", "There are no git repos in\n" + path); return; }
            if (Dialogs.Ask(this, "Watch this folder?", path + " isn't a repo itself, but it has " + hits.Count + " repo(s) inside. Watch it to list them all and give them a default hat?", "Watch folder"))
                WatchFolder(path);
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null) return;
            foreach (var p in paths)
            {
                string dir = Directory.Exists(p) ? p : Path.GetDirectoryName(p);
                Dispatcher.BeginInvoke((Action)delegate { OpenPath(dir); });
            }
        }

        void ToggleTheme()
        {
            settings.theme = settings.theme == "dark" ? "light" : "dark";
            settings.Save();
            Theme.Apply(Application.Current, settings.theme);
            BuildRail();
        }

        void SignInAccount()
        {
            if (!Dialogs.Ask(this, "Sign in another account",
                "This opens Git Credential Manager's own sign-in: a small console window and your browser, where you sign in to GitHub.\n\n" +
                "Hatpick doesn't see anything you enter. When the console closes, Hatpick refreshes the list of signed-in accounts.", "Open sign-in")) return;
            try
            {
                var p = Git.StartGcmLogin();
                p.EnableRaisingEvents = true;
                p.Exited += delegate { Dispatcher.BeginInvoke((Action)Rescan); };
            }
            catch (Exception e) { Dialogs.Info(this, "Couldn't start Git Credential Manager", e.Message); }
        }

        // ---------- hats (accounts) ----------

        void AddAccount(string github = null)
        {
            var a = new Account { label = "", name = github ?? "", email = "", github = github ?? "", hat = HatKinds.DefaultFor(settings.accounts.Count) };
            if (github != null) a.label = github;
            if (!AccountEditor.Edit(this, a, settings.accounts, null, gcm)) return;
            settings.accounts.Add(a);
            settings.Save();
            SyncFolders();
            Rescan();
        }

        void EditAccount(Account a)
        {
            var before = new Account { label = a.label, name = a.name, email = a.email, github = a.github, hat = a.hat };
            if (!AccountEditor.Edit(this, a, settings.accounts, a, gcm)) return;
            foreach (var f in settings.folders.Where(f => f.account == before.label)) f.account = a.label;
            if (selectedHat == before.label) selectedHat = a.label;
            int updated = 0;
            if (a.name != before.name || a.email != before.email || a.github != before.github)
                foreach (var r in reports.Where(x => x.LocalIdentity && string.Equals(x.Email, before.email, StringComparison.OrdinalIgnoreCase)))
                {
                    string push = !r.LocalPush ? null : string.Equals(r.PushUser, before.github, StringComparison.OrdinalIgnoreCase) ? a.github : r.PushUser;
                    try { Git.Assign(r.Path, a, push); settings.MarkManaged(r.Path); updated++; } catch { }
                }
            settings.Save();
            SyncFolders();
            Rescan();
            if (updated > 0) Dialogs.Info(this, "Repos updated", updated + " repo(s) wearing this hat now use the new details.");
        }

        void RemoveAccount(Account a)
        {
            if (!Dialogs.Ask(this, "Remove the " + a.label + " hat?",
                "Repos already set to it keep their settings until you change or reset them. Folder defaults that use it are cleared.\n\nThis doesn't sign the account out of Git Credential Manager.",
                "Remove", "Cancel", false)) return;
            settings.accounts.Remove(a);
            foreach (var f in settings.folders.Where(f => f.account == a.label)) f.account = "";
            settings.Save();
            SyncFolders();
            Rescan();
        }

        List<Ui.Choice> HatChoices(string noneLabel, bool github)
        {
            var list = new List<Ui.Choice> { new Ui.Choice { Value = "", Label = noneLabel } };
            foreach (var a in settings.accounts)
                list.Add(new Ui.Choice { Value = github ? a.github : a.label, Label = a.label, Sub = github ? a.github : a.email, Acct = a });
            if (github)
                foreach (var g in gcm.Where(g => settings.AccountByGitHub(g) == null))
                    list.Add(new Ui.Choice { Value = g, Label = g, Sub = "signed in, not a hat yet", Hat = "none" });
            return list.Where(c => !github || c.Value != null).ToList();
        }

        // ---------- help menu ----------

        void ShowHelpMenu(UIElement anchor)
        {
            var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Right, StaysOpen = false, AllowsTransparency = true, HorizontalOffset = 8 };
            var list = new StackPanel();
            Action<string, Action> item = (text, act) =>
            {
                var b = new Button { Style = Ui.Style("Row"), Content = Ui.Text(text, 13), MinHeight = 36, Padding = new Thickness(12, 0, 12, 0) };
                b.Click += delegate { popup.IsOpen = false; act(); };
                list.Children.Add(b);
            };
            item("Take the tour", StartTour);
            item("Run setup again", RunSetup);
            item("How Hatpick works", () => Ui.ShowHelp(anchor, HelpText.Topics["overview"]));
            item("Watched folders vs. single repos", () => Ui.ShowHelp(anchor, HelpText.Topics["folders"]));
            item("Resetting", () => Ui.ShowHelp(anchor, HelpText.Topics["reset"]));
            item("Sign in another account", SignInAccount);
            item("About Hatpick", () => Dialogs.Info(this, "Hatpick " + typeof(MainWindow).Assembly.GetName().Version.ToString(3),
                "Picks which Git account each repo commits and pushes as.\n\nIt never signs in or sees a password or token; Git Credential Manager keeps your sign-ins.\n\nUsing git at:\n" + Git.Exe));
            var card = Ui.Panel(list, 6);
            card.MinWidth = 240;
            card.Margin = new Thickness(0, 0, 12, 12);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 };
            popup.Child = card;
            popup.IsOpen = true;
        }

        void RunSetup()
        {
            bool tour;
            if (SetupWizard.Run(this, settings, out tour))
            {
                settings.setupDone = true;
                settings.Save();
                SyncFolders();
                Rescan();
                if (tour) Dispatcher.BeginInvoke((Action)StartTour, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else
            {
                settings.setupDone = true;
                settings.Save();
                Rescan();
            }
        }
    }
}
