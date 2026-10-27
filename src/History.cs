using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hatpick
{
    class CommitInfo
    {
        public string Sha, Short, Subject, Body;
        public string[] Parents;
        public string AuthorName, AuthorEmail, CommitterName, CommitterEmail;
        public DateTime When;
        public List<string> CoAuthors = new List<string>();
        public bool Pushed, Signed;
        public bool IsMerge { get { return Parents.Length > 1; } }
    }

    class BranchState
    {
        public string Branch;          // null = detached HEAD
        public string Upstream;        // e.g. origin/main, or null
        public string DefaultRemoteBranch;   // e.g. origin/main (what origin/HEAD points at), or null
        public bool Dirty, Busy;       // uncommitted changes to tracked files / a rebase, merge etc. in progress
        public List<CommitInfo> Commits = new List<CommitInfo>();
        public int UnpushedCount { get { return Commits.Count(c => !c.Pushed); } }
    }

    class Backup { public string Ref, Sha, When; }

    class RewritePlan
    {
        public List<string> Blockers = new List<string>();   // reasons it can't be done
        public List<Issue> Warnings = new List<Issue>();
        public bool NeedsAck;                                // pushed or other people's work involved
        public int RangeCount;                               // how many commits get a new ID
    }

    // Reading commit history, and rewriting the author / co-authors of commits on the
    // current branch. Every rewrite saves a backup ref first so it can be undone exactly.
    // Hatpick never pushes: after rewriting pushed commits you force push yourself.
    static class History
    {
        const string RS = "\x1e", US = "\x1f";
        static readonly Regex CoAuthorLine = new Regex(@"^\s*co-authored-by:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // ---------- reading ----------

        public static BranchState Read(string repo, int max = 60)
        {
            var st = new BranchState();
            var b = Git.InRepo(repo, "symbolic-ref", "--short", "-q", "HEAD");
            st.Branch = b.Code == 0 ? b.Out.Trim() : null;
            var u = Git.InRepo(repo, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
            st.Upstream = u.Code == 0 && u.Out.Trim().Length > 0 ? u.Out.Trim() : null;
            var d = Git.InRepo(repo, "symbolic-ref", "-q", "--short", "refs/remotes/origin/HEAD");
            st.DefaultRemoteBranch = d.Code == 0 ? d.Out.Trim() : null;
            st.Dirty = Git.InRepo(repo, "status", "--porcelain", "--untracked-files=no").Out.Trim().Length > 0;
            st.Busy = InProgress(repo);

            if (Git.InRepo(repo, "rev-parse", "-q", "--verify", "HEAD").Code != 0) return st;   // no commits yet

            var unpushed = new HashSet<string>(Git.InRepo(repo, "rev-list", "HEAD", "--not", "--remotes")
                .Out.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            var r = Git.InRepo(repo, "log", "-n", max.ToString(), "--date-order",
                "--format=%H%x1f%P%x1f%an%x1f%ae%x1f%cn%x1f%ce%x1f%at%x1f%G?%x1f%s%x1f%B%x1e");
            foreach (var rec in r.Out.Split(new[] { RS }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = rec.TrimStart('\n', '\r').Split(new[] { US }, 10, StringSplitOptions.None);
                if (f.Length < 10) continue;
                var c = new CommitInfo
                {
                    Sha = f[0], Short = f[0].Substring(0, Math.Min(7, f[0].Length)),
                    Parents = f[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries),
                    AuthorName = f[2], AuthorEmail = f[3], CommitterName = f[4], CommitterEmail = f[5],
                    When = FromUnix(f[6]), Signed = f[7] != "N" && f[7].Length > 0, Subject = f[8], Body = f[9]
                };
                foreach (Match m in CoAuthorLine.Matches(c.Body)) c.CoAuthors.Add(m.Groups[1].Value);
                c.Pushed = !unpushed.Contains(c.Sha);
                st.Commits.Add(c);
            }
            return st;
        }

        static DateTime FromUnix(string s)
        {
            long t;
            return long.TryParse(s, out t) ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(t).ToLocalTime() : DateTime.MinValue;
        }

        static bool InProgress(string repo)
        {
            foreach (var p in new[] { "rebase-merge", "rebase-apply", "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD" })
            {
                var r = Git.InRepo(repo, "rev-parse", "--git-path", p);
                if (r.Code != 0) continue;
                string path = r.Out.Trim();
                if (!System.IO.Path.IsPathRooted(path)) path = System.IO.Path.Combine(repo, path);
                if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) return true;
            }
            return false;
        }

        public static bool IsMine(CommitInfo c, Settings s) { return s.AccountByEmail(c.AuthorEmail) != null; }

        // ---------- planning: what a rewrite would touch, and what could go wrong ----------

        // Commits from the oldest selected one up to HEAD all get new IDs, selected or not.
        static List<CommitInfo> Range(BranchState st, HashSet<string> selected)
        {
            int oldest = -1;
            for (int i = 0; i < st.Commits.Count; i++) if (selected.Contains(st.Commits[i].Sha)) oldest = i;
            return oldest < 0 ? new List<CommitInfo>() : st.Commits.Take(oldest + 1).ToList();   // newest first
        }

        // Each commit's first parent must be the next older one: a plain line back from HEAD.
        static bool Straight(List<CommitInfo> range)
        {
            for (int i = 0; i + 1 < range.Count; i++)
                if (range[i].Parents.Length == 0 || range[i].Parents[0] != range[i + 1].Sha) return false;
            return true;
        }

        public static RewritePlan Plan(string repo, BranchState st, HashSet<string> selected, Settings s, Account newAuthor, bool removeCoAuthors)
        {
            var plan = new RewritePlan();
            var sel = st.Commits.Where(c => selected.Contains(c.Sha)).ToList();
            if (sel.Count == 0) { plan.Blockers.Add("Select at least one commit."); return plan; }
            if (newAuthor == null && !removeCoAuthors) plan.Blockers.Add("Choose a new author, remove co-authors, or both.");
            if (st.Branch == null) plan.Blockers.Add("You're not on a branch (detached HEAD). Check out a branch first.");
            if (st.Busy) plan.Blockers.Add("A rebase, merge or cherry-pick is in progress. Finish or abort it first.");
            if (st.Dirty) plan.Blockers.Add("You have uncommitted changes. Commit or stash them first.");
            var range = Range(st, selected);
            plan.RangeCount = range.Count;
            if (range.Any(c => c.IsMerge) || !Straight(range)) plan.Blockers.Add("The commits include a merge. Hatpick only rewrites a straight line of commits on this branch.");
            if (range.Count == st.Commits.Count && st.Commits.Count >= 60)
                plan.Blockers.Add("That reaches further back than Hatpick has loaded. Pick more recent commits.");
            if (plan.Blockers.Count > 0) return plan;

            bool anyPushed = range.Any(c => c.Pushed);
            if (anyPushed)
            {
                plan.NeedsAck = true;
                plan.Warnings.Add(new Issue(Level.Warn,
                    range.Count(c => c.Pushed) + " of these commits are already pushed. After rewriting, the branch on GitHub and yours will differ, so you'll " +
                    "need to force push (GitHub Desktop offers \"Force push\"). Anyone who already pulled this branch will need to reset their copy."));
                string upstreamBranch = st.Upstream;
                if (upstreamBranch != null && upstreamBranch == st.DefaultRemoteBranch)
                    plan.Warnings.Add(new Issue(Level.Warn, upstreamBranch + " is the repo's default branch. Others are most likely to have it, and it may be protected against force pushes on GitHub."));
            }
            else
                plan.Warnings.Add(new Issue(Level.Info, "None of these commits are pushed yet, so nobody else has them. This is safe to do before you push."));

            var others = range.Where(c => !selected.Contains(c.Sha) && !IsMine(c, s)).ToList();
            if (others.Count > 0)
            {
                plan.NeedsAck = true;
                plan.Warnings.Add(new Issue(Level.Warn, others.Count + " commit(s) by other people (" + string.Join(", ", others.Select(c => c.AuthorName).Distinct()) +
                    ") sit on top of the ones you picked. Their content and authors stay the same, but they get new commit IDs too."));
            }
            var notMine = sel.Where(c => !IsMine(c, s)).ToList();
            if (notMine.Count > 0 && newAuthor != null)
            {
                plan.NeedsAck = true;
                plan.Warnings.Add(new Issue(Level.Warn, notMine.Count + " selected commit(s) were authored by someone who isn't one of your hats (" +
                    string.Join(", ", notMine.Select(c => c.AuthorName).Distinct()) + "). Changing the author takes the credit away from them."));
            }
            if (removeCoAuthors && sel.Any(c => c.CoAuthors.Count > 0))
                plan.Warnings.Add(new Issue(Level.Info, "Removing co-authors means GitHub stops crediting " +
                    string.Join(", ", sel.SelectMany(c => c.CoAuthors).Select(x => Regex.Replace(x, @"\s*<.*>", "")).Distinct()) + " on these commits."));
            if (range.Any(c => c.Signed))
                plan.Warnings.Add(new Issue(Level.Warn, range.Count(c => c.Signed) + " signed commit(s) will lose their signature. Rewritten commits aren't signed."));

            var oldest = range[range.Count - 1];
            var other = Git.InRepo(repo, "branch", "-a", "--contains", oldest.Sha, "--format=%(refname:short)").Out
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())
                .Where(x => x != st.Branch && x != st.Upstream && !x.EndsWith("/HEAD")).ToList();
            if (other.Count > 0)
                plan.Warnings.Add(new Issue(Level.Info, "These commits are also on " + string.Join(", ", other.Take(5)) + (other.Count > 5 ? " and more" : "") +
                    ". Those branches keep the old version."));
            plan.Warnings.Add(new Issue(Level.Info, plan.RangeCount + " commit(s) get new IDs. A backup is saved first, so Undo can put the branch back exactly."));
            return plan;
        }

        // Undoing commits: the branch steps back to before the oldest selected commit (and
        // everything newer goes with it), while their changes stay in your folder, staged,
        // ready to commit again. Like "git reset --soft", with a backup first.
        public static RewritePlan PlanUncommit(string repo, BranchState st, HashSet<string> selected, Settings s)
        {
            var plan = new RewritePlan();
            if (!st.Commits.Any(c => selected.Contains(c.Sha))) { plan.Blockers.Add("Select at least one commit."); return plan; }
            if (st.Branch == null) plan.Blockers.Add("You're not on a branch (detached HEAD). Check out a branch first.");
            if (st.Busy) plan.Blockers.Add("A rebase, merge or cherry-pick is in progress. Finish or abort it first.");
            var range = Range(st, selected);
            plan.RangeCount = range.Count;
            if (range.Any(c => c.IsMerge) || !Straight(range)) plan.Blockers.Add("The commits include a merge. Hatpick only undoes a straight line of commits on this branch.");
            if (range.Count > 0 && range[range.Count - 1].Parents.Length == 0) plan.Blockers.Add("That includes the repo's very first commit, which has nothing before it to go back to.");
            if (range.Count == st.Commits.Count && st.Commits.Count >= 60) plan.Blockers.Add("That reaches further back than Hatpick has loaded. Pick more recent commits.");
            if (plan.Blockers.Count > 0) return plan;

            int extra = range.Count(c => !selected.Contains(c.Sha));
            if (extra > 0)
                plan.Warnings.Add(new Issue(Level.Warn, "This also undoes " + extra + " newer commit(s) you didn't select. Undo always works back from the newest commit."));
            if (range.Any(c => c.Pushed))
            {
                plan.NeedsAck = true;
                plan.Warnings.Add(new Issue(Level.Warn, range.Count(c => c.Pushed) + " of these commits are already pushed. When you commit again you'll need to " +
                    "force push (GitHub Desktop offers \"Force push\"), and anyone who already pulled this branch will need to reset their copy."));
                if (st.Upstream != null && st.Upstream == st.DefaultRemoteBranch)
                    plan.Warnings.Add(new Issue(Level.Warn, st.Upstream + " is the repo's default branch. Others are most likely to have it, and it may be protected against force pushes on GitHub."));
            }
            else
                plan.Warnings.Add(new Issue(Level.Info, "None of these commits are pushed yet, so nobody else has them."));
            var others = range.Where(c => !IsMine(c, s)).ToList();
            if (others.Count > 0)
            {
                plan.NeedsAck = true;
                plan.Warnings.Add(new Issue(Level.Warn, others.Count + " of these commits are by other people (" + string.Join(", ", others.Select(c => c.AuthorName).Distinct()) +
                    "). If you commit their changes again, they're credited to you instead."));
            }
            if (range.Any(c => c.CoAuthors.Count > 0))
                plan.Warnings.Add(new Issue(Level.Info, "Co-authored-by lines go with the old commits. Add them back in the new commit's message if you want to keep them."));
            if (st.Dirty)
                plan.Warnings.Add(new Issue(Level.Info, "You also have uncommitted changes. They stay as they are, mixed in with the undone ones."));
            plan.Warnings.Add(new Issue(Level.Info, "Files in your folder don't change. The changes from " + (range.Count == 1 ? "this commit" : "these " + range.Count + " commits") +
                " show up as uncommitted changes in GitHub Desktop, ready to commit again. A backup is saved first, so Undo can put the commits back."));
            return plan;
        }

        // Returns the backup ref name.
        public static string Uncommit(string repo, BranchState st, HashSet<string> selected)
        {
            var range = Range(st, selected);
            if (range.Count == 0) throw new Exception("Nothing selected.");
            var oldest = range[range.Count - 1];
            if (oldest.Parents.Length == 0) throw new Exception("That's the first commit.");
            string oldHead = Git.InRepo(repo, "rev-parse", "HEAD").Out.Trim();
            if (oldHead != st.Commits[0].Sha) throw new Exception("The branch changed since Hatpick read it. Refresh and try again.");
            string backup = SaveBackup(repo, st.Branch, oldHead, "hatpick: backup before undoing commits");
            var r = Git.InRepo(repo, "reset", "-q", "--soft", oldest.Parents[0]);
            if (r.Code != 0) throw new Exception("Undoing the commits failed: " + r.Err + "\n\nNothing was changed.");
            return backup;
        }

        static string SaveBackup(string repo, string branch, string sha, string why)
        {
            string backup = "refs/hatpick/backup/" + branch + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var b = Git.InRepo(repo, "update-ref", "-m", why, backup, sha);
            if (b.Code != 0) throw new Exception("Couldn't save a backup: " + b.Err);
            return backup;
        }

        // ---------- rewriting ----------

        static GitResult Run(string repo, Dictionary<string, string> env, string input, params string[] args)
        {
            var all = new List<string> { "-C", repo };
            all.AddRange(args);
            var psi = new ProcessStartInfo(Git.Exe, string.Join(" ", all.Select(Git.Quote)))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = input != null, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            Demo.Isolate(psi);
            if (env != null) foreach (var kv in env) psi.EnvironmentVariables[kv.Key] = kv.Value;
            using (var p = Process.Start(psi))
            {
                if (input != null)
                {
                    var bytes = new UTF8Encoding(false).GetBytes(input);
                    p.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                    p.StandardInput.Close();
                }
                var err = p.StandardError.ReadToEndAsync();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return new GitResult { Code = p.ExitCode, Out = output, Err = err.Result.Trim() };
            }
        }

        class RawCommit { public string Tree, AuthorName, AuthorEmail, AuthorDate, CommitterName, CommitterEmail, CommitterDate, Message; }

        static RawCommit ReadRaw(string repo, string sha)
        {
            var r = Git.InRepo(repo, "cat-file", "commit", sha);
            if (r.Code != 0) throw new Exception("Couldn't read commit " + sha + ": " + r.Err);
            var raw = new RawCommit();
            int split = r.Out.IndexOf("\n\n", StringComparison.Ordinal);
            string head = split < 0 ? r.Out : r.Out.Substring(0, split);
            raw.Message = split < 0 ? "" : r.Out.Substring(split + 2);
            foreach (var line in head.Split('\n'))
            {
                if (line.StartsWith("tree ")) raw.Tree = line.Substring(5).Trim();
                else if (line.StartsWith("author ")) ParseIdent(line.Substring(7), out raw.AuthorName, out raw.AuthorEmail, out raw.AuthorDate);
                else if (line.StartsWith("committer ")) ParseIdent(line.Substring(10), out raw.CommitterName, out raw.CommitterEmail, out raw.CommitterDate);
            }
            return raw;
        }

        // "Name <email> 1700000000 +0100"
        static void ParseIdent(string s, out string name, out string email, out string date)
        {
            int lt = s.IndexOf('<'), gt = s.LastIndexOf('>');
            name = lt > 0 ? s.Substring(0, lt).Trim() : "";
            email = lt >= 0 && gt > lt ? s.Substring(lt + 1, gt - lt - 1) : "";
            date = gt >= 0 ? s.Substring(gt + 1).Trim() : "";
        }

        public static string StripCoAuthors(string message)
        {
            var lines = message.Replace("\r\n", "\n").Split('\n').Where(l => !CoAuthorLine.IsMatch(l)).ToList();
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            return string.Join("\n", lines) + "\n";
        }

        // Returns the backup ref name.
        public static string Rewrite(string repo, BranchState st, HashSet<string> selected, Account newAuthor, bool removeCoAuthors, bool alsoCommitter)
        {
            var range = Range(st, selected);
            if (range.Count == 0) throw new Exception("Nothing selected.");
            if (range.Any(c => c.IsMerge) || !Straight(range)) throw new Exception("The commits include a merge.");
            string oldHead = Git.InRepo(repo, "rev-parse", "HEAD").Out.Trim();
            if (oldHead != st.Commits[0].Sha) throw new Exception("The branch changed since Hatpick read it. Refresh and try again.");

            string backup = SaveBackup(repo, st.Branch, oldHead, "hatpick: backup before rewrite");

            var oldestParents = range[range.Count - 1].Parents;
            string parent = oldestParents.Length > 0 ? oldestParents[0] : null;
            for (int i = range.Count - 1; i >= 0; i--)    // oldest first
            {
                var c = range[i];
                var raw = ReadRaw(repo, c.Sha);
                bool pick = selected.Contains(c.Sha);
                var env = new Dictionary<string, string>
                {
                    { "GIT_AUTHOR_NAME", pick && newAuthor != null ? newAuthor.name : raw.AuthorName },
                    { "GIT_AUTHOR_EMAIL", pick && newAuthor != null ? newAuthor.email : raw.AuthorEmail },
                    { "GIT_AUTHOR_DATE", raw.AuthorDate },
                    { "GIT_COMMITTER_NAME", pick && newAuthor != null && alsoCommitter ? newAuthor.name : raw.CommitterName },
                    { "GIT_COMMITTER_EMAIL", pick && newAuthor != null && alsoCommitter ? newAuthor.email : raw.CommitterEmail },
                    { "GIT_COMMITTER_DATE", raw.CommitterDate },
                };
                string msg = pick && removeCoAuthors ? StripCoAuthors(raw.Message) : raw.Message;
                var args = new List<string> { "commit-tree", raw.Tree };
                if (parent != null) { args.Add("-p"); args.Add(parent); }
                args.Add("-F"); args.Add("-");
                var r = Run(repo, env, msg, args.ToArray());
                if (r.Code != 0) throw new Exception("Rewriting " + c.Short + " failed: " + r.Err + "\n\nNothing was changed. Your branch is as it was.");
                parent = r.Out.Trim();
            }
            // The new commits have the same files, so only the branch pointer moves; your working folder is untouched.
            var u = Git.InRepo(repo, "update-ref", "-m", "hatpick: rewrite authors", "refs/heads/" + st.Branch, parent, oldHead);
            if (u.Code != 0) throw new Exception("Couldn't move the branch: " + u.Err + "\n\nNothing was changed.");
            return backup;
        }

        // ---------- backups and undo ----------

        public static List<Backup> Backups(string repo, string branch)
        {
            var list = new List<Backup>();
            if (branch == null) return list;
            var r = Git.InRepo(repo, "for-each-ref", "--sort=-refname", "--format=%(refname)%1f%(objectname)", "refs/hatpick/backup/" + branch + "/");
            foreach (var line in r.Out.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = line.Split('\x1f');
                if (f.Length < 2) continue;
                string stamp = f[0].Substring(f[0].LastIndexOf('/') + 1);
                DateTime t;
                string when = DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out t) ? t.ToString("MMM d, h:mm tt") : stamp;
                list.Add(new Backup { Ref = f[0], Sha = f[1], When = when });
            }
            return list;
        }

        // Puts the branch back to the backup. Never loses uncommitted changes: after undoing
        // commits (the branch is behind the backup), only the branch moves and your files stay;
        // otherwise it refuses while you have uncommitted changes.
        public static void Undo(string repo, BranchState st, Backup b)
        {
            if (st.Branch == null) throw new Exception("Check out the branch first.");
            bool behind = Git.InRepo(repo, "merge-base", "--is-ancestor", "HEAD", b.Sha).Code == 0;
            if (st.Dirty && !behind) throw new Exception("You have uncommitted changes. Commit or stash them first.");
            var r = Git.InRepo(repo, "reset", "-q", behind ? "--mixed" : "--keep", b.Sha);   // --mixed: moves the branch and the staging area, never your files
            if (r.Code != 0) throw new Exception("Undo failed: " + r.Err);
            Git.InRepo(repo, "update-ref", "-d", b.Ref);
        }

        public static void DeleteBackup(string repo, Backup b) { Git.InRepo(repo, "update-ref", "-d", b.Ref); }
    }
}
