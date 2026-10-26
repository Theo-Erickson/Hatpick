using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hatpick
{
    enum Level { Ok = 0, Info = 1, Warn = 2, Error = 3 }

    class Issue
    {
        public Level Level; public string Text;
        public Issue(Level l, string t) { Level = l; Text = t; }
    }

    enum RepoSource { Watched, Single, Opened }

    // One row of "where each setting comes from": what a layer sets, and whether it wins.
    class LayerRow
    {
        public string Layer;
        public string Name = "", Email = "", Push = "";
        public bool NameWins, EmailWins, PushWins;
    }

    // What a repo commits and pushes as, where each setting comes from, and what's off.
    class RepoReport
    {
        public string Path;
        public RepoSource Source;
        public string WatchedFolder;      // set when Source == Watched

        public string Error;              // couldn't be read at all

        public string Name = "", Email = "";
        public string IdentitySetBy = "";

        public string RemoteName = "", RemoteUrl = "";
        public string Host = "", Owner = "", RepoName = "", UrlUser = "";
        public bool IsSsh, IsLocal;

        public string PushUser = "";
        public string PushSetBy = "";
        public bool? SignedIn;            // null = couldn't ask Git Credential Manager

        public bool Managed;              // Hatpick has written settings into this repo
        public bool LocalIdentity, LocalPush;

        public List<Issue> Issues = new List<Issue>();
        public List<LayerRow> Layers = new List<LayerRow>();
        public Level Level { get { return Issues.Count == 0 ? Level.Ok : Issues.Max(i => i.Level); } }

        public string DisplayName { get { return System.IO.Path.GetFileName(Settings.Norm(Path)); } }
        public bool IsGitHubHttps { get { return !IsSsh && string.Equals(Host, "github.com", StringComparison.OrdinalIgnoreCase); } }
    }

    static class Analyzer
    {
        static string SetBy(ConfigEntry e)
        {
            if (e == null) return "";
            if (e.Scope == "local") return "This repo";
            if (e.Scope == "worktree") return "This worktree";
            if (Git.IsOurInclude(e.Origin)) return "Folder default";
            if (e.Scope == "global") return "Your global settings";
            if (e.Scope == "system") return "Git's system settings";
            return e.Scope;
        }

        static ConfigEntry Last(List<ConfigEntry> entries, string key)
        {
            return entries.LastOrDefault(e => e.Key == key);
        }

        // https://user@host/owner/repo(.git)   ssh://git@host/owner/repo   git@host:owner/repo   C:/local/path
        static void ParseRemote(RepoReport r)
        {
            string url = r.RemoteUrl;
            if (Regex.IsMatch(url, @"^[A-Za-z]:[\\/]") || url.StartsWith("/") || url.StartsWith("\\") ||
                url.StartsWith(".") || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            { r.IsLocal = true; return; }
            var m = Regex.Match(url, @"^https?://(?:(?<user>[^@/]+)@)?(?<host>[^/:]+)(?::\d+)?/(?<path>.+?)/?$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                r.Host = m.Groups["host"].Value;
                r.UrlUser = Uri.UnescapeDataString(m.Groups["user"].Value.Split(':')[0]);
                SplitPath(r, m.Groups["path"].Value);
                return;
            }
            m = Regex.Match(url, @"^ssh://(?:[^@/]+@)?(?<host>[^/:]+)(?::\d+)?/(?<path>.+?)/?$", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(url, @"^(?:[^@/]+@)?(?<host>[^/:]+):(?<path>[^/].*?)/?$");
            if (m.Success)
            {
                r.IsSsh = true;
                r.Host = m.Groups["host"].Value;
                SplitPath(r, m.Groups["path"].Value);
            }
        }

        static void SplitPath(RepoReport r, string path)
        {
            var parts = path.Split('/');
            if (parts.Length >= 2)
            {
                r.Owner = parts[0];
                r.RepoName = Regex.Replace(parts[parts.Length - 1], @"\.git$", "");
            }
        }

        // Which credential.*.username applies to this remote, following git's URL matching:
        // a more specific URL wins, and among equally specific ones the last one read wins.
        static ConfigEntry PushEntry(List<ConfigEntry> entries, RepoReport r)
        {
            string target = r.Host.Length > 0 ? "https://" + r.Host + "/" + r.Owner + "/" + r.RepoName : "https://github.com/";
            ConfigEntry best = null; int bestScore = -1;
            foreach (var e in entries)
            {
                int score;
                if (e.Key == "credential.username") score = 0;
                else if (e.Key.StartsWith("credential.") && e.Key.EndsWith(".username"))
                {
                    string pattern = e.Key.Substring("credential.".Length, e.Key.Length - "credential.".Length - ".username".Length);
                    score = UrlScore(pattern, target);
                    if (score < 0) continue;
                }
                else continue;
                if (score >= bestScore) { best = e; bestScore = score; }
            }
            return best;
        }

        static int UrlScore(string pattern, string target)
        {
            var p = Regex.Match(pattern, @"^(?<scheme>[a-z]+)://(?<host>[^/]+)(?<path>/.*)?$", RegexOptions.IgnoreCase);
            var t = Regex.Match(target, @"^(?<scheme>[a-z]+)://(?<host>[^/]+)(?<path>/.*)?$", RegexOptions.IgnoreCase);
            if (!p.Success || !t.Success) return -1;
            if (!string.Equals(p.Groups["scheme"].Value, t.Groups["scheme"].Value, StringComparison.OrdinalIgnoreCase)) return -1;
            if (!string.Equals(p.Groups["host"].Value, t.Groups["host"].Value, StringComparison.OrdinalIgnoreCase)) return -1;
            string pp = p.Groups["path"].Value.TrimEnd('/'), tp = t.Groups["path"].Value.TrimEnd('/');
            if (pp.Length == 0) return 1;
            if (tp == pp || tp.StartsWith(pp + "/")) return 1 + pp.Length;
            return -1;
        }

        public static RepoReport Analyze(string path, RepoSource source, string watched, Settings s, List<string> gcm)
        {
            var r = new RepoReport { Path = path, Source = source, WatchedFolder = watched };
            if (!Directory.Exists(path) || !(Directory.Exists(System.IO.Path.Combine(path, ".git")) || File.Exists(System.IO.Path.Combine(path, ".git"))))
            {
                r.Error = "Folder not found, or it's no longer a git repo.";
                r.Issues.Add(new Issue(Level.Error, r.Error));
                return r;
            }
            List<ConfigEntry> entries;
            try { entries = Git.ListConfig(path); }
            catch (Exception e)
            {
                r.Error = e.Message;
                r.Issues.Add(new Issue(Level.Error, "Git couldn't read this repo: " + e.Message));
                return r;
            }

            var name = Last(entries, "user.name");
            var email = Last(entries, "user.email");
            r.Name = name != null ? name.Value : "";
            r.Email = email != null ? email.Value : "";
            r.IdentitySetBy = SetBy(email ?? name);
            r.LocalIdentity = (email != null && email.Scope == "local") || (name != null && name.Scope == "local");
            var managed = Last(entries, "hatpick.managed");
            r.Managed = managed != null && managed.Scope == "local" && managed.Value == "true";

            var origin = Last(entries, "remote.origin.url");
            var remote = origin ?? entries.FirstOrDefault(e => e.Key.StartsWith("remote.") && e.Key.EndsWith(".url"));
            if (remote != null)
            {
                r.RemoteUrl = remote.Value;
                r.RemoteName = remote.Key.Substring(7, remote.Key.Length - 11);
                ParseRemote(r);
            }

            if (r.UrlUser.Length > 0) { r.PushUser = r.UrlUser; r.PushSetBy = "The remote URL"; }
            else
            {
                var pe = PushEntry(entries, r);
                if (pe != null) { r.PushUser = pe.Value; r.PushSetBy = SetBy(pe); r.LocalPush = pe.Scope == "local"; }
            }
            Layers(r, entries, name, email);

            if (Git.GcmAvailable && r.PushUser.Length > 0)
                r.SignedIn = gcm.Any(g => string.Equals(g, r.PushUser, StringComparison.OrdinalIgnoreCase));

            AddIssues(r, s, gcm);
            return r;
        }

        static string LayerOf(ConfigEntry e)
        {
            if (e.Scope == "local" || e.Scope == "worktree") return "This repo";
            if (Git.IsOurInclude(e.Origin)) return "Folder default";
            if (e.Scope == "global") return "Global";
            if (e.Scope == "system") return "System";
            return null;
        }

        static void Layers(RepoReport r, List<ConfigEntry> entries, ConfigEntry nameWin, ConfigEntry emailWin)
        {
            var pushWin = r.UrlUser.Length > 0 ? null : PushEntry(entries, r);
            if (r.UrlUser.Length > 0)
                r.Layers.Add(new LayerRow { Layer = "Remote URL", Push = r.UrlUser, PushWins = true });
            foreach (var layer in new[] { "This repo", "Folder default", "Global", "System" })
            {
                var es = entries.Where(e => LayerOf(e) == layer).ToList();
                var n = Last(es, "user.name"); var em = Last(es, "user.email"); var p = PushEntry(es, r);
                if (layer == "System" && n == null && em == null && p == null) continue;
                r.Layers.Add(new LayerRow
                {
                    Layer = layer,
                    Name = n != null ? n.Value : "", Email = em != null ? em.Value : "", Push = p != null ? p.Value : "",
                    NameWins = n != null && n == nameWin, EmailWins = em != null && em == emailWin, PushWins = p != null && p == pushWin
                });
            }
        }

        static void AddIssues(RepoReport r, Settings s, List<string> gcm)
        {
            if (r.Name.Length == 0 || r.Email.Length == 0)
                r.Issues.Add(new Issue(Level.Error, "No commit name or email is set, so commits will fail. Pick an account below."));

            if (r.RemoteUrl.Length == 0)
                r.Issues.Add(new Issue(Level.Info, "No remote yet. The push account starts mattering once you add a GitHub remote (publishing from GitHub Desktop does that)."));
            else if (r.IsLocal)
                r.Issues.Add(new Issue(Level.Info, "The remote is a folder on this computer, so pushing doesn't sign in to anything."));
            else if (r.IsSsh)
                r.Issues.Add(new Issue(Level.Info, "This remote uses SSH, so your SSH key decides who pushes. The push account setting only applies to https remotes."));
            else if (!r.IsGitHubHttps && r.Host.Length > 0)
                r.Issues.Add(new Issue(Level.Info, "This remote is on " + r.Host + ". Hatpick's push account setting only covers github.com."));
            else if (r.Host.Length == 0)
                r.Issues.Add(new Issue(Level.Info, "Hatpick doesn't recognize this remote address, so it can't tell who pushes."));

            bool pushMatters = r.RemoteUrl.Length == 0 || r.IsGitHubHttps;
            if (pushMatters && r.PushUser.Length == 0)
            {
                if (!Git.GcmAvailable) { }
                else if (gcm.Count > 1)
                    r.Issues.Add(new Issue(Level.Warn, "No push account is set, so Git Credential Manager will ask which account to use every time you push."));
                else if (gcm.Count == 1)
                    r.Issues.Add(new Issue(Level.Info, "No push account is set. Git Credential Manager will use its only signed-in account, " + gcm[0] + "."));
                else
                    r.Issues.Add(new Issue(Level.Warn, "No GitHub accounts are signed in to Git Credential Manager yet, so your first push will open a sign-in."));
            }
            if (pushMatters && r.SignedIn == false)
                r.Issues.Add(new Issue(Level.Warn, r.PushUser + " isn't signed in to Git Credential Manager, so the first push will open your browser to sign in."));

            var commitAcct = s.AccountByEmail(r.Email);
            if (pushMatters && commitAcct != null && !string.IsNullOrEmpty(commitAcct.github) && r.PushUser.Length > 0 &&
                !string.Equals(commitAcct.github, r.PushUser, StringComparison.OrdinalIgnoreCase))
                r.Issues.Add(new Issue(Level.Warn, "Commits are authored as " + commitAcct.label + " (" + commitAcct.github + ") but pushed as " + r.PushUser + "."));

            if (r.IsGitHubHttps && r.Owner.Length > 0 && r.PushUser.Length > 0 &&
                !string.Equals(r.Owner, r.PushUser, StringComparison.OrdinalIgnoreCase))
                r.Issues.Add(new Issue(Level.Info, "This repo belongs to " + r.Owner + ". Pushing as " + r.PushUser + " works if that account is a collaborator or a member of the organization."));
        }
    }
}
