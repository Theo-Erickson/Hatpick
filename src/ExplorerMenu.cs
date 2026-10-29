using System;
using Microsoft.Win32;

namespace Hatpick
{
    // "Analyze with Hatpick" in the right-click menu of folders (and of a folder's empty
    // space). Lives under HKEY_CURRENT_USER, so it needs no admin rights and only affects you.
    // On Windows 11 it shows under "Show more options". Windows only: elsewhere, drop a
    // folder on the window or pass it on the command line (hatpick ~/code/my-repo).
    static class ExplorerMenu
    {
        public static bool Supported { get { return OperatingSystem.IsWindows(); } }

        static readonly string[] Keys =
        {
            @"Software\Classes\Directory\shell\Hatpick",
            @"Software\Classes\Directory\Background\shell\Hatpick"
        };

        public static bool Installed
        {
            get
            {
                if (Demo.On || !OperatingSystem.IsWindows()) return false;
                try { using (var k = Registry.CurrentUser.OpenSubKey(Keys[0])) return k != null; }
                catch { return false; }
            }
        }

        public static void Install(string exe)
        {
            if (Demo.On || !OperatingSystem.IsWindows()) return;   // the demo never touches the registry
            foreach (var path in Keys)
            {
                using (var k = Registry.CurrentUser.CreateSubKey(path))
                {
                    k.SetValue("", "Analyze with Hatpick");
                    k.SetValue("Icon", "\"" + exe + "\"");
                    using (var c = k.CreateSubKey("command"))
                        c.SetValue("", "\"" + exe + "\" \"%V\"");
                }
            }
        }

        public static void Remove()
        {
            if (Demo.On || !OperatingSystem.IsWindows()) return;
            foreach (var path in Keys)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(path, false); } catch { }
            }
        }
    }
}
