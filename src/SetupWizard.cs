using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Hatpick
{
    // First-time setup: what Hatpick is, signing accounts in to Git Credential Manager,
    // adding accounts, and choosing watched folders. Can be run again from Help.
    class SetupWizard : Form
    {
        static int S(int px) { return Program.S(px); }

        readonly Settings settings;
        readonly List<Func<Control>> pages = new List<Func<Control>>();
        int page;
        Panel host;
        Label stepLabel;
        Button back, next, cancel;
        List<string> gcm = new List<string>();

        public bool StartTour, AddExplorerMenu;

        public SetupWizard(Settings settings)
        {
            this.settings = settings;
            Text = "Set up Hatpick";
            Font = Program.UiFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(S(700), S(500));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);
            host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(24), S(18), S(24), S(8)), Margin = Padding.Empty };
            layout.Controls.Add(host);

            var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(S(16), S(10), S(16), S(12)), BackColor = SystemColors.ControlLight, Margin = Padding.Empty };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            stepLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
            cancel = new Button { Text = "Skip setup", AutoSize = true, Height = S(30), Margin = new Padding(0, 0, S(16), 0), TabIndex = 3 };
            back = new Button { Text = "Back", Width = S(90), Height = S(30), TabIndex = 2 };
            next = new Button { Text = "Next", Width = S(110), Height = S(30), TabIndex = 1 };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            back.Click += delegate { Go(page - 1); };
            next.Click += delegate { if (page == pages.Count - 1) Finish(); else Go(page + 1); };
            bar.Controls.Add(stepLabel, 0, 0); bar.Controls.Add(cancel, 1, 0); bar.Controls.Add(back, 2, 0); bar.Controls.Add(next, 3, 0);
            layout.Controls.Add(bar);
            AcceptButton = next;

            pages.Add(Welcome);
            pages.Add(SignIn);
            pages.Add(Accounts);
            pages.Add(Folders);
            pages.Add(Done);
            Shown += delegate { Go(0); };
        }

        void Go(int i)
        {
            page = Math.Max(0, Math.Min(i, pages.Count - 1));
            host.SuspendLayout();
            foreach (Control c in host.Controls.Cast<Control>().ToList()) c.Dispose();
            host.Controls.Clear();
            var content = pages[page]();
            content.Dock = DockStyle.Fill;
            host.Controls.Add(content);
            host.ResumeLayout();
            stepLabel.Text = "Step " + (page + 1) + " of " + pages.Count;
            back.Enabled = page > 0;
            next.Text = page == pages.Count - 1 ? "Finish" : "Next";
            cancel.Visible = page < pages.Count - 1;
            next.Select();   // Enter always means Next, never Skip
        }

        // ---------- building blocks ----------

        TableLayoutPanel Page(string heading, string intro)
        {
            var t = new TableLayoutPanel { ColumnCount = 1, AutoScroll = true };
            t.Controls.Add(new Label { Text = heading, AutoSize = true, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Margin = new Padding(0, 0, 0, S(8)) });
            if (intro != null) t.Controls.Add(Para(intro));
            return t;
        }

        Label Para(string text, bool gray = false)
        {
            return new Label
            {
                Text = text, AutoSize = true, MaximumSize = new Size(S(640), 0), Margin = new Padding(0, 0, 0, S(10)),
                ForeColor = gray ? SystemColors.GrayText : SystemColors.ControlText
            };
        }

        static Button Btn(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true, Height = Program.S(30), Margin = new Padding(0, 0, Program.S(8), 0) };
            b.Click += delegate { onClick(); };
            return b;
        }

        // ---------- pages ----------

        Control Welcome()
        {
            var t = Page("Welcome to Hatpick",
                "Hatpick picks which Git account each repo commits and pushes as - for example Work for your job's repos and " +
                "Personal for your own - and shows you what any repo is set to and why.");
            t.Controls.Add(Para(
                "In the next few steps you'll:\n\n" +
                "  1.  Check which GitHub accounts are signed in to Git Credential Manager\n" +
                "  2.  Add your accounts (a commit name and email, plus a GitHub username)\n" +
                "  3.  Choose the folders where you keep your repos, and a default account for each"));
            var safe = Para(
                "Hatpick never signs in and never sees a password or token. Your sign-ins stay in Git Credential Manager, which comes with Git for Windows. " +
                "Hatpick only writes plain git settings that say which sign-in to use, and everything it changes can be reset.");
            safe.Font = new Font("Segoe UI", 9.5f, FontStyle.Italic);
            t.Controls.Add(safe);
            t.Controls.Add(Para("You can run this setup again any time from Help.", true));
            return t;
        }

        Control SignIn()
        {
            var t = Page("Sign in to Git Credential Manager",
                "Git Credential Manager (GCM) keeps your GitHub sign-ins. Sign in every GitHub account you use once, and GCM remembers it.");
            gcm = Git.CredentialManagerAccounts();
            if (!Git.GcmAvailable)
            {
                t.Controls.Add(Para("Git Credential Manager wasn't found. It comes with Git for Windows - install or update it from git-scm.com, then come back to this step. " +
                                    "You can still finish setup now; pushes will just keep asking for a sign-in."));
                t.Controls.Add(Btn("Check again", delegate { Go(page); }));
                return t;
            }
            t.Controls.Add(new Label { Text = "Signed in right now:", AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(0, S(4), 0, S(4)) });
            var lb = new ListBox { Width = S(320), Height = S(90), IntegralHeight = false };
            foreach (var g in gcm) lb.Items.Add(g);
            if (gcm.Count == 0) lb.Items.Add("(none yet)");
            t.Controls.Add(lb);
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, S(10), 0, S(10)) };
            row.Controls.Add(Btn("Sign in an account...", delegate
            {
                try
                {
                    var p = Git.StartGcmLogin();
                    p.EnableRaisingEvents = true;
                    p.Exited += delegate { try { BeginInvoke((Action)delegate { if (!IsDisposed) Go(page); }); } catch { } };
                }
                catch (Exception e) { MessageBox.Show(this, "Couldn't start Git Credential Manager:\n" + e.Message, Program.Title); }
            }));
            row.Controls.Add(Btn("Refresh", delegate { Go(page); }));
            t.Controls.Add(row);
            t.Controls.Add(Para(
                "\"Sign in an account\" opens a small console window and your browser, where you sign in to GitHub. When the console closes, " +
                "the list above updates. Do it once for each account. If an account is already listed, you can skip this step.", true));
            return t;
        }

        Control Accounts()
        {
            var t = Page("Add your accounts",
                "An account is who you commit and push as. Use the email that's on that GitHub account (GitHub > Settings > Emails), " +
                "or its private noreply address, so GitHub credits your commits.");
            var lv = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, Width = S(640), Height = S(170), MultiSelect = false };
            lv.Columns.Add("Label", S(100)); lv.Columns.Add("Commit name", S(150)); lv.Columns.Add("Commit email", S(220)); lv.Columns.Add("GitHub username", S(150));
            Action fill = delegate
            {
                lv.Items.Clear();
                foreach (var a in settings.accounts)
                {
                    var i = new ListViewItem(a.label);
                    i.SubItems.Add(a.name); i.SubItems.Add(a.email); i.SubItems.Add(a.github);
                    lv.Items.Add(i);
                }
            };
            fill();
            t.Controls.Add(lv);
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, S(8), 0, S(8)) };
            var missing = gcm.Where(g => settings.AccountByGitHub(g) == null).ToList();
            if (missing.Count > 0)
                row.Controls.Add(Btn("Add from signed-in accounts (" + missing.Count + ")...", delegate
                {
                    foreach (var g in gcm.Where(x => settings.AccountByGitHub(x) == null).ToList())
                    {
                        var a = new Account { label = g, name = g, email = "", github = g };
                        if (AccountEditor.Edit(this, a, settings.accounts, null)) settings.accounts.Add(a);
                    }
                    fill();
                }));
            row.Controls.Add(Btn("Add...", delegate
            {
                var a = new Account { label = "", name = "", email = "", github = "" };
                if (AccountEditor.Edit(this, a, settings.accounts, null)) { settings.accounts.Add(a); fill(); }
            }));
            row.Controls.Add(Btn("Edit...", delegate
            {
                if (lv.SelectedIndices.Count == 0) return;
                var a = settings.accounts[lv.SelectedIndices[0]];
                string old = a.label;
                if (AccountEditor.Edit(this, a, settings.accounts, a))
                {
                    foreach (var f in settings.folders.Where(f => f.account == old)) f.account = a.label;
                    fill();
                }
            }));
            row.Controls.Add(Btn("Remove", delegate
            {
                if (lv.SelectedIndices.Count == 0) return;
                var a = settings.accounts[lv.SelectedIndices[0]];
                settings.accounts.Remove(a);
                foreach (var f in settings.folders.Where(f => f.account == a.label)) f.account = "";
                fill();
            }));
            t.Controls.Add(row);
            t.Controls.Add(Para("Tip: the label is just for you - Work and Personal are a good start.", true));
            return t;
        }

        Control Folders()
        {
            var t = Page("Choose your repo folders",
                "Watch the folders where you keep repos. Hatpick lists every repo inside them, and a folder's default account applies to all of " +
                "them - including repos you clone or create there later.");
            t.Controls.Add(Para("Repos that live somewhere else can be added one at a time later, with Add > Add a single repo. " +
                                "A repo's own setting always beats its folder default.", true));
            string gh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub");
            if (settings.folders.Count == 0 && Directory.Exists(gh))
                settings.folders.Add(new WatchedFolder { path = gh, account = "" });   // GitHub Desktop's default location

            var lv = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, Width = S(640), Height = S(150), MultiSelect = false };
            lv.Columns.Add("Folder", S(440)); lv.Columns.Add("Default account", S(180));
            Action fill = delegate
            {
                lv.Items.Clear();
                foreach (var f in settings.folders)
                {
                    var i = new ListViewItem(f.path);
                    i.SubItems.Add(string.IsNullOrEmpty(f.account) ? "(none)" : f.account);
                    lv.Items.Add(i);
                }
            };
            fill();
            t.Controls.Add(lv);
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, S(8), 0, S(8)) };
            row.Controls.Add(Btn("Watch a folder...", delegate
            {
                string p = FoldersForm.PickFolder(this, "Pick a folder that holds your repos");
                if (p != null && FoldersForm.AddWatched(this, settings, p)) fill();
            }));
            Action setDefault = delegate
            {
                if (lv.SelectedIndices.Count == 0) return;
                var f = settings.folders[lv.SelectedIndices[0]];
                string pick;
                if (PickAccount.Ask(this, "Default account for every repo in\n" + f.path, settings.accounts, f.account, out pick)) { f.account = pick; fill(); }
            };
            lv.DoubleClick += delegate { setDefault(); };
            row.Controls.Add(Btn("Default account...", setDefault));
            row.Controls.Add(Btn("Stop watching", delegate
            {
                if (lv.SelectedIndices.Count == 0) return;
                settings.folders.RemoveAt(lv.SelectedIndices[0]);
                fill();
            }));
            t.Controls.Add(row);
            return t;
        }

        CheckBox tour, explorer;

        Control Done()
        {
            var t = Page("You're set", null);
            int defaults = settings.folders.Count(f => !string.IsNullOrEmpty(f.account));
            t.Controls.Add(Para(
                settings.accounts.Count + " account(s), " + settings.folders.Count + " watched folder(s)" +
                (defaults > 0 ? ", " + defaults + " with a default account." : ".") + "\n\n" +
                "Next, Hatpick scans your folders and shows each repo with a status dot. Pick a repo to see who it commits and pushes as, " +
                "and change it if you need to. Look for the ? buttons whenever something isn't clear."));
            tour = new CheckBox { Text = "Show me around the window (about a minute)", AutoSize = true, Checked = true, Margin = new Padding(0, S(6), 0, S(4)) };
            explorer = new CheckBox { Text = "Add \"Analyze with Hatpick\" to Explorer's right-click menu for folders", AutoSize = true, Checked = ExplorerMenu.Installed };
            t.Controls.Add(tour);
            t.Controls.Add(explorer);
            t.Controls.Add(Para("The right-click entry is per-user and needs no admin rights. You can turn it off later under Options.", true));
            return t;
        }

        void Finish()
        {
            StartTour = tour != null && tour.Checked;
            AddExplorerMenu = explorer != null && explorer.Checked;
            DialogResult = DialogResult.OK;
        }
    }
}
