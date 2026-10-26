// Hatpick
// Picks which Git account each repo commits and pushes as, and explains what any
// repo is set to and why. It never signs in or sees a password or token: Git
// Credential Manager keeps the sign-ins, Hatpick only says which one to use.
//
// Build with build.cmd (uses the C# compiler that ships with Windows). C# 5 only.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Hatpick")]
[assembly: System.Reflection.AssemblyProduct("Hatpick")]
[assembly: System.Reflection.AssemblyDescription("Pick which Git account each repo commits and pushes as")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]

namespace Hatpick
{
    static class Program
    {
        public const string Title = "Hatpick";
        public const int WM_COPYDATA = 0x004A;
        public static float Scale = 1f;
        public static int S(int px) { return (int)Math.Round(px * Scale); }
        public static readonly Font UiFont = new Font("Segoe UI", 9.5f);

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
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
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
                if (Git.Exe == null)
                {
                    MessageBox.Show("Couldn't find git.exe.\n\nInstall Git for Windows (git-scm.com), then open Hatpick again.",
                        Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Application.Run(new MainForm(openPath));
            }
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

        public static string ExePath { get { return Application.ExecutablePath; } }
    }
}
