// Hatpick
// Picks which Git account (hat) each repo commits and pushes as, explains what any
// repo is set to and why, and can fix the author of commits before or after pushing.
// It never signs in or sees a password or token: Git Credential Manager keeps the
// sign-ins, Hatpick only says which one to use.
//
// Runs on Windows, macOS and Linux (Avalonia). Build with build.cmd or build.sh.

using System;
using System.IO;
using System.Threading;

namespace Hatpick
{
    class App : Application
    {
        public override void Initialize() { AppTheme.Init(this); }
    }

    static class Program
    {
        public const string Title = "Hatpick";

        public static string LogPath { get { return Path.Combine(Platform.DataDir, "hatpick-error.log"); } }

        [STAThread]
        static void Main(string[] args)
        {
            args = Demo.TakeArgs(args);   // --demo: made-up accounts and repos, nothing real touched
            string openPath = args.Length > 0 ? args[0].Trim().Trim('"') : null;
            // Already open: hand it the folder (if any) and let it come forward.
            if (Platform.HandToRunningCopy(openPath)) return;

            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .With(new Win32PlatformOptions { OverlayPopups = true })
                .With(new X11PlatformOptions { OverlayPopups = true })
                .With(new AvaloniaNativePlatformOptions { OverlayPopups = true })
                .SetupWithoutStarting();
            Dispatcher.UIThread.UnhandledException += (s, e) =>
            {
                Report(e.Exception);
                e.Handled = true;
            };
            try
            {
                if (Git.Exe == null)
                {
                    Dialogs.Info(null, "Couldn't find git", "Hatpick needs git to read and change your repos.\n\n" + Platform.GitInstall);
                    return;
                }
                var settings = Demo.On ? Demo.Build() : Settings.Load();
                AppTheme.Apply(Application.Current, settings.theme);
                var w = new MainWindow(settings, openPath);
                Platform.Listen(path => Dispatcher.UIThread.Post(() => w.HandOver(path)));
                var done = new CancellationTokenSource();
                w.Closed += delegate { done.Cancel(); };
                w.Show();
                Dispatcher.UIThread.MainLoop(done.Token);
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        // Anything unexpected: write it to the log and say where, so it can be fixed.
        public static void Report(Exception e)
        {
            string text = DateTime.Now + (Demo.On ? " (demo)" : "") + "\n" + e + "\n\n";
            try { Directory.CreateDirectory(Platform.DataDir); File.AppendAllText(LogPath, text); } catch { }
            try { Dialogs.Info(null, "Something went wrong", e.Message + "\n\nDetails were saved to:\n" + LogPath); } catch { }
        }
    }
}
