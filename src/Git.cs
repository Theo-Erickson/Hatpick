using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Hatpick
{
    class GitResult { public int Code; public string Out; public string Err; }

    class ConfigEntry
    {
        public string Scope;   // system / global / local / worktree / command
        public string Origin;  // e.g. "file:C:/Users/theos/.gitconfig"
        public string Key;     // section and variable lowercased, subsection as written
        public string Value;
    }

    // Everything that touches git. Hatpick only ever reads and writes plain config
    // settings. It never asks git or Git Credential Manager for a password or token
    // (never runs "git credential fill"), so secrets stay in Windows Credential Manager.
    static class Git
    {
        public const string CredKey = "credential.https://github.com.username";
        public static readonly string Exe = Find();
        // Properties, not fields: a static field can be set before Main reads --demo, which once
        // sent the demo's made-up hats into your real ~/.hatpick.
        static string Home { get { return Demo.On ? Demo.HomeDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }
        public static string IncludeDir { get { return Path.Combine(Home, ".hatpick"); } }
        // Include folders this app has written to, including under its earlier names.
        public static readonly string[] OurIncludeDirs = { ".hatpick/", ".gitpicky/", ".gitpicker/" };

        // ---------- finding and running git ----------

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

        // Git Bash, if this git came from Git for Windows (..\git-bash.exe next to cmd\git.exe).
        public static string GitBash
        {
            get
            {
                try
                {
                    string root = Path.GetDirectoryName(Path.GetDirectoryName(Exe));
                    string gb = Path.Combine(root, "git-bash.exe");
                    return File.Exists(gb) ? gb : null;
                }
                catch { return null; }
            }
        }

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
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";   // never block waiting for input
            Demo.Isolate(psi);
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

        // ---------- reading ----------

        // Top folder of the repo containing 'path', or null if it isn't inside one.
        public static string Toplevel(string path)
        {
            if (!Directory.Exists(path)) return null;
            var r = InRepo(path, "rev-parse", "--show-toplevel");
            if (r.Code != 0) return null;
            string top = r.Out.Trim();
            return top.Length == 0 ? null : Settings.Norm(top);
        }

        // Every setting that applies in this repo, in the order git reads them (later wins).
        // -z output: scope NUL origin NUL key LF value NUL (a key with no value has no LF).
        public static List<ConfigEntry> ListConfig(string repo)
        {
            var r = InRepo(repo, "config", "--show-scope", "--show-origin", "-z", "--list");
            if (r.Code != 0) throw new Exception(r.Err.Length > 0 ? r.Err : "git config failed");
            var parts = r.Out.Split('\0');
            var list = new List<ConfigEntry>();
            for (int i = 0; i + 2 < parts.Length; i += 3)
            {
                string kv = parts[i + 2];
                int nl = kv.IndexOf('\n');
                list.Add(new ConfigEntry
                {
                    Scope = parts[i], Origin = parts[i + 1],
                    Key = nl < 0 ? kv : kv.Substring(0, nl),
                    Value = nl < 0 ? "" : kv.Substring(nl + 1)
                });
            }
            return list;
        }

        // ---------- Git Credential Manager (names only, never secrets) ----------

        public static bool GcmAvailable;

        // GitHub accounts Git Credential Manager has a saved sign-in for.
        public static List<string> CredentialManagerAccounts()
        {
            var list = new List<string>();
            if (Demo.On) { GcmAvailable = true; return new List<string>(Demo.GcmAccounts); }
            try
            {
                var r = Run("credential-manager", "github", "list");
                GcmAvailable = r.Code == 0;
                if (r.Code == 0)
                    foreach (var line in r.Out.Split('\n'))
                        if (line.Trim().Length > 0) list.Add(line.Trim());
            }
            catch { GcmAvailable = false; }
            return list;
        }

        // Opens a console window running GCM's own sign-in (it opens your browser).
        // Hatpick never sees what you type there.
        public static Process StartGcmLogin()
        {
            if (Demo.On) throw new Exception("This is the demo, so there's nothing to sign in to.");
            var psi = new ProcessStartInfo(Exe, "credential-manager github login")
            {
                UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = Home
            };
            return Process.Start(psi);
        }

        // ---------- writing a repo's account (with a backup so Reset can undo it) ----------

        static string LocalGet(string repo, string key)
        {
            var r = InRepo(repo, "config", "--local", "--get", key);
            return r.Code == 0 ? r.Out.TrimEnd('\r', '\n') : null;
        }

        public static bool IsManaged(string repo) { return LocalGet(repo, "hatpick.managed") == "true"; }

        // The first time Hatpick touches a repo, it saves whatever the repo had set itself.
        static void Backup(string repo)
        {
            if (IsManaged(repo)) return;
            string n = LocalGet(repo, "user.name"), e = LocalGet(repo, "user.email"), u = LocalGet(repo, CredKey);
            if (n != null) InRepo(repo, "config", "--local", "hatpick.oldname", n);
            if (e != null) InRepo(repo, "config", "--local", "hatpick.oldemail", e);
            if (u != null) InRepo(repo, "config", "--local", "hatpick.oldusername", u);
            Check(InRepo(repo, "config", "--local", "hatpick.managed", "true"), "Saving a backup");
        }

        static void SetOrUnset(string repo, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) InRepo(repo, "config", "--local", "--unset", key);   // exit 5 = wasn't set
            else Check(InRepo(repo, "config", "--local", key, value), "Setting " + key);
        }

        // commitAs null = don't set a commit identity here (inherit the folder default / global).
        // pushAs null or "" = don't set a push account here.
        public static void Assign(string repo, Account commitAs, string pushAs)
        {
            Backup(repo);
            SetOrUnset(repo, "user.name", commitAs == null ? null : commitAs.name);
            SetOrUnset(repo, "user.email", commitAs == null ? null : commitAs.email);
            SetOrUnset(repo, CredKey, pushAs);
        }

        // Puts the repo back exactly as it was before Hatpick first touched it.
        public static void Reset(string repo)
        {
            if (!IsManaged(repo)) return;
            string n = LocalGet(repo, "hatpick.oldname"), e = LocalGet(repo, "hatpick.oldemail"), u = LocalGet(repo, "hatpick.oldusername");
            SetOrUnset(repo, "user.name", n);
            SetOrUnset(repo, "user.email", e);
            SetOrUnset(repo, CredKey, u);
            InRepo(repo, "config", "--local", "--remove-section", "hatpick");
            // Drop the [credential "https://github.com"] header if nothing is left under it.
            if (InRepo(repo, "config", "--local", "--get-regexp", @"^credential\.https://github\.com\.").Out.Trim().Length == 0)
                InRepo(repo, "config", "--local", "--remove-section", "credential.https://github.com");
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

        public static bool IsOurInclude(string origin)
        {
            return OurIncludeDirs.Any(d => (origin ?? "").Replace('\\', '/').IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0);
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

            RemoveFolderDefaults();

            // Add current ones. They're appended to the end of .gitconfig, after your global
            // [user] section, so they win over it (later settings override earlier ones).
            foreach (var f in s.folders)
            {
                var a = s.AccountByLabel(f.account);
                if (a == null) continue;
                string dir = f.path.Replace('\\', '/').TrimEnd('/') + "/";
                Check(Run("config", "--global", "includeIf.gitdir/i:" + dir + ".path", "~/.hatpick/" + FileFor(a)),
                    "Setting the default for " + f.path);
            }
        }

        // Removes every includeIf section Hatpick (or its earlier names) added. Returns how many.
        public static int RemoveFolderDefaults()
        {
            int n = 0;
            // -z: entries end in NUL, key and value split by newline.
            var r = Run("config", "--global", "-z", "--get-regexp", @"^includeif\..*\.path$");
            foreach (var entry in r.Out.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int nl = entry.IndexOf('\n');
                if (nl < 0) continue;
                string key = entry.Substring(0, nl), value = entry.Substring(nl + 1);
                if (!IsOurInclude(value)) continue;
                // key looks like "includeif.gitdir/i:C:/Repos/.path"; --remove-section wants the
                // section exactly as written in the file, and Hatpick writes "includeIf".
                string sub = key.Substring("includeif".Length, key.Length - "includeif".Length - ".path".Length);
                if (Run("config", "--global", "--remove-section", "includeIf" + sub).Code == 0 ||
                    Run("config", "--global", "--remove-section", "includeif" + sub).Code == 0) n++;
            }
            return n;
        }
    }
}
