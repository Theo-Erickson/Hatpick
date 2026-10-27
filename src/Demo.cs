using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Hatpick
{
    // Hatpick.exe --demo [--autoplay <script>]
    //
    // Runs Hatpick against made-up accounts and sample repos in %TEMP%\Hatpick demo, so it
    // can be shown or recorded without real names, emails or folders. Nothing outside that
    // folder is read or changed: git gets its own global config and HOME there, the system
    // config is skipped, Git Credential Manager isn't called (its list is faked), and the
    // Explorer menu option does nothing. The folder is rebuilt every time the demo starts.
    //
    // --autoplay walks through the pages on a timer, for recording (tools\readme_capture).
    static class Demo
    {
        public static bool On;
        public static string Autoplay;              // script name, or null
        public static string Root, HomeDir;

        public static readonly List<string> GcmAccounts = new List<string> { "alex-acme", "alexr", "pixelforge-alex" };

        static readonly string[,] People =
        {
            // label, name, email, github, hat
            { "Work", "Alex Rivera", "alex.rivera@acme.example", "alex-acme", "hardhat" },
            { "Personal", "Alex Rivera", "alex@rivera.example", "alexr", "beanie" },
            { "Games", "Alex Rivera", "alex@pixelforge.example", "pixelforge-alex", "tophat" },
        };
        const string Teammate = "Sam Chen <sam.chen@acme.example>";

        // Reads the demo flags and points everything at the demo folder. Call before anything
        // touches Git or Settings, since their paths are fixed when first used.
        public static string[] TakeArgs(string[] args)
        {
            var rest = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--demo") On = true;
                else if (args[i] == "--autoplay") { On = true; Autoplay = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "tour"; }
                else rest.Add(args[i]);
            }
            if (On)
            {
                Root = Path.Combine(Path.GetTempPath(), "Hatpick demo");
                HomeDir = Path.Combine(Root, "home");
                Environment.SetEnvironmentVariable("HOME", HomeDir);
                Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", Path.Combine(HomeDir, ".gitconfig"));
                Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(HomeDir, ".config"));
            }
            return rest.ToArray();
        }

        static string Repos { get { return Path.Combine(Root, "repos"); } }

        // Builds the sample accounts, folders and repos. Returns the settings to open with.
        public static Settings Build()
        {
            if (Directory.Exists(Root))
                try { ForceDelete(Root); } catch { }
            Directory.CreateDirectory(HomeDir);
            Directory.CreateDirectory(Path.Combine(HomeDir, ".config"));
            File.WriteAllText(Path.Combine(HomeDir, ".gitconfig"), "[init]\n\tdefaultBranch = main\n[core]\n\tautocrlf = false\n");

            var s = new Settings
            {
                accounts = new List<Account>(), folders = new List<WatchedFolder>(), repos = new List<string>(),
                managed = new List<string>(), setupDone = true, theme = "dark",
            };
            for (int i = 0; i < People.GetLength(0); i++)
                s.accounts.Add(new Account { label = People[i, 0], name = People[i, 1], email = People[i, 2], github = People[i, 3], hat = People[i, 4] });
            // a hat with custom colors, to show them off
            s.accounts[2].colors = new Dictionary<string, string> { { "main", "#5b3a8c" }, { "trim", "#f5c02a" }, { "extra", "#7a52b3" } };
            Account work = s.accounts[0], personal = s.accounts[1], games = s.accounts[2];

            // Work: a folder default. acme-api has two unpushed commits made with the wrong hat
            // (one co-authored), acme-web was set to the personal hat by hand.
            string workDir = Dir("Work"), personalDir = Dir("Personal");
            s.folders.Add(new WatchedFolder { path = Settings.Norm(workDir), account = "Work" });
            s.folders.Add(new WatchedFolder { path = Settings.Norm(personalDir), account = "Personal" });

            string api = NewRepo(workDir, "acme-api", true);
            Commit(api, work, 30, "Set up the API project", "README.md", "# acme-api\n\nOrders and invoices for the Acme storefront.\n");
            Commit(api, work, 26, "Add orders endpoint", "src/orders.js", "export function listOrders() { return []; }\n");
            Commit(api, work, 19, "Add invoice totals", "src/invoices.js", "export const total = lines => lines.reduce((a, l) => a + l.price * l.qty, 0);\n");
            Commit(api, work, 12, "Validate order quantities", "src/orders.js", "export function listOrders() { return []; }\nexport const valid = q => q > 0;\n");
            Push(api);
            Commit(api, personal, 2, "Cache invoice lookups\n\nCo-authored-by: " + Teammate, "src/cache.js", "export const cache = new Map();\n");
            Commit(api, personal, 1, "Fix rounding in invoice totals", "src/invoices.js", "export const total = lines => Math.round(lines.reduce((a, l) => a + l.price * l.qty, 0) * 100) / 100;\n");

            string web = NewRepo(workDir, "acme-web", true);
            Commit(web, work, 40, "Start the storefront", "index.html", "<h1>Acme</h1>\n");
            Commit(web, work, 22, "Add the cart page", "cart.html", "<h1>Cart</h1>\n");
            Push(web);
            Git(web, "config", "--local", "user.name", personal.name);
            Git(web, "config", "--local", "user.email", personal.email);

            string infra = NewRepo(workDir, "acme-infra", true);
            Commit(infra, work, 50, "Add deploy scripts", "deploy.sh", "#!/bin/sh\necho deploying\n");
            Push(infra);

            // Personal: follows the folder default.
            string dot = NewRepo(personalDir, "dotfiles", true);
            Commit(dot, personal, 90, "Add shell config", ".bashrc", "alias gs='git status'\n");
            Commit(dot, personal, 35, "Add editor settings", ".editorconfig", "root = true\n");
            Push(dot);
            string recipes = NewRepo(personalDir, "recipe-box", false);
            Commit(recipes, personal, 8, "Add pancakes", "pancakes.md", "# Pancakes\n");

            // Games: a single repo wearing the custom-colored hat.
            string dungeon = NewRepo(Dir("Games"), "tiny-dungeon", true);
            Commit(dungeon, games, 15, "Add a player that can walk", "player.gd", "extends CharacterBody2D\n");
            Commit(dungeon, games, 6, "Add a first room", "room_01.tscn", "[gd_scene format=3]\n");
            Push(dungeon);
            Hatpick.Git.Assign(dungeon, games, games.github);
            s.repos.Add(Settings.Norm(dungeon));
            s.MarkManaged(dungeon);

            // A repo with no hat at all.
            string scratch = NewRepo(Dir("Sandbox"), "scratch", false);
            Commit(scratch, null, 3, "Try things out", "notes.txt", "scratch\n");
            s.repos.Add(Settings.Norm(scratch));

            s.Save();
            Hatpick.Git.SyncFolderDefaults(s);
            return s;
        }

        static string Dir(string name) { string d = Path.Combine(Repos, name); Directory.CreateDirectory(d); return d; }

        static string NewRepo(string parent, string name, bool remote)
        {
            string path = Path.Combine(parent, name);
            Directory.CreateDirectory(path);
            Git(path, "init", "-q", "-b", "main");
            if (remote)
            {
                string bare = Path.Combine(Root, "remotes", name + ".git");
                Directory.CreateDirectory(bare);
                Git(bare, "init", "-q", "--bare", "-b", "main");
                Git(path, "remote", "add", "origin", bare);
            }
            return path;
        }

        static void Push(string repo) { Git(repo, "push", "-q", "-u", "origin", "main"); }

        // Commits one file as 'who' (null = "Someone"), 'daysAgo' days back, at a working hour.
        static void Commit(string repo, Account who, int daysAgo, string message, string file, string content)
        {
            string full = Path.Combine(repo, file.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content, new UTF8Encoding(false));
            Git(repo, "add", "-A");
            var when = DateTime.Now.Date.AddDays(-daysAgo).AddHours(10 + (message.Length % 7)).AddMinutes(message.Length * 7 % 60);
            string date = when.ToString("yyyy-MM-ddTHH:mm:ss");
            string name = who != null ? who.name : "Someone", email = who != null ? who.email : "someone@laptop.example";
            var env = new Dictionary<string, string>
            {
                { "GIT_AUTHOR_NAME", name }, { "GIT_AUTHOR_EMAIL", email }, { "GIT_AUTHOR_DATE", date },
                { "GIT_COMMITTER_NAME", name }, { "GIT_COMMITTER_EMAIL", email }, { "GIT_COMMITTER_DATE", date },
            };
            string msgFile = Path.Combine(Root, "msg.txt");
            File.WriteAllText(msgFile, message + "\n", new UTF8Encoding(false));
            Run(repo, env, "commit", "-q", "-F", msgFile);
        }

        static void Git(string repo, params string[] args) { Run(repo, null, args); }

        static void Run(string repo, Dictionary<string, string> env, params string[] args)
        {
            var all = new List<string> { "-C", repo };
            all.AddRange(args);
            var psi = new ProcessStartInfo(Hatpick.Git.Exe, string.Join(" ", all.Select(Hatpick.Git.Quote)))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            };
            if (env != null) foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) throw new Exception("Demo setup: git " + string.Join(" ", args) + " failed: " + err.Result.Trim());
            }
        }

        static void ForceDelete(string dir)
        {
            // git marks object files read-only
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        // ---------- autoplay ----------

        class Step { public double Seconds; public string Page, Repo, Hat; }

        // Scripts: "tour" (everything), "overview", "repos", "hats", "fix".
        static List<Step> Script(string name)
        {
            var steps = new List<Step>();
            Action<double, string, string, string> add = (sec, pg, rp, ht) => steps.Add(new Step { Seconds = sec, Page = pg, Repo = rp, Hat = ht });
            Func<string, string> repo = n => Settings.Norm(Directory.GetDirectories(Repos, n, SearchOption.AllDirectories).First());
            switch (name)
            {
                case "overview":
                    add(6, "overview", null, null);
                    break;
                case "repos":
                    foreach (var n in new[] { "acme-api", "acme-web", "dotfiles", "tiny-dungeon", "scratch" }) add(2.8, "repos", repo(n), null);
                    break;
                case "hats":
                    foreach (var h in new[] { "Work", "Personal", "Games" }) add(2.2, "hats", null, h);
                    break;
                case "fix":
                    add(5, "repos", repo("acme-api"), null);
                    break;
                default:
                    add(4, "overview", null, null);
                    foreach (var n in new[] { "acme-api", "acme-web", "tiny-dungeon" }) add(2.6, "repos", repo(n), null);
                    foreach (var h in new[] { "Work", "Personal", "Games" }) add(1.8, "hats", null, h);
                    add(3, "folders", null, null);
                    break;
            }
            return steps;
        }

        // Steps through the script, looping, once the first scan is done.
        public static void Start(MainWindow w)
        {
            if (Autoplay == null) return;
            var steps = Script(Autoplay);
            int i = 0;
            var timer = new DispatcherTimer();
            EventHandler tick = null;
            tick = delegate
            {
                var st = steps[i % steps.Count];
                w.DemoShow(st.Page, st.Repo, st.Hat);
                timer.Interval = TimeSpan.FromSeconds(st.Seconds);
                i++;
            };
            timer.Tick += tick;
            timer.Interval = TimeSpan.FromSeconds(1.5);   // let the first scan land
            timer.Start();
        }
    }
}
