using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Hatpick
{
    class DialogBase : Form
    {
        protected static int S(int px) { return Program.S(px); }
        protected TableLayoutPanel Body;

        protected DialogBase(string title, int w, int h)
        {
            Text = title;
            Font = Program.UiFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(S(w), S(h));
            Body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(S(12)) };
            Controls.Add(Body);
        }

        protected Button MakeButton(string text, Action onClick)
        {
            var b = new Button { Text = text, Width = S(140), Height = S(32), Margin = new Padding(0, 0, 0, S(6)) };
            b.Click += delegate { onClick(); };
            return b;
        }

        protected FlowLayoutPanel OkCancel(string okText, Action onOk)
        {
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, S(12), 0, 0) };
            var cancel = new Button { Text = "Cancel", Width = S(90), Height = S(30), DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = okText, AutoSize = true, MinimumSize = new Size(S(90), S(30)) };
            ok.Click += delegate { onOk(); };
            row.Controls.Add(cancel); row.Controls.Add(ok);
            AcceptButton = ok; CancelButton = cancel;
            return row;
        }
    }

    // List + buttons dialog used for Accounts and Watched folders.
    abstract class ListDialog : DialogBase
    {
        protected ListView List;
        protected FlowLayoutPanel Buttons;

        protected ListDialog(string title, string note) : base(title, 760, 380)
        {
            Body.ColumnCount = 2; Body.RowCount = 2;
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var lbl = new Label { Text = note, AutoSize = true, MaximumSize = new Size(S(720), 0), Margin = new Padding(0, 0, 0, S(10)) };
            Body.Controls.Add(lbl, 0, 0);
            Body.SetColumnSpan(lbl, 2);
            List = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, S(10), 0) };
            Body.Controls.Add(List, 0, 1);
            Buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            Body.Controls.Add(Buttons, 1, 1);
        }

        protected int SelectedIndex { get { return List.SelectedIndices.Count > 0 ? List.SelectedIndices[0] : -1; } }

        protected void Select(int i)
        {
            if (List.Items.Count == 0) return;
            i = Math.Max(0, Math.Min(i, List.Items.Count - 1));
            List.Items[i].Selected = true; List.Items[i].Focused = true;
        }
    }

    class AccountsForm : ListDialog
    {
        // old label -> new label, for renames made while this dialog was open
        public Dictionary<string, string> Renames = new Dictionary<string, string>();
        readonly List<Account> accounts;

        public AccountsForm(List<Account> accounts)
            : base("Accounts",
                "An account is who you commit and push as: a commit name and email, plus the GitHub username " +
                "Git Credential Manager signs in as. Hatpick never asks for a password - sign-ins stay in Git Credential Manager. " +
                "Editing an account updates every repo already set to it.")
        {
            this.accounts = accounts;
            List.Columns.Add("Label", S(100));
            List.Columns.Add("Commit name", S(150));
            List.Columns.Add("Commit email", S(200));
            List.Columns.Add("GitHub username", S(130));
            List.DoubleClick += delegate { Edit(); };
            Buttons.Controls.Add(MakeButton("Add...", Add));
            Buttons.Controls.Add(MakeButton("Edit...", Edit));
            Buttons.Controls.Add(MakeButton("Remove", Remove));
            var close = MakeButton("Close", Close);
            Buttons.Controls.Add(close);
            CancelButton = close;
            Rebuild(0);
        }

        void Rebuild(int sel)
        {
            List.Items.Clear();
            foreach (var a in accounts)
            {
                var i = new ListViewItem(a.label);
                i.SubItems.Add(a.name); i.SubItems.Add(a.email); i.SubItems.Add(a.github);
                List.Items.Add(i);
            }
            Select(sel);
        }

        void Add()
        {
            var a = new Account { label = "", name = "", email = "", github = "" };
            if (AccountEditor.Edit(this, a, accounts, null)) { accounts.Add(a); Rebuild(accounts.Count - 1); }
        }

        void Edit()
        {
            int i = SelectedIndex; if (i < 0) return;
            var a = accounts[i];
            string oldLabel = a.label;
            if (AccountEditor.Edit(this, a, accounts, a))
            {
                if (a.label != oldLabel)
                {
                    // Chain renames: A -> B -> C maps A to C.
                    var origin = Renames.FirstOrDefault(kv => kv.Value == oldLabel).Key ?? oldLabel;
                    Renames[origin] = a.label;
                }
                Rebuild(i);
            }
        }

        void Remove()
        {
            int i = SelectedIndex; if (i < 0) return;
            var a = accounts[i];
            if (MessageBox.Show(this, "Remove '" + a.label + "' from Hatpick?\n\nRepos already set to it keep their settings until you change or reset them. Folder defaults that use it are cleared.\n\nThis doesn't sign the account out of Git Credential Manager.",
                "Remove account", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            accounts.RemoveAt(i);
            Rebuild(i);
        }
    }

    class AccountEditor : DialogBase
    {
        readonly Account target;
        readonly List<Account> all;
        readonly Account self;
        TextBox label, name, email;
        ComboBox github;

        AccountEditor(Account target, List<Account> all, Account self)
            : base(self == null ? "Add account" : "Edit account", 520, 330)
        {
            this.target = target; this.all = all; this.self = self;
            Body.ColumnCount = 2;
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            label = AddField("Label:", target.label, "What Hatpick calls it, e.g. Work or Personal.");
            name = AddField("Commit name:", target.name, "Shown as the author of your commits.");
            email = AddField("Commit email:", target.email, "GitHub links commits to the account that has this email.");

            Body.Controls.Add(new Label { Text = "GitHub username:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(8), 0) });
            github = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, Text = target.github ?? "" };
            foreach (var g in Git.CredentialManagerAccounts()) github.Items.Add(g);
            Body.Controls.Add(github);
            Body.Controls.Add(new Label());
            Body.Controls.Add(new Label
            {
                Text = github.Items.Count > 0 ? "The list shows accounts already signed in to Git Credential Manager." : "Who Git Credential Manager signs in as when you push.",
                AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(S(3), 0, 0, S(6))
            });

            var row = OkCancel("OK", Save);
            Body.Controls.Add(row);
            Body.SetColumnSpan(row, 2);
        }

        TextBox AddField(string caption, string value, string hint)
        {
            Body.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(8), 0) });
            var t = new TextBox { Dock = DockStyle.Fill, Text = value ?? "" };
            Body.Controls.Add(t);
            Body.Controls.Add(new Label());
            Body.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(S(3), 0, 0, S(6)) });
            return t;
        }

        void Save()
        {
            string l = label.Text.Trim(), n = name.Text.Trim(), e = email.Text.Trim(), g = github.Text.Trim();
            if (l.Length == 0 || n.Length == 0 || e.Length == 0)
            { MessageBox.Show(this, "Label, commit name and commit email are all needed.", Program.Title); return; }
            if (all.Any(a => a != self && string.Equals(a.label, l, StringComparison.OrdinalIgnoreCase)))
            { MessageBox.Show(this, "There's already an account called '" + l + "'.", Program.Title); return; }
            if (all.Any(a => a != self && string.Equals(a.email, e, StringComparison.OrdinalIgnoreCase)))
            { MessageBox.Show(this, "Another account already uses " + e + ". Hatpick tells accounts apart by email, so each needs its own.", Program.Title); return; }
            target.label = l; target.name = n; target.email = e; target.github = g;
            DialogResult = DialogResult.OK;
        }

        public static bool Edit(IWin32Window owner, Account target, List<Account> all, Account self)
        {
            using (var f = new AccountEditor(target, all, self)) return f.ShowDialog(owner) == DialogResult.OK;
        }
    }

    class FoldersForm : ListDialog
    {
        readonly Settings settings;

        public FoldersForm(Settings settings)
            : base("Watched folders",
                "Watch the folders where you keep your repos, like Documents\\GitHub. Hatpick lists every repo inside them. " +
                "Give a folder a default account and every repo in it uses that account - including repos you clone or create there later - " +
                "unless you set a repo differently. Repos that live somewhere else can be added one at a time with Add > Single repo.")
        {
            this.settings = settings;
            List.Columns.Add("Folder", S(420));
            List.Columns.Add("Default account", S(150));
            List.DoubleClick += delegate { ChangeDefault(); };
            Buttons.Controls.Add(MakeButton("Watch a folder...", Add));
            Buttons.Controls.Add(MakeButton("Default account...", ChangeDefault));
            Buttons.Controls.Add(MakeButton("Stop watching", Remove));
            var close = MakeButton("Close", Close);
            Buttons.Controls.Add(close);
            CancelButton = close;
            Rebuild(0);
        }

        void Rebuild(int sel)
        {
            List.Items.Clear();
            foreach (var f in settings.folders)
            {
                var i = new ListViewItem(f.path);
                i.SubItems.Add(string.IsNullOrEmpty(f.account) ? "(none)" : f.account);
                List.Items.Add(i);
            }
            Select(sel);
        }

        void Add()
        {
            string path = PickFolder(this, "Pick a folder that holds your repos");
            if (path == null) return;
            AddWatched(this, settings, path);
            Rebuild(settings.folders.Count - 1);
        }

        public static string PickFolder(IWin32Window owner, string description)
        {
            using (var d = new FolderBrowserDialog { Description = description, ShowNewFolderButton = false })
                return d.ShowDialog(owner) == DialogResult.OK ? d.SelectedPath : null;
        }

        public static bool AddWatched(IWin32Window owner, Settings settings, string path)
        {
            if (settings.folders.Any(f => Settings.SamePath(f.path, path))) return false;
            var nf = new WatchedFolder { path = Settings.Norm(path), account = "" };
            string pick;
            if (settings.accounts.Count > 0 &&
                PickAccount.Ask(owner, "Default account for every repo in\n" + nf.path + "\n\nYou can still set single repos differently.", settings.accounts, "", out pick))
                nf.account = pick;
            settings.folders.Add(nf);
            return true;
        }

        void ChangeDefault()
        {
            int i = SelectedIndex; if (i < 0) return;
            var f = settings.folders[i];
            string pick;
            if (PickAccount.Ask(this, "Default account for every repo in\n" + f.path, settings.accounts, f.account, out pick))
            { f.account = pick; Rebuild(i); }
        }

        void Remove()
        {
            int i = SelectedIndex; if (i < 0) return;
            if (MessageBox.Show(this, "Stop watching " + settings.folders[i].path + "?\n\nIts repos leave the list and its default account stops applying. Accounts you set on single repos inside it stay.",
                Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            settings.folders.RemoveAt(i);
            Rebuild(i);
        }
    }

    class PickAccount : DialogBase
    {
        ComboBox combo;

        PickAccount(string prompt, List<Account> accounts, string current) : base("Default account", 460, 210)
        {
            Body.ColumnCount = 1;
            Body.Controls.Add(new Label { Text = prompt, AutoSize = true, MaximumSize = new Size(S(430), 0) });
            combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, S(8), 0, 0) };
            combo.Items.Add("(no default - use your global settings)");
            foreach (var a in accounts) combo.Items.Add(a.label + "    " + a.email);
            int idx = accounts.FindIndex(a => a.label == current);
            combo.SelectedIndex = idx + 1;
            Body.Controls.Add(combo);
            Body.Controls.Add(OkCancel("OK", delegate { DialogResult = DialogResult.OK; }));
        }

        public static bool Ask(IWin32Window owner, string prompt, List<Account> accounts, string current, out string label)
        {
            label = current;
            using (var f = new PickAccount(prompt, accounts, current))
            {
                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                label = f.combo.SelectedIndex <= 0 ? "" : accounts[f.combo.SelectedIndex - 1].label;
                return true;
            }
        }
    }

    // Undo what Hatpick changed: per-repo settings (restored to what the repo had before)
    // and the folder defaults in your global .gitconfig.
    class ResetForm : DialogBase
    {
        CheckedListBox repos;
        CheckBox folders;
        public List<string> ChosenRepos = new List<string>();
        public bool ChosenFolders;

        public ResetForm(List<string> managed, int folderDefaults) : base("Reset what Hatpick changed", 620, 440)
        {
            Body.ColumnCount = 1;
            Body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Body.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(S(590), 0), Margin = new Padding(0, 0, 0, S(8)),
                Text = "Each checked repo goes back to exactly what it had before Hatpick first changed it. " +
                       "Your commits, branches and Git Credential Manager sign-ins aren't touched."
            });
            repos = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
            foreach (var m in managed) repos.Items.Add(m, true);
            if (managed.Count == 0) { repos.Items.Add("(Hatpick hasn't changed any repos)"); repos.Enabled = false; }
            Body.Controls.Add(repos);
            folders = new CheckBox
            {
                AutoSize = true, Checked = folderDefaults > 0, Enabled = folderDefaults > 0, Margin = new Padding(0, S(8), 0, 0),
                Text = folderDefaults > 0
                    ? "Also remove the " + folderDefaults + " folder default(s) from your global .gitconfig"
                    : "No folder defaults to remove"
            };
            Body.Controls.Add(folders);
            Body.Controls.Add(OkCancel("Reset", Done));
        }

        void Done()
        {
            if (repos.Enabled) foreach (var o in repos.CheckedItems) ChosenRepos.Add((string)o);
            ChosenFolders = folders.Checked;
            if (ChosenRepos.Count == 0 && !ChosenFolders) { DialogResult = DialogResult.Cancel; return; }
            DialogResult = DialogResult.OK;
        }
    }
}
