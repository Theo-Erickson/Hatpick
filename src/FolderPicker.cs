using System.Linq;
using Avalonia.Platform.Storage;

namespace Hatpick
{
    // The system's own "Select folder" and "Open" dialogs (Explorer's on Windows, Finder's
    // on macOS, the desktop's on Linux). They're asynchronous; these wait for the answer.
    static class FolderPicker
    {
        public static string Pick(Window owner, string title)
        {
            var sp = Storage(owner);
            if (sp == null || !sp.CanPickFolder) return null;
            var picked = Dialogs.Wait(sp.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false }));
            var path = picked == null ? null : picked.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
            return path == null ? null : Settings.Norm(path);
        }

        public static string PickFile(Window owner, string title, string kind, params string[] patterns)
        {
            var sp = Storage(owner);
            if (sp == null || !sp.CanOpen) return null;
            var picked = Dialogs.Wait(sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title, AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType(kind) { Patterns = patterns } },
            }));
            return picked == null ? null : picked.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
        }

        static IStorageProvider Storage(Window owner)
        {
            var top = owner ?? MainWindow.Current;
            return top == null ? null : top.StorageProvider;
        }
    }
}
