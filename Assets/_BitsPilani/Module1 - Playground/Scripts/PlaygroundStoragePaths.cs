using System;
using System.IO;

namespace BitsPilani.Module1.Playground {
    public static class PlaygroundStoragePaths {
        public static string SharedRootFromPersistent(string persistentPath) {
            var files = new DirectoryInfo(Path.GetFullPath(persistentPath));
            if (files.Name != "files" || files.Parent?.Parent?.Name != "data" || files.Parent?.Parent?.Parent?.Name != "Android")
                throw new IOException("Expected Android/data/<package>/files; refusing to guess a storage root from " + persistentPath);
            var root = files;
            for (int i = 0; i < 4; i++) root = root.Parent ?? throw new IOException("The Android storage path has too few parent folders.");
            return root.FullName;
        }
        // A path traversal changes the destination, not Android's storage access rules.
        // Cache this decision in FolderManager: do not repeatedly probe during recording.
        public static string ChooseQuestRoot(string persistentPath, string folderName, bool preferSharedRoot,
                Action<string> ensureWritable, out bool fallbackUsed, out string reason) {
            if (string.IsNullOrWhiteSpace(folderName) || folderName == "." || folderName == ".." || folderName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new ArgumentException("Root Folder Name must be a single folder name.");
            fallbackUsed = false; reason = null;
            if (preferSharedRoot) {
                try {
                    string shared = Path.Combine(SharedRootFromPersistent(persistentPath), folderName);
                    ensureWritable(shared); return shared;
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) {
                    fallbackUsed = true; reason = ex.Message;
                }
            }
            string appRoot = Path.Combine(persistentPath, folderName);
            ensureWritable(appRoot); return appRoot;
        }
        public static void EnsureWritable(string directory) {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".write-check-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.WriteByte(0); stream.Flush(); }
            } finally { if (File.Exists(probe)) File.Delete(probe); }
        }
    }
}
