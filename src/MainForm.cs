using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hatpick
{
    partial class MainForm : Form
    {
        static readonly string[] SkipDirs = { "node_modules", "Binaries", "Intermediate", "Saved", "DerivedDataCache", "Library", "obj", "bin" };
        const int MaxDepth = 4;
        const string NotSetHere = "(not set here - use the folder default or global)";

        Settings settings;
        List<RepoReport> reports = new List<RepoReport>();
        List<string> opened = new List<string>();     // analyzed folders that aren't saved in the list
        List<string> gcm = new List<string>();
        bool scanning;
        string pendingSelect;
        readonly string startPath;

        ToolStrip toolbar;
        ToolStripDropDownButton addMenu, resetMenu, optionsMenu;
        ToolStripMenuItem explorerItem;
        SplitContainer split;
        TextBox search;
        ListView list;
        ImageList statusImages;
        Panel reportHost;
        TableLayoutPanel report;
        ToolStripStatusLabel statusText;
        ContextMenuStrip listMenu;

        static int S(int px) { return Program.S(px); }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public MainForm(string startPath)
        {
            this.startPath = startPath;
            Text = Program.Title;
            Font = Program.UiFont;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(1040), S(660));
            MinimumSize = new Size(S(820), S(520));
            AllowDrop = true;
            DragEnter += OnDragEnter; DragDrop += OnDragDrop;

            settings = Settings.Load();
            BuildUi();

            Shown += delegate { OnFirstShown(); };
        }

        // ---------- layout ----------

        void BuildUi()
        {
            statusImages = new ImageList { ImageSize = new Size(S(14), S(14)), ColorDepth = ColorDepth.Depth32Bit };
            foreach (Level l in new[] { Level.Ok, Level.Info, Level.Warn, Level.Error })
                statusImages.Images.Add(l.ToString(), Dot(LevelColor(l), S(14)));

            split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = S(6) };
            Controls.Add(split);

            // left: search + repo list
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(S(10), S(8), 0, S(8)) };
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, S(6)) };
            header.Controls.Add(new Label { Text = "Repos", AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = Padding.Empty });
            header.Controls.Add(HelpUi.Button("list"));
            var foldersHelp = new LinkLabel { Text = "Watched folders vs. single repos", AutoSize = true, Margin = new Padding(S(10), S(3), 0, 0) };
            foldersHelp.LinkClicked += delegate { HelpUi.Show(foldersHelp, "folders"); };
            header.Controls.Add(foldersHelp);
            left.Controls.Add(header, 0, 0);
            search = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, S(6)) };
            search.HandleCreated += delegate { try { SendMessage(search.Handle, 0x1501, (IntPtr)1, "Search repos"); } catch { } };   // EM_SETCUEBANNER placeholder text
            search.TextChanged += delegate { RebuildList(); };
            left.Controls.Add(search, 0, 1);
            list = new ListView
            {
                View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, Dock = DockStyle.Fill,
                SmallImageList = statusImages, ShowGroups = true, AllowDrop = true, Margin = Padding.Empty
            };
            list.Columns.Add("Repository", S(180));
            list.Columns.Add("Account", S(110));
            list.SelectedIndexChanged += delegate { ShowSelected(); };
            list.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) Rescan(); };
            list.DragEnter += OnDragEnter; list.DragDrop += OnDragDrop;
            listMenu = new ContextMenuStrip();
            listMenu.Opening += (s, e) => { e.Cancel = !BuildListMenu(); };
            list.ContextMenuStrip = listMenu;
            left.Controls.Add(list, 0, 2);
            split.Panel1.Controls.Add(left);

            // right: the selected repo's report
            reportHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AllowDrop = true, Padding = new Padding(S(16), S(10), S(16), S(10)) };
            reportHost.DragEnter += OnDragEnter; reportHost.DragDrop += OnDragDrop;
            reportHost.Resize += delegate { FitReport(); };
            report = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = new Point(S(16), S(10)) };
            reportHost.Controls.Add(report);
            split.Panel2.Controls.Add(reportHost);

            toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(S(6), S(3), S(6), S(3)), RenderMode = ToolStripRenderMode.System, Font = Program.UiFont };
            addMenu = new ToolStripDropDownButton("Add");
            addMenu.DropDownItems.Add("Watch a folder of repos...", null, delegate { AddWatchedFolder(); });
            addMenu.DropDownItems.Add("Add a single repo...", null, delegate { AddSingleRepo(); });
            addMenu.DropDownItems.Add(new ToolStripSeparator());
            addMenu.DropDownItems.Add("Analyze any folder (without adding it)...", null, delegate
            {
                string p = FoldersForm.PickFolder(this, "Pick a folder to analyze");
                if (p != null) OpenPath(p);
            });
            toolbar.Items.Add(addMenu);
            accountsButton = new ToolStripButton("Accounts...", null, delegate { EditAccounts(); });
            foldersButton = new ToolStripButton("Watched folders...", null, delegate { EditFolders(); });
            toolbar.Items.Add(accountsButton);
            toolbar.Items.Add(foldersButton);
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(new ToolStripButton("Rescan", null, delegate { Rescan(); }) { ToolTipText = "Look for repos again and re-read their settings (F5)" });
            resetMenu = new ToolStripDropDownButton("Reset");
            resetMenu.DropDownItems.Add("Reset the selected repo", null, delegate { ResetSelected(); });
            resetMenu.DropDownItems.Add("Reset everything Hatpick changed...", null, delegate { ResetAll(); });
            toolbar.Items.Add(resetMenu);
            optionsMenu = new ToolStripDropDownButton("Options") { Alignment = ToolStripItemAlignment.Right };
            explorerItem = new ToolStripMenuItem("Add \"Analyze with Hatpick\" to Explorer's right-click menu") { CheckOnClick = true };
            explorerItem.Click += delegate { ToggleExplorerMenu(); };
            optionsMenu.DropDownItems.Add(explorerItem);
            optionsMenu.DropDownOpening += delegate { explorerItem.Checked = ExplorerMenu.Installed; };
            toolbar.Items.Add(optionsMenu);
            BuildHelpMenu();
            Controls.Add(toolbar);

            var status = new StatusStrip { SizingGrip = true };
            statusText = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            status.Items.Add(statusText);
            Controls.Add(status);

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) Rescan(); };
            split.SplitterDistance = S(320);
            ShowEmpty();
        }

        static Color LevelColor(Level l)
        {
            switch (l)
            {
                case Level.Ok: return Color.FromArgb(46, 160, 90);
                case Level.Info: return Color.FromArgb(70, 130, 200);
                case Level.Warn: return Color.FromArgb(222, 158, 28);
                default: return Color.FromArgb(206, 60, 60);
            }
        }

        static Bitmap Dot(Color c, int size)
        {
            var b = new Bitmap(size, size);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int pad = Math.Max(1, size / 7);
                using (var br = new SolidBrush(c)) g.FillEllipse(br, pad, pad, size - 2 * pad - 1, size - 2 * pad - 1);
            }
            return b;
        }

        // ---------- startup ----------

        void OnFirstShown()
        {
            if (!Settings.Exists && Settings.OldExists)
            {
                settings.Save();
                SyncFolders();    // move folder defaults over from an earlier name
            }
            if (NeedsSetup) RunSetup();   // first time: walk through accounts and folders
            else Rescan();
            if (startPath != null) OpenPath(startPath);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WM_COPYDATA)
            {
                string path = Program.ReadCopyData(m.LParam);
                BeginInvoke((Action)delegate { if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); OpenPath(path); });
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- scanning ----------

        class Target { public string Path; public RepoSource Source; public string Watched; }

        void Rescan()
        {
            if (scanning) return;
            scanning = true;
            statusText.Text = "Scanning...";
            UseWaitCursor = true;
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
                UseWaitCursor = false;
                if (IsDisposed) return;
                if (t.Exception != null) { statusText.Text = "Scan failed: " + t.Exception.InnerException.Message; return; }
                gcm = t.Result.Item1;
                string keep = pendingSelect ?? SelectedPath();
                pendingSelect = null;
                reports = t.Result.Item2;
                RebuildList(keep);
                UpdateStatus();
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
            RebuildList(old.Path);
            UpdateStatus();
        }

        void UpdateStatus()
        {
            int attention = reports.Count(r => r.Level >= Level.Warn);
            string g = !Git.GcmAvailable ? "Git Credential Manager not found"
                : gcm.Count == 0 ? "Git Credential Manager: no GitHub accounts signed in"
                : "Git Credential Manager: " + string.Join(", ", gcm) + " signed in";
            statusText.Text = reports.Count + " repos  ·  " + (attention == 0 ? "none need attention" : attention + " need attention") + "  ·  " + g;
        }

        // ---------- list ----------

        string SelectedPath()
        {
            var r = SelectedReport();
            return r != null ? r.Path : null;
        }

        RepoReport SelectedReport()
        {
            return list.SelectedItems.Count > 0 ? (RepoReport)list.SelectedItems[0].Tag : null;
        }

        string AccountText(RepoReport r)
        {
            if (r.Error != null) return "";
            var a = settings.AccountByEmail(r.Email);
            if (a != null) return a.label;
            return r.Email.Length > 0 ? r.Email : "(none)";
        }

        void RebuildList() { RebuildList(SelectedPath()); }

        void RebuildList(string select)
        {
            string q = search.Text.Trim();
            list.BeginUpdate();
            list.Items.Clear();
            list.Groups.Clear();
            var groups = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in settings.folders)
            {
                string def = string.IsNullOrEmpty(f.account) ? "no default" : "default: " + f.account;
                var g = new ListViewGroup(f.path + "   (" + def + ")");
                groups[Settings.Norm(f.path)] = g;
                list.Groups.Add(g);
            }
            var single = new ListViewGroup("Single repos");
            var openedGroup = new ListViewGroup("Analyzed, not in your list");
            list.Groups.Add(single);
            list.Groups.Add(openedGroup);

            foreach (var r in reports)
            {
                string acct = AccountText(r);
                if (q.Length > 0 && r.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    acct.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && r.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var item = new ListViewItem(r.DisplayName) { Tag = r, ImageKey = r.Level.ToString(), ToolTipText = r.Path };
                item.SubItems.Add(acct);
                ListViewGroup g;
                if (r.Source == RepoSource.Watched && groups.TryGetValue(Settings.Norm(r.WatchedFolder), out g)) item.Group = g;
                else if (r.Source == RepoSource.Opened) item.Group = openedGroup;
                else item.Group = single;
                list.Items.Add(item);
                if (select != null && Settings.SamePath(select, r.Path)) { item.Selected = true; item.Focused = true; }
            }
            list.EndUpdate();
            if (list.SelectedItems.Count > 0) list.SelectedItems[0].EnsureVisible();
            ShowSelected();
        }

        bool BuildListMenu()
        {
            listMenu.Items.Clear();
            var r = SelectedReport();
            if (r == null) return false;
            listMenu.Items.Add("Open folder", null, delegate { OpenFolder(r); });
            if (r.Managed) listMenu.Items.Add("Reset this repo", null, delegate { ResetSelected(); });
            listMenu.Items.Add(new ToolStripSeparator());
            if (r.Source == RepoSource.Single) listMenu.Items.Add("Remove from list", null, delegate { RemoveSingle(r); });
            if (r.Source == RepoSource.Opened) listMenu.Items.Add("Keep in list", null, delegate { KeepOpened(r); });
            if (r.Source == RepoSource.Watched) listMenu.Items.Add("Watched folder settings...", null, delegate { EditFolders(); });
            return true;
        }

        // ---------- the report panel ----------

        void ShowSelected()
        {
            var r = SelectedReport();
            if (r == null) ShowEmpty(); else ShowReport(r);
        }

        void FitReport()
        {
            int w = Math.Max(S(380), reportHost.ClientSize.Width - S(32));
            report.MaximumSize = new Size(w, 0);
            report.MinimumSize = new Size(w, 0);
        }

        Label Lbl(string text, float size = 9.5f, FontStyle style = FontStyle.Regular, Color? color = null, int top = 0)
        {
            return new Label
            {
                Text = text, AutoSize = true, Font = new Font("Segoe UI", size, style),
                ForeColor = color ?? SystemColors.ControlText, Margin = new Padding(0, S(top), 0, 0),
                MaximumSize = new Size(Math.Max(S(300), reportHost.ClientSize.Width - S(60)), 0)
            };
        }

        void ShowEmpty()
        {
            report.SuspendLayout();
            report.Controls.Clear();
            FitReport();
            report.Controls.Add(Lbl("No repo selected", 14f, FontStyle.Bold));
            report.Controls.Add(Lbl("Pick a repo on the left to see who it commits and pushes as, and why.\n\n" +
                                     "You can also drop any folder onto this window to analyze it.", 10f, FontStyle.Regular, SystemColors.GrayText, 8));
            report.ResumeLayout();
        }

        protected virtual void ShowReport(RepoReport r)
        {
            report.SuspendLayout();
            report.Controls.Clear();
            FitReport();

            report.Controls.Add(Lbl(r.DisplayName, 15f, FontStyle.Bold));
            report.Controls.Add(Lbl(r.Path, 9f, FontStyle.Regular, SystemColors.GrayText));
            report.Controls.Add(Banner(r));

            if (r.Error == null)
            {
                var facts = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, S(10), 0, 0) };
                facts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(160)));
                facts.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                AddFact(facts, "remote", "Remote", r.RemoteUrl.Length > 0 ? r.RemoteUrl : "(none yet)", null);
                var acct = settings.AccountByEmail(r.Email);
                string commits = r.Email.Length == 0 && r.Name.Length == 0 ? "(not set)"
                    : (acct != null ? acct.label + "  -  " : "") + r.Name + " <" + r.Email + ">";
                AddFact(facts, "commits", "Commits as", commits, r.IdentitySetBy);
                string pushes, pushBy = r.PushSetBy;
                if (r.IsSsh) { pushes = "Your SSH key"; pushBy = null; }
                else if (r.IsLocal) { pushes = "No sign-in needed (local remote)"; pushBy = null; }
                else if (r.Host.Length > 0 && !r.IsGitHubHttps) { pushes = "Not managed by Hatpick (" + r.Host + ")"; pushBy = null; }
                else if (r.PushUser.Length == 0) pushes = Git.GcmAvailable && gcm.Count == 1 ? "(not set - GCM uses " + gcm[0] + ")" : "(not set - GCM will ask)";
                else
                {
                    var pa = settings.AccountByGitHub(r.PushUser);
                    pushes = (pa != null ? pa.label + "  -  " : "") + r.PushUser;
                }
                AddFact(facts, "pushes", "Pushes as", pushes, pushBy);
                string signed = r.SignedIn == null ? (Git.GcmAvailable ? "-" : "Unknown - Git Credential Manager wasn't found")
                    : r.SignedIn.Value ? "Yes, Git Credential Manager has a sign-in for " + r.PushUser
                    : "No - the first push will open a browser to sign in";
                AddFact(facts, "signedin", "Signed in", signed, null);
                AddFact(facts, "changed", "Changed by Hatpick", r.Managed ? "Yes - Reset puts back what the repo had before" : "No", null);
                report.Controls.Add(facts);

                foreach (var issue in r.Issues.OrderByDescending(i => i.Level))
                    report.Controls.Add(IssueRow(issue));

                report.Controls.Add(ChangeBox(r));
            }
            report.Controls.Add(ActionRow(r));
            report.ResumeLayout();
            reportHost.AutoScrollPosition = Point.Empty;
        }

        Control HelpButtonFor(string topic) { return HelpUi.Button(topic); }

        void AddFact(TableLayoutPanel t, string topic, string caption, string value, string setBy)
        {
            var cap = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, S(6), S(8), 0) };
            cap.Controls.Add(new Label { Text = caption, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = Padding.Empty });
            var help = HelpButtonFor(topic);
            if (help != null) cap.Controls.Add(help);
            t.Controls.Add(cap);
            var v = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, S(6), 0, 0) };
            v.Controls.Add(new Label { Text = value, AutoSize = true, Margin = Padding.Empty, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), MaximumSize = new Size(Math.Max(S(240), reportHost.ClientSize.Width - S(220)), 0) });
            if (!string.IsNullOrEmpty(setBy))
                v.Controls.Add(new Label { Text = "Set by: " + setBy, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = Padding.Empty });
            t.Controls.Add(v);
        }

        Control Banner(RepoReport r)
        {
            string title, sub;
            switch (r.Level)
            {
                case Level.Ok: title = "All set"; sub = "Commits and pushes use the account you'd expect."; break;
                case Level.Info: title = "Looks fine"; sub = "A few notes below."; break;
                case Level.Warn: title = "Needs attention"; sub = "See the warnings below."; break;
                default: title = r.Error != null ? "Can't read this repo" : "Not set up"; sub = r.Error ?? "Commits will fail until an account is set."; break;
            }
            var p = new Panel { Height = S(52), Margin = new Padding(0, S(10), 0, 0), BackColor = Blend(LevelColor(r.Level), 0.14f) };
            p.Width = report.MaximumSize.Width;
            p.Paint += (s, e) => { using (var b = new SolidBrush(LevelColor(r.Level))) e.Graphics.FillRectangle(b, 0, 0, S(5), p.Height); };
            p.Controls.Add(new Label { Text = sub, AutoSize = true, Location = new Point(S(16), S(28)), BackColor = Color.Transparent });
            p.Controls.Add(new Label { Text = title, AutoSize = true, Location = new Point(S(16), S(7)), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), BackColor = Color.Transparent });
            return p;
        }

        static Color Blend(Color c, float amount)
        {
            Color b = SystemColors.Window;
            return Color.FromArgb((int)(b.R + (c.R - b.R) * amount), (int)(b.G + (c.G - b.G) * amount), (int)(b.B + (c.B - b.B) * amount));
        }

        Control IssueRow(Issue issue)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, S(8), 0, 0) };
            row.Controls.Add(new PictureBox { Image = statusImages.Images[issue.Level.ToString()], Size = statusImages.ImageSize, Margin = new Padding(0, S(2), S(6), 0) });
            row.Controls.Add(new Label { Text = issue.Text, AutoSize = true, Margin = Padding.Empty, MaximumSize = new Size(Math.Max(S(300), reportHost.ClientSize.Width - S(90)), 0) });
            return row;
        }

        Control ChangeBox(RepoReport r)
        {
            var box = new GroupBox { Text = "Set this repo", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(S(10), S(6), S(10), S(10)), Margin = new Padding(0, S(16), 0, 0) };
            var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = new Point(S(10), S(24)) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(330)));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var commit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            commit.Items.Add(NotSetHere);
            foreach (var a in settings.accounts) commit.Items.Add(a.label + "  -  " + a.email);
            var localAcct = r.LocalIdentity ? settings.AccountByEmail(r.Email) : null;
            commit.SelectedIndex = localAcct != null ? settings.accounts.IndexOf(localAcct) + 1 : 0;

            var push = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            push.Items.Add(NotSetHere);
            var users = settings.accounts.Where(a => !string.IsNullOrEmpty(a.github)).Select(a => a.github)
                .Concat(gcm).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (r.LocalPush && r.PushUser.Length > 0 && !users.Contains(r.PushUser, StringComparer.OrdinalIgnoreCase)) users.Add(r.PushUser);
            foreach (var u in users) push.Items.Add(u);
            push.SelectedIndex = r.LocalPush ? Math.Max(0, users.FindIndex(u => string.Equals(u, r.PushUser, StringComparison.OrdinalIgnoreCase)) + 1) : 0;

            commit.SelectedIndexChanged += delegate
            {
                if (commit.SelectedIndex <= 0) return;
                var a = settings.accounts[commit.SelectedIndex - 1];
                int i = users.FindIndex(u => string.Equals(u, a.github, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) push.SelectedIndex = i + 1;
            };

            t.Controls.Add(new Label { Text = "Commit as:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(8), 0) }, 0, 0);
            t.Controls.Add(commit, 1, 0);
            var h1 = HelpButtonFor("setcommit"); if (h1 != null) t.Controls.Add(h1, 2, 0);
            t.Controls.Add(new Label { Text = "Push as:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(8), 0) }, 0, 1);
            t.Controls.Add(push, 1, 1);
            var h2 = HelpButtonFor("setpush"); if (h2 != null) t.Controls.Add(h2, 2, 1);

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, S(10), 0, 0) };
            var apply = new Button { Text = "Apply", Width = S(100), Height = S(30) };
            apply.Click += delegate
            {
                var a = commit.SelectedIndex > 0 ? settings.accounts[commit.SelectedIndex - 1] : null;
                string u = push.SelectedIndex > 0 ? users[push.SelectedIndex - 1] : null;
                Apply(r, a, u);
            };
            buttons.Controls.Add(apply);
            var reset = new Button { Text = "Reset this repo", AutoSize = true, Height = S(30), Enabled = r.Managed };
            reset.Click += delegate { ResetSelected(); };
            buttons.Controls.Add(reset);
            t.Controls.Add(buttons, 1, 2);
            t.SetColumnSpan(buttons, 2);
            var note = new Label
            {
                Text = "Only changes this repo's own .git/config. Reset puts back whatever the repo had before Hatpick first changed it.",
                AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(S(480), 0), Margin = new Padding(0, S(6), 0, 0)
            };
            t.Controls.Add(note, 1, 3);
            t.SetColumnSpan(note, 2);
            box.Controls.Add(t);
            return box;
        }

        Control ActionRow(RepoReport r)
        {
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, S(14), 0, S(10)) };
            Action<string, Action> add = (text, act) =>
            {
                var b = new Button { Text = text, AutoSize = true, Height = S(30), Margin = new Padding(0, 0, S(8), 0) };
                b.Click += delegate { act(); };
                row.Controls.Add(b);
            };
            add("Open folder", () => OpenFolder(r));
            add("Open in GitHub Desktop", () => OpenInDesktop(r));
            if (Git.GitBash != null) add("Git Bash here", () => OpenGitBash(r));
            if (r.Source == RepoSource.Opened) add("Keep in list", () => KeepOpened(r));
            if (r.Source == RepoSource.Single) add("Remove from list", () => RemoveSingle(r));
            return row;
        }

        // ---------- actions ----------

        void Apply(RepoReport r, Account commitAs, string pushAs)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                Git.Assign(r.Path, commitAs, pushAs);
                settings.MarkManaged(r.Path);
                settings.Save();
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { Cursor = Cursors.Default; }
            Reanalyze(r.Path);
        }

        void ResetSelected()
        {
            var r = SelectedReport();
            if (r == null) return;
            if (!r.Managed) { MessageBox.Show(this, "Hatpick hasn't changed " + r.DisplayName + ", so there's nothing to reset.", Program.Title); return; }
            if (MessageBox.Show(this, "Reset " + r.DisplayName + "?\n\nIts commit name, email and push account go back to what the repo had before Hatpick first changed it.",
                Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { Git.Reset(r.Path); settings.UnmarkManaged(r.Path); settings.Save(); }
            catch (Exception e) { MessageBox.Show(this, e.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            Reanalyze(r.Path);
        }

        void ResetAll()
        {
            var managed = settings.managed.Concat(reports.Where(r => r.Managed).Select(r => r.Path))
                .Select(Settings.Norm).Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).OrderBy(p => p).ToList();
            int folderDefaults = settings.folders.Count(f => settings.AccountByLabel(f.account) != null);
            using (var f = new ResetForm(managed, folderDefaults))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                var errors = new List<string>();
                Cursor = Cursors.WaitCursor;
                foreach (var p in f.ChosenRepos)
                {
                    try { Git.Reset(p); settings.UnmarkManaged(p); }
                    catch (Exception e) { errors.Add(Path.GetFileName(p) + ": " + e.Message); }
                }
                settings.managed.RemoveAll(m => !Directory.Exists(m));
                int removed = 0;
                if (f.ChosenFolders)
                {
                    foreach (var w in settings.folders) w.account = "";
                    try
                    {
                        removed = Git.RemoveFolderDefaults();
                        foreach (var file in Directory.GetFiles(Git.IncludeDir, "*.gitconfig")) File.Delete(file);
                    }
                    catch (Exception e) { errors.Add("Folder defaults: " + e.Message); }
                }
                settings.Save();
                Cursor = Cursors.Default;
                string msg = "Reset " + (f.ChosenRepos.Count - errors.Count(e => !e.StartsWith("Folder"))) + " repo(s)" +
                             (f.ChosenFolders ? " and removed " + removed + " folder default(s)." : ".");
                if (errors.Count > 0) msg += "\n\nThese didn't work:\n" + string.Join("\n", errors);
                MessageBox.Show(this, msg, Program.Title);
                Rescan();
            }
        }

        void OpenFolder(RepoReport r)
        {
            if (Directory.Exists(r.Path)) Process.Start("explorer.exe", "\"" + r.Path + "\"");
        }

        void OpenInDesktop(RepoReport r)
        {
            try { Process.Start("x-github-client://openLocalRepo/" + Uri.EscapeDataString(r.Path)); }
            catch (Exception e) { MessageBox.Show(this, "Couldn't open GitHub Desktop:\n" + e.Message, Program.Title); }
        }

        void OpenGitBash(RepoReport r)
        {
            try { Process.Start(new ProcessStartInfo(Git.GitBash, "--cd=\"" + r.Path + "\"") { UseShellExecute = false }); }
            catch (Exception e) { MessageBox.Show(this, "Couldn't open Git Bash:\n" + e.Message, Program.Title); }
        }

        void AddWatchedFolder()
        {
            string p = FoldersForm.PickFolder(this, "Pick a folder that holds your repos. Hatpick lists every repo inside it.");
            if (p == null) return;
            if (FoldersForm.AddWatched(this, settings, p)) { settings.Save(); SyncFolders(); Rescan(); }
        }

        void AddSingleRepo()
        {
            string p = FoldersForm.PickFolder(this, "Pick a repo folder (or any folder inside one)");
            if (p == null) return;
            string top = Git.Toplevel(p);
            if (top == null) { MessageBox.Show(this, "That folder isn't inside a git repo.", Program.Title); return; }
            AddSingle(top);
        }

        void AddSingle(string top)
        {
            if (!settings.repos.Any(x => Settings.SamePath(x, top))) settings.repos.Add(top);
            opened.RemoveAll(x => Settings.SamePath(x, top));
            settings.Save();
            pendingSelect = top;
            Rescan();
        }

        void RemoveSingle(RepoReport r)
        {
            settings.repos.RemoveAll(p => Settings.SamePath(p, r.Path));
            settings.Save();
            string extra = r.Managed ? "\n\nHatpick changed this repo's settings. They stay unless you reset it first." : "";
            if (r.Managed && MessageBox.Show(this, "Removed " + r.DisplayName + " from the list." + extra + "\n\nReset it now?", Program.Title, MessageBoxButtons.YesNo) == DialogResult.Yes)
            { try { Git.Reset(r.Path); settings.UnmarkManaged(r.Path); settings.Save(); } catch { } }
            Rescan();
        }

        void KeepOpened(RepoReport r) { AddSingle(r.Path); }

        // Analyze any folder: a repo (or a folder inside one) is shown right away; a plain
        // folder full of repos is offered as a watched folder.
        public void OpenPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path)) { MessageBox.Show(this, "Folder not found:\n" + path, Program.Title); return; }
            string top = Git.Toplevel(path);
            if (top != null)
            {
                var existing = reports.FirstOrDefault(x => Settings.SamePath(x.Path, top));
                if (existing != null) { search.Text = ""; RebuildList(existing.Path); return; }
                if (!opened.Any(x => Settings.SamePath(x, top))) opened.Add(top);
                pendingSelect = top;
                Rescan();
                return;
            }
            var hits = new List<string>();
            Walk(path, 0, hits);
            if (hits.Count == 0) { MessageBox.Show(this, "No git repos in\n" + path, Program.Title); return; }
            if (MessageBox.Show(this, path + " isn't a repo itself, but it has " + hits.Count + " repo(s) inside.\n\nWatch this folder? Hatpick will list all of them, and you can give the folder a default account.",
                Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (FoldersForm.AddWatched(this, settings, path)) { settings.Save(); SyncFolders(); Rescan(); }
            }
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null) return;
            foreach (var p in paths)
            {
                string dir = Directory.Exists(p) ? p : Path.GetDirectoryName(p);
                BeginInvoke((Action)delegate { OpenPath(dir); });
            }
        }

        void ToggleExplorerMenu()
        {
            try
            {
                if (explorerItem.Checked) ExplorerMenu.Install(Program.ExePath);
                else ExplorerMenu.Remove();
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, Program.Title); }
            explorerItem.Checked = ExplorerMenu.Installed;
        }

        void SyncFolders()
        {
            try { Git.SyncFolderDefaults(settings); }
            catch (Exception e) { MessageBox.Show(this, e.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        protected void EditAccounts()
        {
            var before = settings.accounts.Select(a => new Account { label = a.label, name = a.name, email = a.email, github = a.github }).ToList();
            Dictionary<string, string> renames;
            using (var f = new AccountsForm(settings.accounts)) { f.ShowDialog(this); renames = f.Renames; }

            // Carry label renames over to folder defaults; drop defaults for removed accounts.
            foreach (var w in settings.folders)
                if (!string.IsNullOrEmpty(w.account))
                {
                    string renamed;
                    if (renames.TryGetValue(w.account, out renamed)) w.account = renamed;
                    if (settings.AccountByLabel(w.account) == null) w.account = "";
                }

            // Repos set to an account whose details changed get the new details.
            int updated = 0;
            foreach (var old in before)
            {
                string newLabel;
                if (!renames.TryGetValue(old.label, out newLabel)) newLabel = old.label;
                var now = settings.AccountByLabel(newLabel);
                if (now == null) continue;
                if (now.name == old.name && now.email == old.email && now.github == old.github) continue;
                foreach (var r in reports.Where(x => x.LocalIdentity && string.Equals(x.Email, old.email, StringComparison.OrdinalIgnoreCase)))
                {
                    string push = !r.LocalPush ? null : string.Equals(r.PushUser, old.github, StringComparison.OrdinalIgnoreCase) ? now.github : r.PushUser;
                    try { Git.Assign(r.Path, now, push); settings.MarkManaged(r.Path); updated++; } catch { }
                }
            }
            settings.Save();
            SyncFolders();
            Rescan();
            if (updated > 0) MessageBox.Show(this, "Updated " + updated + " repo(s) to the edited account details.", Program.Title);
        }

        protected void EditFolders()
        {
            using (var f = new FoldersForm(settings)) f.ShowDialog(this);
            settings.Save();
            SyncFolders();
            Rescan();
        }
    }
}
