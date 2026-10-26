using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Hatpick
{
    public class Account
    {
        public string label { get; set; }    // what you see in Hatpick, e.g. "Work"
        public string name { get; set; }     // git user.name
        public string email { get; set; }    // git user.email
        public string github { get; set; }   // GitHub username Git Credential Manager signs in as
    }

    // A folder Hatpick watches: every repo inside it is listed, and it can have a
    // default account that applies to all of them, including ones created later.
    public class WatchedFolder
    {
        public string path { get; set; }
        public string account { get; set; }  // label of the default account; "" = none
    }

    public class Settings
    {
        public List<Account> accounts { get; set; }
        public List<WatchedFolder> folders { get; set; }   // watched folders
        public List<string> repos { get; set; }            // single repos added on their own
        public List<string> managed { get; set; }          // repos Hatpick has written settings into
        public bool setupDone { get; set; }

        static readonly string Here = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string FilePath = Path.Combine(Here, "hatpick.json");
        // Settings files from the app's earlier names (Gitpicky, GitPicker), newest first.
        static readonly string[] OldFiles = { Path.Combine(Here, "gitpicky.json"), Path.Combine(Here, "gitpicker.json") };

        public static bool Exists { get { return File.Exists(FilePath); } }
        public static bool OldExists { get { return OldFiles.Any(File.Exists); } }

        public static Settings Load()
        {
            Settings s = null;
            try
            {
                string file = File.Exists(FilePath) ? FilePath : OldFiles.FirstOrDefault(File.Exists);
                if (file != null)
                {
                    s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(file));
                    if (s != null && file != FilePath) s.setupDone = true;   // carried over from an earlier name
                }
            }
            catch { }
            if (s == null) s = new Settings();
            if (s.accounts == null) s.accounts = new List<Account>();
            if (s.folders == null) s.folders = new List<WatchedFolder>();
            if (s.repos == null) s.repos = new List<string>();
            if (s.managed == null) s.managed = new List<string>();
            return s;
        }

        public void Save()
        {
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
        }

        public Settings Copy()
        {
            var js = new JavaScriptSerializer();
            return js.Deserialize<Settings>(js.Serialize(this));
        }

        public Account AccountByEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            return accounts.FirstOrDefault(a => string.Equals(a.email, email, StringComparison.OrdinalIgnoreCase));
        }

        public Account AccountByGitHub(string user)
        {
            if (string.IsNullOrEmpty(user)) return null;
            return accounts.FirstOrDefault(a => string.Equals(a.github, user, StringComparison.OrdinalIgnoreCase));
        }

        public Account AccountByLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            return accounts.FirstOrDefault(a => a.label == label);
        }

        public static bool SamePath(string a, string b)
        {
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        public static string Norm(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            try { p = Path.GetFullPath(p.Replace('/', Path.DirectorySeparatorChar)); } catch { }
            return p.TrimEnd('\\', '/');
        }

        public void MarkManaged(string repo)
        {
            if (!managed.Any(m => SamePath(m, repo))) managed.Add(Norm(repo));
        }

        public void UnmarkManaged(string repo)
        {
            managed.RemoveAll(m => SamePath(m, repo));
        }
    }
}
