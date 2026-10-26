// Hatpick
// Picks which Git account (hat) each repo commits and pushes as, explains what any
// repo is set to and why, and can fix the author of commits before or after pushing.
// It never signs in or sees a password or token: Git Credential Manager keeps the
// sign-ins, Hatpick only says which one to use.
//
// Build with build.cmd (uses the C# compiler that ships with Windows). C# 5 only.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

[assembly: System.Reflection.AssemblyTitle("Hatpick")]
[assembly: System.Reflection.AssemblyProduct("Hatpick")]
[assembly: System.Reflection.AssemblyDescription("Pick which Git account each repo commits and pushes as")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

namespace Hatpick
{
    static class Program
    {
        public const string Title = "Hatpick";
        public const int WM_COPYDATA = 0x004A;

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        public static string ExePath { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
        public static string LogPath { get { return Path.Combine(Path.GetDirectoryName(ExePath), "hatpick-error.log"); } }

        [STAThread]
        static void Main(string[] args)
        {
            string openPath = args.Length > 0 ? args[0].Trim().Trim('"') : null;
            bool first;
            using (var mutex = new Mutex(true, "Hatpick.SingleInstance", out first))
            {
                if (!first)
                {
                    // Already open: hand it the folder (if any) and bring it forward.
                    IntPtr h = FindWindow(null, Title);
                    if (h != IntPtr.Zero)
                    {
                        if (!string.IsNullOrEmpty(openPath)) SendPath(h, openPath);
                        ShowWindow(h, 9); SetForegroundWindow(h);
                    }
                    return;
                }
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Report(e.Exception);
                    e.Handled = true;
                };
                try
                {
                    if (Git.Exe == null)
                    {
                        MessageBox.Show("Couldn't find git.exe.\n\nInstall Git for Windows (git-scm.com), then open Hatpick again.",
                            Title, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    var settings = Settings.Load();
                    Theme.Current = settings.theme;
                    Theme.Init(app);
                    var w = new MainWindow(settings, openPath);
                    app.Run(w);
                }
                catch (Exception e)
                {
                    Report(e);
                }
            }
        }

        // Anything unexpected: write it next to the .exe and say where, so it can be fixed.
        public static void Report(Exception e)
        {
            string text = DateTime.Now + "\n" + e + "\n\n";
            try { File.AppendAllText(LogPath, text); } catch { }
            MessageBox.Show("Something went wrong:\n\n" + e.Message + "\n\nDetails were saved to:\n" + LogPath,
                Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        static void SendPath(IntPtr hwnd, string path)
        {
            IntPtr mem = Marshal.StringToHGlobalUni(path);
            try
            {
                var cds = new COPYDATASTRUCT { dwData = (IntPtr)1, cbData = (path.Length + 1) * 2, lpData = mem };
                SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, ref cds);
            }
            finally { Marshal.FreeHGlobal(mem); }
        }

        public static string ReadCopyData(IntPtr lParam)
        {
            var cds = (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));
            return Marshal.PtrToStringUni(cds.lpData);
        }
    }
}
