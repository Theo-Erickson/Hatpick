// Hatpick
// Picks which Git account each repo commits and pushes as.
//
// - Per repo: writes user.name, user.email and credential.https://github.com.username
//   into that repo's own .git/config. GitHub Desktop and command-line git both use them.
// - Per folder: a scan folder can have a default account. Hatpick writes an
//   [includeIf "gitdir/i:<folder>/"] entry into your global .gitconfig pointing at
//   ~/.hatpick/<account>.gitconfig, so every repo in that folder - including ones you
//   create later - uses that account unless the repo has its own setting.
//
// Settings live in hatpick.json next to the .exe.
// Build with build.cmd (uses the C# compiler that ships with Windows). C# 5 only.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Hatpick")]
[assembly: System.Reflection.AssemblyProduct("Hatpick")]
[assembly: System.Reflection.AssemblyDescription("Pick which Git account each repo uses")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace Hatpick
{
    // ---------- settings ----------

    public class Account
    {
        public string label { get; set; }    // what you see in Hatpick, e.g. "Work"
        public string name { get; set; }     // git user.name
        public string email { get; set; }    // git user.email
        public string github { get; set; }   // GitHub username Git Credential Manager signs in as
    }

    public class ScanFolder
    {
        public string path { get; set; }
        public string account { get; set; }  // label of the folder's default account; "" = none
    }

    public class Settings
    {
        public List<Account> accounts { get; set; }
        public List<ScanFolder> folders { get; set; }
        public List<string> repos { get; set; }   // repos added by hand
    }

    static class Program
    {
        public const string Title = "Hatpick";
        public static float Scale = 1f;
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            bool first;
            using (var mutex = new Mutex(true, "Hatpick.SingleInstance", out first))
            {
                if (!first)
                {
                    IntPtr h = FindWindow(null, Title);
                    if (h != IntPtr.Zero) { ShowWindow(h, 9); SetForegroundWindow(h); }
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
                if (Git.Exe == null)
                {
                    MessageBox.Show("Couldn't find git.exe.\n\nInstall Git for Windows (git-scm.com), then open Hatpick again.",
                        Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Application.Run(new MainForm());
            }
        }
    }

    // ---------- git ----------

    class GitResult { public int Code; public string Out; public string Err; }

    static class Git
    {
        public const string CredKey = "credential.https://github.com.username";
        public static readonly string Exe = Find();
        static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string IncludeDir = Path.Combine(Home, ".hatpick");
        // Include folders this app has written to, including under its earlier names.
        public static readonly string[] OurIncludeDirs = { ".hatpick/", ".gitpicky/", ".gitpicker/" };

        static string Find()
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    string d = dir.Trim().Trim('"');
                    if (d.Length == 0) continue;
                    string p = Path.Combine(d, "git.exe");
                    if (File.Exists(p)) return p;
                }
                catch { }
            }
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string c = Path.Combine(pf, @"Git\cmd\git.exe");
            if (File.Exists(c)) return c;
            // GitHub Desktop's bundled git, newest version first.
            string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");
            if (Directory.Exists(desk))
            {
                var apps = Directory.GetDirectories(desk, "app-*").OrderByDescending(a => ParseVersion(Path.GetFileName(a).Substring(4)));
                foreach (var a in apps)
                {
                    string g = Path.Combine(a, @"resources\app\git\cmd\git.exe");
                    if (File.Exists(g)) return g;
                }
            }
            return null;
        }

        static Version ParseVersion(string s) { Version v; return Version.TryParse(s, out v) ? v : new Version(0, 0); }

        // Windows command-line quoting (the rules CommandLineToArgvW / the C runtime use).
        public static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char ch in a)
            {
                if (ch == '\\') { bs++; continue; }
                if (ch == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); }
                else { sb.Append('\\', bs); sb.Append(ch); }
                bs = 0;
            }
            sb.Append('\\', bs * 2);
            sb.Append('"');
            return sb.ToString();
        }

        public static GitResult Run(params string[] args)
        {
            var psi = new ProcessStartInfo(Exe, string.Join(" ", args.Select(Quote)))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Home
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return new GitResult { Code = p.ExitCode, Out = output, Err = err.Result.Trim() };
            }
        }

        public static GitResult InRepo(string repo, params string[] args)
        {
            var all = new List<string> { "-C", repo };
            all.AddRange(args);
            return Run(all.ToArray());
        }

        static void Check(GitResult r, string what)
        {
            if (r.Code != 0) throw new Exception(what + " failed: " + (r.Err.Length > 0 ? r.Err : "exit code " + r.Code));
        }

        // Effective user.email for a repo and where it came from.
        // Output of --show-scope --show-origin --get: "<scope>\t<origin>\t<value>"
        public static void ReadIdentity(RepoInfo info)
        {
            var r = InRepo(info.Path, "config", "--show-scope", "--show-origin", "--get", "user.email");
            if (r.Code == 1) { info.Email = ""; info.Scope = ""; return; }   // not set anywhere
            if (r.Code != 0) { info.Error = r.Err; return; }
            var parts = r.Out.TrimEnd('\r', '\n').Split(new[] { '\t' }, 3);
            if (parts.Length < 3) { info.Error = "Unexpected output from git: " + r.Out.Trim(); return; }
            info.Scope = parts[0];
            info.Origin = parts[1];
            info.Email = parts[2];
        }

        public static void Assign(string repo, Account a)
        {
            Check(InRepo(repo, "config", "--local", "user.name", a.name ?? ""), "Setting user.name");
            Check(InRepo(repo, "config", "--local", "user.email", a.email ?? ""), "Setting user.email");
            if (!string.IsNullOrEmpty(a.github))
                Check(InRepo(repo, "config", "--local", CredKey, a.github), "Setting the GitHub username");
            else
                InRepo(repo, "config", "--local", "--unset", CredKey);
        }

        public static void Clear(string repo)
        {
            // Exit code 5 just means the key wasn't set.
            InRepo(repo, "config", "--local", "--unset", "user.name");
            InRepo(repo, "config", "--local", "--unset", "user.email");
            InRepo(repo, "config", "--local", "--unset", CredKey);
        }

        // ---------- folder defaults (includeIf in the global config) ----------

        public static string FileFor(Account a) { return SafeFileName(a.label) + ".gitconfig"; }

        static string SafeFileName(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s ?? "") sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
            return sb.Length > 0 ? sb.ToString() : "account";
        }

        static string IniValue(string v)
        {
            return "\"" + (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        // Rewrites ~/.hatpick/*.gitconfig and the includeIf entries that point at them.
        public static void SyncFolderDefaults(Settings s)
        {
            Directory.CreateDirectory(IncludeDir);
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in s.accounts)
            {
                string file = FileFor(a);
                keep.Add(file);
                var sb = new StringBuilder();
                sb.Append("# Written by Hatpick for the '" + a.label + "' account. Edit the account in Hatpick - changes here get overwritten.\n");
                sb.Append("[user]\n");
                sb.Append("\tname = " + IniValue(a.name) + "\n");
                sb.Append("\temail = " + IniValue(a.email) + "\n");
                if (!string.IsNullOrEmpty(a.github))
                {
                    sb.Append("[credential \"https://github.com\"]\n");
                    sb.Append("\tusername = " + IniValue(a.github) + "\n");
                }
                File.WriteAllText(Path.Combine(IncludeDir, file), sb.ToString(), new UTF8Encoding(false));
            }
            foreach (var f in Directory.GetFiles(IncludeDir, "*.gitconfig"))
                if (!keep.Contains(Path.GetFileName(f))) { try { File.Delete(f); } catch { } }

            // Remove our old includeIf sections. -z: entries end in NUL, key and value split by newline.
            var r = Run("config", "--global", "-z", "--get-regexp", @"^includeif\..*\.path$");
            foreach (var entry in r.Out.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int nl = entry.IndexOf('\n');
                if (nl < 0) continue;
                string key = entry.Substring(0, nl), value = entry.Substring(nl + 1);
                if (!OurIncludeDirs.Any(dir => value.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                // key looks like "includeif.gitdir/i:C:/Repos/.path"; --remove-section wants the
                // section exactly as written in the file, and Hatpick writes "includeIf".
                string sub = key.Substring("includeif".Length, key.Length - "includeif".Length - ".path".Length);
                if (Run("config", "--global", "--remove-section", "includeIf" + sub).Code != 0)
                    Run("config", "--global", "--remove-section", "includeif" + sub);
            }

            // Add current ones. They're appended to the end of .gitconfig, after your global
            // [user] section, so they win over it (later settings override earlier ones).
            foreach (var f in s.folders)
            {
                var a = s.accounts.FirstOrDefault(x => x.label == f.account);
                if (a == null) continue;
                string dir = f.path.Replace('\\', '/').TrimEnd('/') + "/";
                Check(Run("config", "--global", "includeIf.gitdir/i:" + dir + ".path", "~/.hatpick/" + FileFor(a)),
                    "Setting the default for " + f.path);
            }
        }

        // Accounts Git Credential Manager already has a sign-in for.
        public static List<string> CredentialManagerAccounts()
        {
            var list = new List<string>();
            try
            {
                var r = Run("credential-manager", "github", "list");
                if (r.Code == 0)
                    foreach (var line in r.Out.Split('\n'))
                        if (line.Trim().Length > 0) list.Add(line.Trim());
            }
            catch { }
            return list;
        }
    }

    class RepoInfo
    {
        public string Path;
        public string Group;      // scan folder it was found in, or null if added by hand
        public string Email = "";
        public string Scope = ""; // local / global / system / "" (not set)
        public string Origin = "";
        public string Error;
    }

    // ---------- main window ----------

    class MainForm : Form
    {
        static readonly string Here = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string SettingsFile = Path.Combine(Here, "hatpick.json");
        // Settings files from the app's earlier names (Gitpicky, GitPicker), newest first.
        static readonly string[] OldSettingsFiles = { Path.Combine(Here, "gitpicky.json"), Path.Combine(Here, "gitpicker.json") };
        static string OldSettingsFile { get { return OldSettingsFiles.FirstOrDefault(File.Exists); } }
        static readonly string[] SkipDirs = { "node_modules", "Binaries", "Intermediate", "Saved", "DerivedDataCache", "Library", ".godot", ".vs", "obj", "bin" };
        const int MaxDepth = 4;

        Settings settings;
        List<RepoInfo> repos = new List<RepoInfo>();
        bool scanning;

        ListView list;
        Label hint;
        ContextMenuStrip accountMenu;
        Button setButton;

        static int S(int px) { return Program.S(px); }

        public MainForm()
        {
            Text = Program.Title;
            Font = new Font("Segoe UI", 9.5f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(820), S(460));
            MinimumSize = new Size(S(640), S(420));

            settings = LoadSettings();

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(S(10)) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            list = new ListView
            {
                View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false,
                Dock = DockStyle.Fill, Margin = new Padding(0, 0, S(10), 0), ShowGroups = true
            };
            list.Columns.Add("Repository", S(170));
            list.Columns.Add("Account", S(130));
            list.Columns.Add("Set by", S(110));
            list.Columns.Add("Folder", S(200));
            list.DoubleClick += delegate { ShowAccountMenu(true); };
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) ShowAccountMenu(false);
                else if (e.KeyCode == Keys.F5) Rescan();
                else if (e.KeyCode == Keys.A && e.Control) foreach (ListViewItem i in list.Items) i.Selected = true;
            };
            list.Resize += delegate { FitLastColumn(); };
            layout.Controls.Add(list, 0, 0);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            layout.Controls.Add(buttons, 1, 0);

            setButton = AddButton(buttons, "Set account \u25BE", delegate { ShowAccountMenu(false); }, true);
            AddButton(buttons, "Open in GitHub Desktop", OpenInDesktop, false);
            AddButton(buttons, "Open folder", OpenFolder, false);
            buttons.Controls.Add(new Panel { Height = S(8), Width = 1, Margin = Padding.Empty });
            AddButton(buttons, "Accounts...", EditAccounts, false);
            AddButton(buttons, "Scan folders...", EditFolders, false);
            AddButton(buttons, "Add repo...", AddRepo, false);
            AddButton(buttons, "Remove repo", RemoveRepo, false);
            AddButton(buttons, "Rescan", Rescan, false);

            hint = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, S(8), 0, 0) };
            layout.Controls.Add(hint, 0, 1);
            layout.SetColumnSpan(hint, 2);

            accountMenu = new ContextMenuStrip();

            Shown += delegate
            {
                if (File.Exists(SettingsFile)) Rescan();
                else if (OldSettingsFile != null) { SaveSettings(); SyncFolders(); Rescan(); }   // carry over from an earlier name
                else { SaveSettings(); EditAccounts(); }
            };
        }

        Button AddButton(FlowLayoutPanel panel, string text, Action onClick, bool primary)
        {
            var b = new Button { Text = text, Width = S(170), Height = S(primary ? 42 : 32), Margin = new Padding(0, 0, 0, S(6)) };
            if (primary) b.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            b.Click += delegate { onClick(); };
            panel.Controls.Add(b);
            return b;
        }

        void SetHint(string text) { hint.Text = text; }
        void DefaultHint()
        {
            SetHint("Select repos (Ctrl/Shift for several) and pick an account. Double-click works too. F5 rescans.");
        }

        // ---------- settings ----------

        static Settings LoadSettings()
        {
            Settings s = null;
            try
            {
                string file = File.Exists(SettingsFile) ? SettingsFile : OldSettingsFile;
                if (file != null && File.Exists(file))
                    s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file));
            }
            catch { }
            if (s == null) s = FirstRunSettings();
            if (s.accounts == null) s.accounts = new List<Account>();
            if (s.folders == null) s.folders = new List<ScanFolder>();
            if (s.repos == null) s.repos = new List<string>();
            return s;
        }

        // Seed with your current global identity and GitHub Desktop's default repo folder.
        static Settings FirstRunSettings()
        {
            var s = new Settings { accounts = new List<Account>(), folders = new List<ScanFolder>(), repos = new List<string>() };
            string name = Git.Run("config", "--global", "--get", "user.name").Out.Trim();
            string email = Git.Run("config", "--global", "--get", "user.email").Out.Trim();
            if (name.Length > 0 || email.Length > 0)
            {
                var gcm = Git.CredentialManagerAccounts();
                s.accounts.Add(new Account { label = "Main", name = name, email = email, github = gcm.Count > 0 ? gcm[0] : "" });
            }
            string ghDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub");
            if (Directory.Exists(ghDir)) s.folders.Add(new ScanFolder { path = ghDir, account = "" });
            return s;
        }

        void SaveSettings()
        {
            File.WriteAllText(SettingsFile, new JavaScriptSerializer().Serialize(settings), new UTF8Encoding(false));
        }

        Account AccountByEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            return settings.accounts.FirstOrDefault(a => string.Equals(a.email, email, StringComparison.OrdinalIgnoreCase));
        }

        // ---------- scanning ----------

        void Rescan()
        {
            if (scanning) return;
            scanning = true;
            SetHint("Scanning...");
            UseWaitCursor = true;
            var snapshot = new JavaScriptSerializer().Deserialize<Settings>(new JavaScriptSerializer().Serialize(settings));
            Task.Factory.StartNew<List<RepoInfo>>(() => Scan(snapshot)).ContinueWith(t =>
            {
                scanning = false;
                UseWaitCursor = false;
                if (IsDisposed) return;
                if (t.Exception != null) { SetHint("Scan failed: " + t.Exception.InnerException.Message); return; }
                var keep = SelectedPaths();
                repos = t.Result;
                RebuildList(keep);
                DefaultHint();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        static List<RepoInfo> Scan(Settings s)
        {
            var found = new List<RepoInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in s.folders)
            {
                var hits = new List<string>();
                Walk(f.path, 0, hits);
                foreach (var h in hits.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    if (seen.Add(Norm(h))) found.Add(new RepoInfo { Path = h, Group = f.path });
            }
            foreach (var r in s.repos)
                if (seen.Add(Norm(r))) found.Add(new RepoInfo { Path = r, Group = null });

            Parallel.ForEach(found, new ParallelOptions { MaxDegreeOfParallelism = 6 }, info =>
            {
                if (!IsRepo(info.Path)) { info.Error = "Not found"; return; }
                try { Git.ReadIdentity(info); } catch (Exception e) { info.Error = e.Message; }
            });
            return found;
        }

        static string Norm(string p) { return p.TrimEnd('\\', '/'); }

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

        // ---------- list ----------

        List<string> SelectedPaths()
        {
            var l = new List<string>();
            foreach (ListViewItem i in list.SelectedItems) l.Add(((RepoInfo)i.Tag).Path);
            return l;
        }

        List<RepoInfo> SelectedRepos()
        {
            var l = new List<RepoInfo>();
            foreach (ListViewItem i in list.SelectedItems) l.Add((RepoInfo)i.Tag);
            return l;
        }

        string Describe(RepoInfo r, out string setBy, out Color color)
        {
            color = SystemColors.WindowText;
            setBy = "";
            if (r.Error != null) { color = Color.Firebrick; setBy = "Error"; return r.Error; }
            if (r.Scope == "") { color = Color.Firebrick; setBy = "Not set"; return "(none)"; }
            var a = AccountByEmail(r.Email);
            if (r.Scope == "local") setBy = "This repo";
            else if (r.Origin.IndexOf(".hatpick", StringComparison.OrdinalIgnoreCase) >= 0) { setBy = "Folder default"; color = Color.FromArgb(30, 90, 150); }
            else { setBy = r.Scope == "global" ? "Global" : char.ToUpper(r.Scope[0]) + r.Scope.Substring(1); color = SystemColors.GrayText; }
            return a != null ? a.label : r.Email;
        }

        void RebuildList(List<string> select)
        {
            list.BeginUpdate();
            list.Items.Clear();
            list.Groups.Clear();
            var groups = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in settings.folders)
            {
                string def = string.IsNullOrEmpty(f.account) ? "no default" : "default: " + f.account;
                var g = new ListViewGroup(f.path + "   (" + def + ")");
                groups[f.path] = g;
                list.Groups.Add(g);
            }
            var manual = new ListViewGroup("Added by hand");
            list.Groups.Add(manual);

            foreach (var r in repos)
            {
                string setBy; Color color;
                string acct = Describe(r, out setBy, out color);
                var item = new ListViewItem(Path.GetFileName(Norm(r.Path))) { Tag = r, ForeColor = color, UseItemStyleForSubItems = true };
                item.SubItems.Add(acct);
                item.SubItems.Add(setBy);
                item.SubItems.Add(r.Path);
                ListViewGroup g;
                item.Group = r.Group != null && groups.TryGetValue(r.Group, out g) ? g : manual;
                if (select != null && select.Contains(r.Path, StringComparer.OrdinalIgnoreCase)) item.Selected = true;
                list.Items.Add(item);
            }
            list.EndUpdate();
            if (list.SelectedItems.Count == 0 && list.Items.Count > 0) { list.Items[0].Selected = true; list.Items[0].Focused = true; }
            FitLastColumn();
        }

        void FitLastColumn()
        {
            if (list.Columns.Count < 4) return;
            int w = list.ClientSize.Width - list.Columns[0].Width - list.Columns[1].Width - list.Columns[2].Width;
            if (w > S(80)) list.Columns[3].Width = w;
        }

        // ---------- set account ----------

        void ShowAccountMenu(bool atCursor)
        {
            var sel = SelectedRepos();
            if (sel.Count == 0) return;
            accountMenu.Items.Clear();
            var current = sel.Select(r => r.Scope == "local" ? (r.Email ?? "").ToLowerInvariant() : null).Distinct().ToList();
            foreach (var a in settings.accounts)
            {
                var acct = a;
                var mi = new ToolStripMenuItem(a.label + "    " + a.email);
                mi.Checked = current.Count == 1 && current[0] != null && string.Equals(current[0], a.email, StringComparison.OrdinalIgnoreCase);
                mi.Click += delegate { Apply(sel, acct); };
                accountMenu.Items.Add(mi);
            }
            if (settings.accounts.Count == 0)
                accountMenu.Items.Add(new ToolStripMenuItem("No accounts yet - click Accounts...") { Enabled = false });
            accountMenu.Items.Add(new ToolStripSeparator());
            var clear = new ToolStripMenuItem("Use folder default / global");
            clear.Checked = current.Count == 1 && current[0] == null;
            clear.Click += delegate { Apply(sel, null); };
            accountMenu.Items.Add(clear);

            if (atCursor) accountMenu.Show(Cursor.Position);
            else accountMenu.Show(setButton, new Point(0, setButton.Height));
        }

        void Apply(List<RepoInfo> sel, Account a)
        {
            var errors = new List<string>();
            Cursor = Cursors.WaitCursor;
            foreach (var r in sel)
            {
                try { if (a != null) Git.Assign(r.Path, a); else Git.Clear(r.Path); }
                catch (Exception e) { errors.Add(Path.GetFileName(r.Path) + ": " + e.Message); }
            }
            Cursor = Cursors.Default;
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\n", errors), Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Rescan();
        }

        // ---------- other actions ----------

        void OpenFolder()
        {
            var r = SelectedRepos().FirstOrDefault(); if (r == null) return;
            if (Directory.Exists(r.Path)) Process.Start("explorer.exe", "\"" + r.Path + "\"");
        }

        void OpenInDesktop()
        {
            var r = SelectedRepos().FirstOrDefault(); if (r == null) return;
            try { Process.Start("x-github-client://openLocalRepo/" + Uri.EscapeDataString(r.Path)); }
            catch (Exception e) { MessageBox.Show(this, "Couldn't open GitHub Desktop:\n" + e.Message, Program.Title); }
        }

        void AddRepo()
        {
            using (var d = new FolderBrowserDialog { Description = "Pick a repository folder (the one that contains .git)", ShowNewFolderButton = false })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!IsRepo(d.SelectedPath))
                {
                    MessageBox.Show(this, "That folder isn't a Git repository - it has no .git inside.", Program.Title);
                    return;
                }
                if (!settings.repos.Contains(d.SelectedPath, StringComparer.OrdinalIgnoreCase)) settings.repos.Add(d.SelectedPath);
                SaveSettings();
                Rescan();
            }
        }

        void RemoveRepo()
        {
            var sel = SelectedRepos();
            if (sel.Count == 0) return;
            var scanned = sel.Where(r => r.Group != null).ToList();
            if (scanned.Count > 0)
            {
                MessageBox.Show(this, "'" + Path.GetFileName(scanned[0].Path) + "' is found by scanning\n" + scanned[0].Group +
                    "\n\nOnly repos added by hand can be removed here. Its Git settings are untouched either way.", Program.Title);
                return;
            }
            foreach (var r in sel) settings.repos.RemoveAll(p => string.Equals(p, r.Path, StringComparison.OrdinalIgnoreCase));
            SaveSettings();
            Rescan();
        }

        void SyncFolders()
        {
            try { Git.SyncFolderDefaults(settings); }
            catch (Exception e) { MessageBox.Show(this, e.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        void EditAccounts()
        {
            var before = settings.accounts.Select(a => new Account { label = a.label, name = a.name, email = a.email, github = a.github }).ToList();
            using (var f = new AccountsForm(settings.accounts)) f.ShowDialog(this);

            // Carry label renames over to scan folder defaults; drop defaults for removed accounts.
            foreach (var f in settings.folders)
                if (!string.IsNullOrEmpty(f.account))
                {
                    string renamed;
                    if (AccountsForm.Renames.TryGetValue(f.account, out renamed)) f.account = renamed;
                    if (!settings.accounts.Any(a => a.label == f.account)) f.account = "";
                }

            // Repos set to an account whose details changed get the new details.
            int updated = 0;
            foreach (var old in before)
            {
                string newLabel;
                if (!AccountsForm.Renames.TryGetValue(old.label, out newLabel)) newLabel = old.label;
                var now = settings.accounts.FirstOrDefault(a => a.label == newLabel);
                if (now == null) continue;
                if (now.name == old.name && now.email == old.email && now.github == old.github) continue;
                foreach (var r in repos.Where(r => r.Scope == "local" && string.Equals(r.Email, old.email, StringComparison.OrdinalIgnoreCase)))
                {
                    try { Git.Assign(r.Path, now); updated++; } catch { }
                }
            }
            SaveSettings();
            SyncFolders();
            Rescan();
            if (updated > 0) MessageBox.Show(this, "Updated " + updated + " repo(s) to the edited account details.", Program.Title);
        }

        void EditFolders()
        {
            using (var f = new FoldersForm(settings)) f.ShowDialog(this);
            SaveSettings();
            SyncFolders();
            Rescan();
        }
    }

    // ---------- small dialogs ----------

    class DialogBase : Form
    {
        protected static int S(int px) { return Program.S(px); }
        protected TableLayoutPanel Body;

        protected DialogBase(string title, int w, int h)
        {
            Text = title;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(S(w), S(h));
            Body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(S(10)) };
            Controls.Add(Body);
        }

        protected Button MakeButton(string text, Action onClick)
        {
            var b = new Button { Text = text, Width = S(130), Height = S(32), Margin = new Padding(0, 0, 0, S(6)) };
            b.Click += delegate { onClick(); };
            return b;
        }
    }

    // List + buttons dialog used for both Accounts and Scan folders.
    abstract class ListDialog : DialogBase
    {
        protected ListView List;
        protected FlowLayoutPanel Buttons;

        protected ListDialog(string title, string note) : base(title, 720, 340)
        {
            Body.ColumnCount = 2; Body.RowCount = 2;
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            List = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, S(10), 0) };
            Body.Controls.Add(List, 0, 0);
            Buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            Body.Controls.Add(Buttons, 1, 0);
            var lbl = new Label { Text = note, AutoSize = true, MaximumSize = new Size(S(690), 0), ForeColor = SystemColors.GrayText, Margin = new Padding(0, S(8), 0, 0) };
            Body.Controls.Add(lbl, 0, 1);
            Body.SetColumnSpan(lbl, 2);
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
        // old label -> new label, for renames made in the last session of this dialog
        public static Dictionary<string, string> Renames = new Dictionary<string, string>();

        readonly List<Account> accounts;

        public AccountsForm(List<Account> accounts)
            : base("Accounts", "Each account is a name + email for commits, and the GitHub username Git Credential Manager signs in as when pushing. Editing an account updates every repo already set to it.")
        {
            this.accounts = accounts;
            Renames = new Dictionary<string, string>();
            List.Columns.Add("Label", S(100));
            List.Columns.Add("Commit name", S(140));
            List.Columns.Add("Commit email", S(190));
            List.Columns.Add("GitHub username", S(120));
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
            using (var f = new AccountEditor(a, accounts, null))
                if (f.ShowDialog(this) == DialogResult.OK) { accounts.Add(a); Rebuild(accounts.Count - 1); }
        }

        void Edit()
        {
            int i = SelectedIndex; if (i < 0) return;
            var a = accounts[i];
            string oldLabel = a.label;
            using (var f = new AccountEditor(a, accounts, a))
                if (f.ShowDialog(this) == DialogResult.OK)
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
            if (MessageBox.Show(this, "Remove '" + a.label + "' from Hatpick?\n\nRepos already set to it keep their settings until you change them. Folder defaults that use it are cleared.",
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

        public AccountEditor(Account target, List<Account> all, Account self) : base(self == null ? "Add account" : "Edit account", 460, 250)
        {
            this.target = target; this.all = all; this.self = self;
            Body.ColumnCount = 2;
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            label = AddField("Label (e.g. Work):", target.label);
            name = AddField("Commit name:", target.name);
            email = AddField("Commit email:", target.email);

            Body.Controls.Add(new Label { Text = "GitHub username:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(8), 0) });
            github = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, Text = target.github ?? "" };
            foreach (var g in Git.CredentialManagerAccounts()) github.Items.Add(g);
            Body.Controls.Add(github);

            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, S(12), 0, 0) };
            var cancel = new Button { Text = "Cancel", Width = S(90), Height = S(30), DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = "OK", Width = S(90), Height = S(30) };
            ok.Click += delegate { Save(); };
            row.Controls.Add(cancel); row.Controls.Add(ok);
            Body.Controls.Add(row);
            Body.SetColumnSpan(row, 2);
            AcceptButton = ok; CancelButton = cancel;
        }

        TextBox AddField(string caption, string value)
        {
            Body.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, S(6), S(8), 0) });
            var t = new TextBox { Dock = DockStyle.Fill, Text = value ?? "" };
            Body.Controls.Add(t);
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
    }

    class FoldersForm : ListDialog
    {
        readonly Settings settings;

        public FoldersForm(Settings settings)
            : base("Scan folders", "Hatpick lists every repo inside these folders. A folder's default account applies to all its repos, including ones you create later, unless a repo has its own account set.")
        {
            this.settings = settings;
            List.Columns.Add("Folder", S(400));
            List.Columns.Add("Default account", S(140));
            List.DoubleClick += delegate { ChangeDefault(); };
            Buttons.Controls.Add(MakeButton("Add folder...", Add));
            Buttons.Controls.Add(MakeButton("Default account...", ChangeDefault));
            Buttons.Controls.Add(MakeButton("Remove", Remove));
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
            using (var d = new FolderBrowserDialog { Description = "Pick a folder that holds your repos", ShowNewFolderButton = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (settings.folders.Any(f => string.Equals(f.path, d.SelectedPath, StringComparison.OrdinalIgnoreCase))) return;
                var nf = new ScanFolder { path = d.SelectedPath, account = "" };
                string pick;
                if (PickAccount.Ask(this, "Default account for repos in\n" + nf.path, settings.accounts, "", out pick)) nf.account = pick;
                settings.folders.Add(nf);
                Rebuild(settings.folders.Count - 1);
            }
        }

        void ChangeDefault()
        {
            int i = SelectedIndex; if (i < 0) return;
            var f = settings.folders[i];
            string pick;
            if (PickAccount.Ask(this, "Default account for repos in\n" + f.path, settings.accounts, f.account, out pick))
            { f.account = pick; Rebuild(i); }
        }

        void Remove()
        {
            int i = SelectedIndex; if (i < 0) return;
            settings.folders.RemoveAt(i);
            Rebuild(i);
        }
    }

    class PickAccount : DialogBase
    {
        ComboBox combo;

        PickAccount(string prompt, List<Account> accounts, string current) : base("Default account", 420, 170)
        {
            Body.ColumnCount = 1;
            Body.Controls.Add(new Label { Text = prompt, AutoSize = true, MaximumSize = new Size(S(390), 0) });
            combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, S(8), 0, 0) };
            combo.Items.Add("(none - use your global identity)");
            foreach (var a in accounts) combo.Items.Add(a.label + "    " + a.email);
            int idx = accounts.FindIndex(a => a.label == current);
            combo.SelectedIndex = idx + 1;
            Body.Controls.Add(combo);
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, S(12), 0, 0) };
            var cancel = new Button { Text = "Cancel", Width = S(90), Height = S(30), DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = "OK", Width = S(90), Height = S(30), DialogResult = DialogResult.OK };
            row.Controls.Add(cancel); row.Controls.Add(ok);
            Body.Controls.Add(row);
            AcceptButton = ok; CancelButton = cancel;
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
}
