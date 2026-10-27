using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Hatpick
{
    // Repos page: a slim list on the left, and for the selected repo who it commits and
    // pushes as, where each setting comes from, its hats, and its recent commits.
    partial class MainWindow
    {
        readonly Dictionary<string, BranchState> commitCache = new Dictionary<string, BranchState>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> loadingCommits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> commitSel = new HashSet<string>();
        string commitSelRepo;
        string rewriteAuthor = "";          // account label, "" = keep the author
        bool rewriteStrip = true, rewriteCommitter = true;
        string listSearch = "";

        FrameworkElement RepoPage()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var side = new Border { BorderThickness = new Thickness(0, 0, 1, 0) };
            side.SetResourceReference(Border.BackgroundProperty, "Side");
            side.SetResourceReference(Border.BorderBrushProperty, "Border");
            var sideDock = new DockPanel { Margin = new Thickness(10, 14, 10, 10) };
            var box = new TextBox { Height = 32, Text = listSearch, Margin = new Thickness(0, 0, 0, 8), ToolTip = "Search repos" };
            System.Windows.Automation.AutomationProperties.SetName(box, "Search repos");
            DockPanel.SetDock(box, Dock.Top);
            sideDock.Children.Add(box);
            var listHost = new StackPanel();
            box.TextChanged += delegate { listSearch = box.Text; FillRepoList(listHost); };
            FillRepoList(listHost);
            sideDock.Children.Add(new ScrollViewer { Content = listHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            side.Child = sideDock;
            grid.Children.Add(side);
            Marks["repolist"] = side;

            var r = Find(selected);
            FrameworkElement detail;
            if (!scannedOnce) detail = PageScroll(Ui.Text("Looking for repos...", 14, "Muted"));
            else if (r == null) detail = PageScroll(Ui.Stack(8, Ui.Text("No repo selected", 22, "Text", FontWeights.Bold),
                Ui.Text("Pick a repo on the left, or drop any folder onto this window to analyze it.", 14, "Muted")));
            else detail = PageScroll(RepoDetail(r));
            Grid.SetColumn(detail, 1);
            grid.Children.Add(detail);
            return grid;
        }

        void FillRepoList(StackPanel into)
        {
            into.Children.Clear();
            string q = listSearch.Trim();
            Action<string, IEnumerable<RepoReport>> group = (title, items) =>
            {
                var list = items.Where(x => q.Length == 0 || x.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (list.Count == 0) return;
                var h = Ui.SectionTitle(title);
                h.FontSize = 11; h.Margin = new Thickness(8, 12, 0, 4);
                h.TextTrimming = TextTrimming.CharacterEllipsis; h.TextWrapping = TextWrapping.NoWrap;
                into.Children.Add(h);
                foreach (var x in list)
                {
                    var rep = x;
                    var row = new DockPanel();
                    var dot = Ui.Dot(x.Level, 7);
                    dot.Margin = new Thickness(0, 0, 9, 0);
                    DockPanel.SetDock(dot, Dock.Left);
                    row.Children.Add(dot);
                    var acct = Ui.Text(HatLabel(x), 11, "Muted");
                    acct.TextWrapping = TextWrapping.NoWrap;
                    acct.Margin = new Thickness(6, 0, 0, 0);
                    DockPanel.SetDock(acct, Dock.Right);
                    row.Children.Add(acct);
                    bool on = Settings.SamePath(selected, x.Path);
                    var name = Ui.Text(x.DisplayName, 13, "Text", on ? FontWeights.SemiBold : FontWeights.Normal);
                    name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap;
                    row.Children.Add(name);
                    var b = new Button { Style = Ui.Style("Row"), Content = row, ToolTip = x.Path };
                    if (on) b.SetResourceReference(BackgroundProperty, "SelRow");
                    b.Click += delegate { Navigate("repos", rep.Path); };
                    into.Children.Add(b);
                }
            };
            foreach (var f in settings.folders)
            {
                var folder = f;
                var def = settings.AccountByLabel(f.account);
                group(System.IO.Path.GetFileName(f.path.TrimEnd('\\')) + (def != null ? " - " + def.label : ""),
                    reports.Where(x => x.Source == RepoSource.Watched && Settings.SamePath(x.WatchedFolder, folder.path)));
            }
            group("Single repos", reports.Where(x => x.Source == RepoSource.Single));
            group("Analyzed", reports.Where(x => x.Source == RepoSource.Opened));
        }

        FrameworkElement RepoDetail(RepoReport r)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.MonoText(r.Path, 12, "Muted"));
            var actions = new List<UIElement>
            {
                Ui.IconBtn(Ui.Icon("folder", 14, "Text"), "Open folder", delegate { OpenFolder(r); }),
                Ui.IconBtn(Ui.Icon("external", 14, "Text"), "GitHub Desktop", delegate { OpenInDesktop(r); })
            };
            if (Git.GitBash != null) actions.Add(Ui.IconBtn(Ui.Icon("terminal", 14, "Text"), "Git Bash", delegate { OpenGitBash(r); }));
            if (r.Source == RepoSource.Opened) actions.Add(Ui.Btn("Keep in list", delegate { AddSingle(r.Path); }, "BtnPrimary"));
            if (r.Source == RepoSource.Single) actions.Add(Ui.Btn("Remove from list", delegate { RemoveSingle(r); }));
            var head = Header(r.DisplayName, null, actions.ToArray());
            head.Margin = new Thickness(0, 4, 0, 18);
            sp.Children.Add(head);

            if (r.Error != null)
            {
                sp.Children.Add(IssueBanner(new Issue(Level.Error, r.Error), null));
                return sp;
            }

            foreach (var issue in r.Issues.OrderByDescending(i => i.Level))
            {
                Button fix = issue.Level >= Level.Warn ? QuickFix(r) : null;
                sp.Children.Add(IssueBanner(issue, fix));
            }

            // the three facts
            var facts = Ui.Cards(220);
            facts.Margin = new Thickness(0, 6, 0, 10);
            var ch = CommitHat(r);
            facts.Children.Add(FactCard("Commits as", "commits", r.Email.Length == 0 ? "none" : ch != null ? ch.hat : "unknown",
                r.Email.Length == 0 ? "No hat" : ch != null ? ch.label : r.Name, r.Email.Length == 0 ? "Commits will fail" : r.Email, ch));
            var ph = PushHat(r);
            string pushTitle, pushSub, pushKind;
            if (r.IsSsh) { pushKind = "key"; pushTitle = "SSH key"; pushSub = "Your SSH key decides"; }
            else if (r.IsLocal) { pushKind = "none"; pushTitle = "No sign-in"; pushSub = "The remote is a local folder"; }
            else if (r.PushUser.Length == 0) { pushKind = "none"; pushTitle = "Not set"; pushSub = Git.GcmAvailable && gcm.Count == 1 ? "GCM uses " + gcm[0] : "GCM will ask which account"; }
            else
            {
                pushKind = ph != null ? ph.hat : "unknown"; pushTitle = ph != null ? ph.label : r.PushUser;
                pushSub = r.PushUser + (r.SignedIn == true ? "  ·  signed in" : r.SignedIn == false ? "  ·  not signed in" : "");
            }
            facts.Children.Add(FactCard("Pushes as", "pushes", pushKind, pushTitle, pushSub, r.IsSsh || r.IsLocal ? null : ph));
            facts.Children.Add(FactCard("Remote", "remote", "cloud", r.RemoteName.Length > 0 ? r.RemoteName : "No remote",
                r.Host.Length > 0 ? r.Host + "/" + r.Owner + "/" + r.RepoName : r.RemoteUrl.Length > 0 ? r.RemoteUrl : "Add one by publishing from GitHub Desktop"));
            sp.Children.Add(facts);
            Marks["facts"] = facts;

            var layers = LayersTable(r);
            sp.Children.Add(layers);
            Marks["layers"] = layers;

            var set = SetHatsPanel(r);
            sp.Children.Add(set);
            Marks["set"] = set;

            var commits = CommitsSection(r);
            sp.Children.Add(commits);
            Marks["commits"] = commits;
            return sp;
        }

        Border IssueBanner(Issue issue, Button fix)
        {
            string bg = issue.Level == Level.Error ? "ErrBg" : issue.Level == Level.Warn ? "WarnBg" : "InfoBg";
            string bd = issue.Level == Level.Error ? "ErrBorder" : issue.Level == Level.Warn ? "WarnBorder" : "InfoBorder";
            string fg = issue.Level == Level.Error ? "ErrText" : issue.Level == Level.Warn ? "WarnText" : "InfoText";
            var row = new DockPanel();
            var icon = Ui.StatusIcon(issue.Level, 18);
            icon.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            if (fix != null) { fix.Margin = new Thickness(12, 0, 0, 0); DockPanel.SetDock(fix, Dock.Right); row.Children.Add(fix); }
            row.Children.Add(Ui.Text(issue.Text, 13, fg));
            var b = Ui.Panel(row, 12, bg, bd, 10);
            b.Margin = new Thickness(0, 0, 0, 8);
            return b;
        }

        Button FactCard(string caption, string help, string kind, string title, string sub, Account acct = null)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.TitleWithHelp(Ui.Text(caption, 12, "Muted"), help));
            FrameworkElement tile;
            if (kind == "key" || kind == "cloud")
            {
                var t = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Child = Ui.Icon(kind, 22, "Sub") };
                t.SetResourceReference(Border.BackgroundProperty, "Input");
                tile = t;
            }
            else if (kind == "unknown")
            {
                var t = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Child = Ui.Icon("user", 22, "Sub") };
                t.SetResourceReference(Border.BackgroundProperty, "Input");
                tile = t;
            }
            else tile = acct != null ? Hats.TileFor(acct, 40) : Hats.Tile(kind, 40);
            var name = Ui.Text(title, 17, "Text", FontWeights.SemiBold);
            name.VerticalAlignment = VerticalAlignment.Center;
            var row = Ui.Row(10, tile, name);
            row.Margin = new Thickness(0, 8, 0, 8);
            sp.Children.Add(row);
            var s = Ui.MonoText(sub, 12, "Sub");
            sp.Children.Add(s);
            var b = new Button { Style = Ui.Style("Card"), Content = sp, Margin = new Thickness(0, 0, 12, 12), Cursor = System.Windows.Input.Cursors.Arrow, Focusable = false };
            return b;
        }

        Border LayersTable(RepoReport r)
        {
            var g = new Grid();
            for (int i = 0; i < 4; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(1.1, GridUnitType.Star) : new GridLength(1.3, GridUnitType.Star) });
            int row = 0;
            Action<UIElement, int> put = (el, col) =>
            {
                Grid.SetRow(el, row); Grid.SetColumn(el, col);
                var fe = el as FrameworkElement; if (fe != null) fe.Margin = new Thickness(14, 9, 8, 9);
                g.Children.Add(el);
            };
            g.RowDefinitions.Add(new RowDefinition());
            put(Ui.Text("Layer", 12, "Muted"), 0); put(Ui.Text("Commit name", 12, "Muted"), 1); put(Ui.Text("Commit email", 12, "Muted"), 2); put(Ui.Text("Push account", 12, "Muted"), 3);
            foreach (var l in r.Layers)
            {
                row++;
                g.RowDefinitions.Add(new RowDefinition());
                var line = new Border { BorderThickness = new Thickness(0, 1, 0, 0) };
                line.SetResourceReference(Border.BorderBrushProperty, "Border");
                Grid.SetRow(line, row); Grid.SetColumnSpan(line, 4);
                g.Children.Add(line);
                put(Ui.Text(l.Layer, 13, "Text", FontWeights.SemiBold), 0);
                put(LayerValue(l.Name, l.NameWins), 1);
                put(LayerValue(l.Email, l.EmailWins), 2);
                put(LayerValue(l.Push, l.PushWins), 3);
            }
            var head = new DockPanel { Margin = new Thickness(14, 12, 14, 4) };
            var note = Ui.Text("Git reads top to bottom; the first value it finds wins.", 12, "Muted");
            DockPanel.SetDock(note, Dock.Right);
            note.TextWrapping = TextWrapping.NoWrap;
            head.Children.Add(note);
            head.Children.Add(Ui.TitleWithHelp(Ui.Text("Where each setting comes from", 14, "Text", FontWeights.SemiBold), "layers"));
            var sp = new StackPanel();
            sp.Children.Add(head);
            sp.Children.Add(g);
            var b = Ui.Panel(sp, 0);
            b.Margin = new Thickness(0, 0, 0, 14);
            return b;
        }

        static FrameworkElement LayerValue(string value, bool wins)
        {
            if (value.Length == 0) { var dash = Ui.Text("-", 12.5, "Faint"); dash.FontFamily = Ui.Mono; return dash; }
            var t = Ui.MonoText(value, 12.5, wins ? "WinText" : "Muted");
            if (!wins) { t.TextDecorations = TextDecorations.Strikethrough; return t; }
            var b = new Border { Child = t, Padding = new Thickness(7, 2, 7, 3), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
            b.SetResourceReference(Border.BackgroundProperty, "WinBg");
            b.SetResourceReference(Border.BorderBrushProperty, "WinBorder");
            return b;
        }

        Border SetHatsPanel(RepoReport r)
        {
            var localAcct = r.LocalIdentity ? settings.AccountByEmail(r.Email) : null;
            string commitVal = localAcct != null ? localAcct.label : "";
            string pushVal = r.LocalPush ? r.PushUser : "";
            var commitChoices = HatChoices("Not set here (use folder default)", false);
            var pushChoices = HatChoices("Not set here (use folder default)", true);
            if (pushVal.Length > 0 && !pushChoices.Any(c => c.Value == pushVal)) pushChoices.Add(new Ui.Choice { Value = pushVal, Label = pushVal, Hat = "none" });
            Button pushPicker = null;
            var commitPicker = Ui.Picker(commitChoices, commitVal, v =>
            {
                commitVal = v;
                var a = settings.AccountByLabel(v);
                if (a != null && !string.IsNullOrEmpty(a.github) && pushChoices.Any(c => c.Value == a.github))
                {
                    pushVal = a.github;
                    var fresh = Ui.Picker(pushChoices, pushVal, pv => pushVal = pv, 250);
                    var parent = (Panel)pushPicker.Parent;
                    int idx = parent.Children.IndexOf(pushPicker);
                    parent.Children.RemoveAt(idx);
                    parent.Children.Insert(idx, fresh);
                    pushPicker = fresh;
                }
            }, 250);
            pushPicker = Ui.Picker(pushChoices, pushVal, v => pushVal = v, 250);

            var wrap = new WrapPanel();
            wrap.Children.Add(Ui.Stack(5, Ui.Text("Commit hat", 12, "Muted"), commitPicker));
            var pushCol = Ui.Stack(5, Ui.Text("Push hat", 12, "Muted"), pushPicker);
            pushCol.Margin = new Thickness(12, 0, 0, 0);
            wrap.Children.Add(pushCol);
            var apply = Ui.Btn("Apply", delegate { Apply(r, settings.AccountByLabel(commitVal), pushVal.Length > 0 ? pushVal : null); }, "BtnPrimary");
            apply.Height = 36; apply.Padding = new Thickness(18, 0, 18, 0);
            var reset = Ui.Btn("Reset this repo", delegate { ResetRepo(r); });
            reset.Height = 36; reset.IsEnabled = r.Managed;
            var btns = Ui.Row(8, apply, reset);
            btns.VerticalAlignment = VerticalAlignment.Bottom;
            btns.Margin = new Thickness(12, 0, 0, 0);
            wrap.Children.Add(btns);

            var sp = Ui.Stack(10, Ui.TitleWithHelp(Ui.Text("Pick hats for this repo", 14, "Text", FontWeights.SemiBold), "set"), wrap,
                Ui.Text("Only changes this repo's .git/config." + (r.Managed ? " Hatpick has changed it before; Reset puts back what it had." : ""), 12, "Faint"));
            var b = Ui.Panel(sp, 16);
            b.Margin = new Thickness(0, 0, 0, 22);
            return b;
        }

        // ---------- commits ----------

        FrameworkElement CommitsSection(RepoReport r)
        {
            string key = Settings.Norm(r.Path);
            if (commitSelRepo != key) { commitSel = new HashSet<string>(); commitSelRepo = key; }
            var sp = new StackPanel();
            BranchState st;
            if (!commitCache.TryGetValue(key, out st))
            {
                sp.Children.Add(Ui.Text("Commits", 18, "Text", FontWeights.SemiBold));
                sp.Children.Add(Ui.Text("Reading commits...", 13, "Muted"));
                if (loadingCommits.Add(key))
                    Task.Factory.StartNew(() => History.Read(r.Path)).ContinueWith(t =>
                    {
                        loadingCommits.Remove(key);
                        if (t.Exception == null) commitCache[key] = t.Result;
                        if (page == "repos" && Settings.SamePath(selected, r.Path)) Render();
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                return sp;
            }

            var title = Ui.Text("Commits" + (st.Branch != null ? " on " + st.Branch : ""), 18, "Text", FontWeights.SemiBold);
            var sub = st.Branch == null ? "Detached HEAD" : st.Upstream != null ? "tracking " + st.Upstream + "  ·  " + st.UnpushedCount + " not pushed" : "not pushed anywhere yet";
            var refresh = Ui.IconBtn(Ui.Icon("rescan", 14, "Text"), "Refresh", delegate { commitCache.Remove(key); Render(); });
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var backups = History.Backups(r.Path, st.Branch);
            if (backups.Count > 0)
            {
                var undo = Ui.Btn("Undo last change (" + backups[0].When + ")", delegate { UndoRewrite(r, st, backups[0]); });
                undo.ToolTip = "Puts the branch back as it was before Hatpick's last rewrite or undo of commits";
                undo.Margin = new Thickness(0, 0, 6, 0);
                right.Children.Add(undo);
            }
            right.Children.Add(refresh);
            DockPanel.SetDock(right, Dock.Right);
            head.Children.Add(right);
            head.Children.Add(Ui.Stack(2, title, Ui.Text(sub, 12, "Muted")));
            sp.Children.Add(head);

            if (st.Commits.Count == 0) { sp.Children.Add(Ui.Text("No commits yet.", 13, "Muted")); return sp; }

            // before-you-push check: local commits not wearing this repo's commit hat
            var hat = CommitHat(r);
            var wrong = hat == null ? new List<CommitInfo>() : st.Commits.Where(c => !c.Pushed && !string.Equals(c.AuthorEmail, hat.email, StringComparison.OrdinalIgnoreCase)).ToList();
            var withCo = st.Commits.Where(c => !c.Pushed && c.CoAuthors.Count > 0).ToList();
            if (wrong.Count > 0 || withCo.Count > 0)
            {
                string msg = wrong.Count > 0
                    ? wrong.Count + " commit(s) not pushed yet are authored by someone other than " + hat.label + ", the hat this repo wears."
                    : withCo.Count + " commit(s) not pushed yet have co-authors.";
                var fix = Ui.Btn(wrong.Count > 0 ? "Fix them before you push" : "Review co-authors", delegate
                {
                    commitSel = new HashSet<string>((wrong.Count > 0 ? wrong : withCo).Select(c => c.Sha));
                    if (wrong.Count > 0) rewriteAuthor = hat.label;
                    Render();
                }, "BtnPrimary");
                sp.Children.Add(IssueBanner(new Issue(Level.Warn, msg + " Fixing commits before they're pushed doesn't affect anyone else."), fix));
            }

            var list = new StackPanel();
            foreach (var c in st.Commits) list.Children.Add(CommitRow(c));
            var listPanel = Ui.Panel(list, 4);
            listPanel.Margin = new Thickness(0, 4, 0, 12);
            sp.Children.Add(listPanel);

            // rewrite controls
            var authorChoices = new List<Ui.Choice> { new Ui.Choice { Value = "", Label = "Keep the author" } };
            foreach (var a in settings.accounts) authorChoices.Add(new Ui.Choice { Value = a.label, Label = a.label, Sub = a.name + " <" + a.email + ">", Acct = a });
            var author = Ui.Picker(authorChoices, rewriteAuthor, v => rewriteAuthor = v, 240);
            var strip = new CheckBox { Content = "Remove co-authors", IsChecked = rewriteStrip, VerticalAlignment = VerticalAlignment.Center };
            strip.Click += delegate { rewriteStrip = strip.IsChecked == true; };
            var committer = new CheckBox { Content = "Also make you the committer", IsChecked = rewriteCommitter, VerticalAlignment = VerticalAlignment.Center, ToolTip = "The committer is who last wrote the commit. Usually the same as the author for your own commits." };
            committer.Click += delegate { rewriteCommitter = committer.IsChecked == true; };
            var go = Ui.Btn(commitSel.Count == 0 ? "Select commits above" : "Rewrite " + commitSel.Count + " commit(s)...", delegate { ConfirmRewrite(r, st); }, "BtnPrimary");
            go.IsEnabled = commitSel.Count > 0;
            go.Height = 36;
            var controls = new WrapPanel();
            controls.Children.Add(Ui.Stack(5, Ui.Text("Set author to", 12, "Muted"), author));
            var checks = Ui.Stack(8, strip, committer);
            checks.Margin = new Thickness(16, 18, 16, 0);
            controls.Children.Add(checks);
            go.Margin = new Thickness(0, 18, 0, 0);
            controls.Children.Add(go);
            var tools = Ui.Panel(Ui.Stack(8,
                Ui.Text("Fix selected commits", 14, "Text", FontWeights.SemiBold),
                Ui.Text("Changes the author and/or removes Co-authored-by lines. Files don't change. Hatpick saves a backup first and never pushes for you.", 12, "Muted"),
                controls), 16);
            sp.Children.Add(tools);

            // undo commits, keeping their changes
            int oldestSel = -1;
            for (int i = 0; i < st.Commits.Count; i++) if (commitSel.Contains(st.Commits[i].Sha)) oldestSel = i;
            int undoCount = oldestSel + 1;
            var uncommit = Ui.Btn(undoCount == 0 ? "Select commits above" : "Undo " + undoCount + " commit(s)...", delegate { ConfirmUncommit(r); });
            uncommit.IsEnabled = undoCount > 0;
            uncommit.Height = 36;
            uncommit.HorizontalAlignment = HorizontalAlignment.Left;
            var undoPanel = Ui.Panel(Ui.Stack(8,
                Ui.TitleWithHelp(Ui.Text("Undo commits, keep the changes", 14, "Text", FontWeights.SemiBold), "uncommit"),
                Ui.Text("Takes the selected commits, and any newer ones, off the branch. Their changes stay in your folder, ready to commit again: " +
                    "for example without some files, or split differently. Hatpick saves a backup first and never pushes for you.", 12, "Muted"),
                uncommit), 16);
            undoPanel.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(undoPanel);
            return sp;
        }

        FrameworkElement CommitRow(CommitInfo c)
        {
            var row = new DockPanel { Margin = new Thickness(6, 2, 6, 2) };
            var cb = new CheckBox { IsChecked = commitSel.Contains(c.Sha), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0), IsEnabled = !c.IsMerge };
            System.Windows.Automation.AutomationProperties.SetName(cb, "Select " + c.Subject);
            cb.Click += delegate
            {
                if (cb.IsChecked == true) commitSel.Add(c.Sha); else commitSel.Remove(c.Sha);
                Render();
            };
            DockPanel.SetDock(cb, Dock.Left);
            row.Children.Add(cb);

            var acct = settings.AccountByEmail(c.AuthorEmail);
            var hatIcon = acct != null ? Hats.For(acct, 22) : Ui.Icon("user", 20, "Muted");
            hatIcon.Margin = new Thickness(0, 0, 10, 0);
            hatIcon.ToolTip = c.AuthorName + " <" + c.AuthorEmail + ">";
            DockPanel.SetDock(hatIcon, Dock.Left);
            row.Children.Add(hatIcon);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (c.CoAuthors.Count > 0) right.Children.Add(Chip("+" + c.CoAuthors.Count + " co-author" + (c.CoAuthors.Count > 1 ? "s" : ""), "WarnBg", "WarnText", string.Join("\n", c.CoAuthors)));
            if (c.IsMerge) right.Children.Add(Chip("Merge", "InfoBg", "InfoText", null));
            right.Children.Add(Chip(c.Pushed ? "Pushed" : "Local", c.Pushed ? "Input" : "OkBg", c.Pushed ? "Muted" : "OkText", c.Pushed ? "Already on a remote branch" : "Only on this computer"));
            var sha = Ui.MonoText(c.Short, 12, "Faint");
            sha.Margin = new Thickness(10, 0, 0, 0);
            sha.VerticalAlignment = VerticalAlignment.Center;
            right.Children.Add(sha);
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);

            var subj = Ui.Text(c.Subject, 13.5, "Text");
            subj.TextTrimming = TextTrimming.CharacterEllipsis; subj.TextWrapping = TextWrapping.NoWrap;
            var meta = Ui.Text((acct != null ? acct.label : c.AuthorName) + "  ·  " + c.When.ToString("MMM d, h:mm tt"), 11.5, "Muted");
            row.Children.Add(Ui.Stack(1, subj, meta));
            var b = new Border { Child = row, Padding = new Thickness(4, 6, 4, 6), CornerRadius = new CornerRadius(8) };
            if (commitSel.Contains(c.Sha)) b.SetResourceReference(Border.BackgroundProperty, "SelRow");
            return b;
        }

        static Border Chip(string text, string bg, string fg, string tip)
        {
            var t = Ui.Text(text, 11, fg);
            t.TextWrapping = TextWrapping.NoWrap;
            var b = new Border { Child = t, Padding = new Thickness(8, 2, 8, 3), CornerRadius = new CornerRadius(10), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            b.SetResourceReference(Border.BackgroundProperty, bg);
            if (tip != null) b.ToolTip = tip;
            return b;
        }

        void ConfirmRewrite(RepoReport r, BranchState st)
        {
            // re-read so the plan uses the latest state of the branch
            var fresh = History.Read(r.Path);
            commitCache[Settings.Norm(r.Path)] = fresh;
            var sel = new HashSet<string>(commitSel.Where(s => fresh.Commits.Any(c => c.Sha == s)));
            var author = settings.AccountByLabel(rewriteAuthor);
            var plan = History.Plan(r.Path, fresh, sel, settings, author, rewriteStrip);
            if (plan.Blockers.Count > 0)
            {
                Dialogs.Ask(this, "Can't rewrite these commits", Dialogs.IssueList(plan.Blockers.Select(b => new Issue(Level.Error, b))), "OK", null, true);
                return;
            }
            var w = new ThemedWindow(this, "Rewrite commits", 600);
            bool go = false;
            var what = "Rewrite " + sel.Count + " commit(s)" + (author != null ? " to be authored by " + author.label + " (" + author.email + ")" : "") +
                       (rewriteStrip ? (author != null ? " and" : "") + " remove their co-authors" : "") + ".";
            var body = Ui.Stack(12, Ui.Text("Before you rewrite", 18, "Text", FontWeights.SemiBold), Ui.Text(what, 14, "Sub"), Dialogs.IssueList(plan.Warnings));
            var ok = Ui.Btn(plan.NeedsAck ? "Rewrite anyway" : "Rewrite", delegate { go = true; w.Close(); }, "BtnPrimary");
            if (plan.NeedsAck)
            {
                var ack = new CheckBox { Content = "I understand this changes shared history", Margin = new Thickness(0, 4, 0, 0) };
                ok.IsEnabled = false;
                ack.Click += delegate { ok.IsEnabled = ack.IsChecked == true; };
                body.Children.Add(ack);
            }
            w.Layout(body, Ui.Btn("Cancel", w.Close), ok);
            w.ShowDialog();
            if (!go) return;

            bool anyPushed = fresh.Commits.Where(c => sel.Contains(c.Sha)).Any(c => c.Pushed) || plan.Warnings.Any(i => i.Text.Contains("already pushed"));
            try
            {
                History.Rewrite(r.Path, fresh, sel, author, rewriteStrip, rewriteCommitter);
                commitSel.Clear();
                commitCache.Remove(Settings.Norm(r.Path));
                Dialogs.Info(this, "Done", "Rewrote " + plan.RangeCount + " commit(s). Your files didn't change.\n\n" +
                    (anyPushed
                        ? "Next: these were already pushed, so push with Force push in GitHub Desktop (or git push --force-with-lease). Let anyone else on this branch know to reset their copy."
                        : "Next: push as usual from GitHub Desktop.") +
                    "\n\nChanged your mind? Use \"Undo last rewrite\" above the commit list.");
            }
            catch (Exception e) { Dialogs.Info(this, "Rewrite didn't happen", e.Message); }
            Render();
        }

        void ConfirmUncommit(RepoReport r)
        {
            var fresh = History.Read(r.Path);
            commitCache[Settings.Norm(r.Path)] = fresh;
            var sel = new HashSet<string>(commitSel.Where(s => fresh.Commits.Any(c => c.Sha == s)));
            var plan = History.PlanUncommit(r.Path, fresh, sel, settings);
            if (plan.Blockers.Count > 0)
            {
                Dialogs.Ask(this, "Can't undo these commits", Dialogs.IssueList(plan.Blockers.Select(b => new Issue(Level.Error, b))), "OK", null, true);
                return;
            }
            var range = fresh.Commits.Take(plan.RangeCount).ToList();
            var w = new ThemedWindow(this, "Undo commits", 600);
            bool go = false;
            var names = new StackPanel();
            foreach (var c in range.Take(6)) names.Children.Add(Ui.Text("•  " + c.Subject + "  (" + c.Short + ")", 13, "Sub"));
            if (range.Count > 6) names.Children.Add(Ui.Text("•  and " + (range.Count - 6) + " more", 13, "Muted"));
            var body = Ui.Stack(12, Ui.Text("Before you undo", 18, "Text", FontWeights.SemiBold),
                Ui.Text("Take " + range.Count + " commit(s) off " + fresh.Branch + " and keep their changes:", 14, "Sub"), names, Dialogs.IssueList(plan.Warnings));
            var ok = Ui.Btn(plan.NeedsAck ? "Undo anyway" : "Undo commits", delegate { go = true; w.Close(); }, "BtnPrimary");
            if (plan.NeedsAck)
            {
                var ack = new CheckBox { Content = "I understand this changes shared history", Margin = new Thickness(0, 4, 0, 0) };
                ok.IsEnabled = false;
                ack.Click += delegate { ok.IsEnabled = ack.IsChecked == true; };
                body.Children.Add(ack);
            }
            w.Layout(body, Ui.Btn("Cancel", w.Close), ok);
            w.ShowDialog();
            if (!go) return;

            bool anyPushed = range.Any(c => c.Pushed);
            try
            {
                History.Uncommit(r.Path, fresh, sel);
                commitSel.Clear();
                commitCache.Remove(Settings.Norm(r.Path));
                if (range.Count == 1) try { Clipboard.SetText(range[0].Body.Trim().Length > 0 ? range[0].Body.Trim() : range[0].Subject); } catch { }
                Dialogs.Info(this, "Done", "Undid " + range.Count + " commit(s). Their changes are in your folder as uncommitted changes.\n\n" +
                    "Next: in GitHub Desktop, uncheck anything you want to leave out and commit again." +
                    (range.Count == 1 ? " The old commit message is on your clipboard." : "") +
                    (anyPushed ? " These were already pushed, so push with Force push afterwards." : "") +
                    "\n\nChanged your mind? \"Undo last change\" above the commit list puts the commits back.");
            }
            catch (Exception e) { Dialogs.Info(this, "Undo didn't happen", e.Message); }
            Render();
        }

        void UndoRewrite(RepoReport r, BranchState st, Backup b)
        {
            if (!Dialogs.Ask(this, "Put the branch back?", "Puts " + st.Branch + " back exactly as it was on " + b.When + ", before Hatpick's last change. " +
                "Any commits you made since are dropped from the branch (your files stay), and if you already force pushed, you'll need to force push again.", "Put it back")) return;
            try
            {
                History.Undo(r.Path, History.Read(r.Path), b);
                commitCache.Remove(Settings.Norm(r.Path));
            }
            catch (Exception e) { Dialogs.Info(this, "Couldn't undo", e.Message); }
            Render();
        }
    }
}
