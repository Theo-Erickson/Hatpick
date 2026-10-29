using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Hatpick
{
    // Everything that differs between Windows, macOS and Linux: where Hatpick keeps its
    // files, how it opens folders and terminals, the words it uses for them, and how a
    // second copy hands a folder to the one already open.
    static class Platform
    {
        public static readonly bool Windows = OperatingSystem.IsWindows();
        public static readonly bool Mac = OperatingSystem.IsMacOS();
        public static readonly bool Linux = OperatingSystem.IsLinux();

        public static string ExePath { get { return Environment.ProcessPath; } }

        // Windows: next to the .exe, as always (Hatpick is portable there). macOS and Linux:
        // the usual per-user settings folder, since an app bundle or /usr/bin isn't for writing.
        public static string DataDir
        {
            get
            {
                if (Windows) return AppContext.BaseDirectory;
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (Mac) return Path.Combine(home, "Library", "Application Support", "Hatpick");
                string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, "hatpick");
            }
        }

        public static FontFamily UiFont
        {
            get
            {
                if (Windows) return new FontFamily("Segoe UI Variable Text, Segoe UI");
                if (Mac) return FontFamily.Default;
                return new FontFamily("fonts:Inter#Inter, $Default");
            }
        }

        public static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas, SF Mono, Menlo, DejaVu Sans Mono, Liberation Mono, monospace");

        // ---------- words ----------

        public static string FileManager { get { return Windows ? "Explorer" : Mac ? "Finder" : "your file manager"; } }
        public static string ReposFolder { get { return Path.Combine("Documents", "GitHub"); } }   // where GitHub Desktop clones
        public static string TerminalName { get { return Windows ? "Git Bash" : "Terminal"; } }
        public static string GitInstall
        {
            get
            {
                if (Windows) return "Install Git for Windows (git-scm.com), then open Hatpick again.";
                if (Mac) return "Install git (run xcode-select --install in Terminal, or brew install git), then open Hatpick again.";
                return "Install git with your package manager (for example sudo apt install git), then open Hatpick again.";
            }
        }
        public static string GcmInstall
        {
            get
            {
                return Windows
                    ? "It comes with Git for Windows - install or update it from git-scm.com."
                    : "Install it from github.com/git-ecosystem/git-credential-manager" + (Mac ? " (or brew install --cask git-credential-manager)." : ".");
            }
        }
        public static string SignInWindow { get { return Windows ? "a small console window and your browser" : "your browser"; } }
        public static string SignInDone { get { return Windows ? "When the console closes" : "When the sign-in finishes"; } }

        // ---------- opening things ----------

        public static void OpenFolder(string path)
        {
            if (Windows) Process.Start("explorer.exe", "\"" + path + "\"");
            else Process.Start(new ProcessStartInfo(Mac ? "open" : "xdg-open") { ArgumentList = { path }, UseShellExecute = false });
        }

        public static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        // GitHub Desktop is made for Windows and macOS only.
        public static bool HasGitHubDesktop { get { return Windows || Mac; } }

        public static bool HasTerminal { get { return Windows ? Git.GitBash != null : Mac || LinuxTerminal() != null; } }

        // Git Bash on Windows, Terminal on macOS, the desktop's terminal on Linux, opened in 'dir'.
        public static ProcessStartInfo Terminal(string dir)
        {
            if (Windows) return new ProcessStartInfo(Git.GitBash, "--cd=\"" + dir + "\"") { UseShellExecute = false };
            if (Mac) return new ProcessStartInfo("open") { ArgumentList = { "-a", "Terminal", dir }, UseShellExecute = false };
            var psi = new ProcessStartInfo(LinuxTerminal()) { UseShellExecute = false, WorkingDirectory = dir };
            return psi;
        }

        static readonly string[] LinuxTerminals = { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "kitty", "alacritty", "wezterm", "xterm" };

        static string LinuxTerminal()
        {
            foreach (var t in LinuxTerminals)
            {
                string p = OnPath(t);
                if (p != null) return p;
            }
            return null;
        }

        // The first 'name' (name.exe on Windows) in PATH, or null.
        public static string OnPath(string name)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    string d = dir.Trim().Trim('"');
                    if (d.Length == 0) continue;
                    string p = Path.Combine(d, Windows ? name + ".exe" : name);
                    if (File.Exists(p)) return p;
                }
                catch { }
            }
            return null;
        }

        // ---------- one copy at a time ----------
        //
        // The first copy listens on a named pipe. A second copy (say, from Explorer's
        // right-click entry) sends it the folder to open, lets it come to the front, and quits.

        static string PipeName { get { return (Demo.On ? "Hatpick.Demo." : "Hatpick.") + Environment.UserName; } }

        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);

        // True if another copy was running and got the path (so this one should quit).
        public static bool HandToRunningCopy(string path)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    c.Connect(300);
                    if (Windows) AllowSetForegroundWindow(-1);   // let it come to the front
                    using (var w = new StreamWriter(c)) w.WriteLine(path ?? "");
                    return true;
                }
            }
            catch { return false; }
        }

        // Calls onPath (on a background thread) for every path a second copy sends; "" = just show.
        public static void Listen(Action<string> onPath)
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var s = new NamedPipeServerStream(PipeName, PipeDirection.In, 1))
                        {
                            s.WaitForConnection();
                            using (var r = new StreamReader(s)) onPath(r.ReadLine() ?? "");
                        }
                    }
                    catch (IOException) { Thread.Sleep(500); }
                    catch { return; }
                }
            }) { IsBackground = true, Name = "Hatpick single instance" };
            t.Start();
        }
    }
}
