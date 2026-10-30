using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [Serializable] public sealed class PlaygroundQuestStorageSettings {
        public bool enabled;
        public bool android;
        public string folderName = "Quest";
        public string subfolderName = "bitspilani";
        public string adbExecutable;
        public string deviceSerial;
        public string displayFileName;
        public bool keepLocalCopy;
    }
    [Serializable] public sealed class PlaygroundQuestTransfer {
        public string localPath;
        public PlaygroundQuestStorageSettings settings;
    }
    public sealed class PlaygroundQuestExportResult {
        public bool success;
        public string location;
        public string error;
    }

    public static class PlaygroundQuestVideoStorage {
        const string PendingSuffix = ".quest-transfer.json";
        public static bool IsValidFolderName(string name) { return !string.IsNullOrEmpty(name) && name == name.Trim() && Regex.IsMatch(name, "\\A[A-Za-z0-9][A-Za-z0-9 &_\u002d]{0,63}\\z"); }
        public static string RelativeFolder(string name, string subfolder = "") {
            if (!IsValidFolderName(name)) throw new ArgumentException("Quest folder name must use letters, numbers, spaces, underscore or hyphen (1-64 characters), without path separators.");
            if (string.IsNullOrEmpty(subfolder)) return name;
            if (subfolder.Split('/').Any(segment => !IsValidFolderName(segment))) throw new ArgumentException("Set valid module and participant folder names.");
            return name + "/" + subfolder;
        }
        public static string Queue(string path, PlaygroundQuestStorageSettings settings) {
            RelativeFolder(settings.folderName, settings.subfolderName);
            string sidecar = path + PendingSuffix;
            File.WriteAllText(sidecar, JsonUtility.ToJson(new PlaygroundQuestTransfer { localPath = path, settings = settings }, true));
            return sidecar;
        }
        public static async Task<PlaygroundQuestExportResult> PublishAsync(string path, PlaygroundQuestStorageSettings settings) {
            try {
                if (settings.subfolderName == null) settings.subfolderName = "";
                RelativeFolder(settings.folderName, settings.subfolderName);
                if (!File.Exists(path)) throw new FileNotFoundException("The finalized recording is missing.", path);
                return settings.android ? await PublishAndroidAsync(path, settings).ConfigureAwait(false) : await Task.Run(() => PublishDesktop(path, settings)).ConfigureAwait(false);
            } catch (Exception ex) { return new PlaygroundQuestExportResult { error = ex.Message }; }
        }
        public static void Acknowledge(string path, PlaygroundQuestStorageSettings settings) {
            // Remove the PC/app-private staging file only AFTER successful verified publication.
            if (!settings.keepLocalCopy && File.Exists(path)) { try { File.Delete(path); } catch (IOException) { Debug.LogWarning("[M1 Playground][QuestVideoSaved] Headset video saved; temporary source is still in use and was retained."); } catch (UnauthorizedAccessException) { Debug.LogWarning("[M1 Playground][QuestVideoSaved] Headset video saved; temporary source could not be removed."); } }
            string sidecar = path + PendingSuffix;
            if (File.Exists(sidecar)) { try { File.Delete(sidecar); } catch (IOException) { Debug.LogWarning("[M1 Playground][QuestVideoSaved] Headset video saved; transfer receipt could not be removed."); } catch (UnauthorizedAccessException) { Debug.LogWarning("[M1 Playground][QuestVideoSaved] Headset video saved; transfer receipt could not be removed."); } }
        }
        public static async Task<int> RetryPendingAsync(string directory, string expectedRoot = null) {
            if (!Directory.Exists(directory)) return 0;
            int saved = 0;
            foreach (string sidecar in Directory.GetFiles(directory, "*" + PendingSuffix, SearchOption.AllDirectories)) {
                try {
                    var job = JsonUtility.FromJson<PlaygroundQuestTransfer>(File.ReadAllText(sidecar));
                    if (job == null || job.settings == null || !File.Exists(job.localPath)) continue;
                    if (!string.IsNullOrEmpty(expectedRoot) && job.settings.folderName != expectedRoot) continue;
                    string expectedSource = sidecar.Substring(0, sidecar.Length - PendingSuffix.Length);
                    if (!string.Equals(Path.GetFullPath(job.localPath), Path.GetFullPath(expectedSource), StringComparison.OrdinalIgnoreCase)) continue;
                    var result = await PublishAsync(job.localPath, job.settings);
                    if (result.success) { Acknowledge(job.localPath, job.settings); saved++; Debug.Log("[M1 Playground][QuestVideoSaved] " + result.location); }
                    else { Debug.LogWarning("[M1 Playground][QuestTransferPending] " + result.error); break; }
                } catch (Exception ex) { Debug.LogWarning("[M1 Playground][QuestTransferPending] " + ex.Message); }
            }
            return saved;
        }

        static Task<PlaygroundQuestExportResult> PublishAndroidAsync(string path, PlaygroundQuestStorageSettings settings) {
            return PlaygroundQuestFolderAccess.PublishAsync(path, settings);
        }

        public static PlaygroundQuestExportResult PublishDesktop(string path, PlaygroundQuestStorageSettings settings) {
            try {
                string folder = RelativeFolder(settings.folderName, settings.subfolderName);
                if (!Regex.IsMatch(Path.GetFileName(path), "\\A[A-Za-z0-9_.-]+\\.mp4\\z")) throw new ArgumentException("Unexpected recording filename; transfer was not started.");
                if (string.IsNullOrEmpty(settings.adbExecutable) || !File.Exists(settings.adbExecutable)) throw new FileNotFoundException("Set the ADB Executable field to Android SDK/platform-tools/adb.exe for Quest Link transfers.");
                string serial = ResolveQuest(settings);
                EnsureDesktopFolders(settings, serial);
                string videoName = string.IsNullOrEmpty(settings.displayFileName) ? Path.GetFileName(path) : settings.displayFileName;
                if (!Regex.IsMatch(videoName, "\\A[A-Za-z0-9_.:-]+\\.mp4\\z")) throw new ArgumentException("Invalid headset video filename.");
                string destination = "/sdcard/" + folder + "/" + videoName;
                string pending = destination + ".uploading";
                Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "mkdir -p '" + "/sdcard/" + folder + "'" }, 10000);
                try { Adb(settings.adbExecutable, new[] { "-s", serial, "push", path, pending }, 120000); }
                catch (IOException) {
                    if (!videoName.Contains(":")) throw;
                    videoName = videoName.Replace(':', '-'); destination = "/sdcard/" + folder + "/" + videoName; pending = destination + ".uploading";
                    Adb(settings.adbExecutable, new[] { "-s", serial, "push", path, pending }, 120000);
                    Debug.LogWarning("[FolderManager] Headset filesystem rejected colons; used compatible video time separators.");
                }
                string size = Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "stat -c %s '" + pending + "'" }, 10000).Trim();
                if (!long.TryParse(size, out long remoteSize) || remoteSize != new FileInfo(path).Length) throw new IOException("The Quest video transfer did not match the local file size. The local recording was retained.");
                string localHash;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var input = File.OpenRead(path)) localHash = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                string remoteHash = Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "sha256sum '" + pending + "'" }, 10000).Trim().Split(' ')[0];
                if (!string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Quest video checksum did not match; local recording retained.");
                // Destination names contain an ID and millisecond timestamp; mv -n avoids replacing an existing video.
                Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "mv -n '" + pending + "' '" + destination + "'" }, 10000);
                string finalSize = Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "stat -c %s '" + destination + "'" }, 10000).Trim();
                if (!long.TryParse(finalSize, out long publishedSize) || publishedSize != remoteSize) throw new IOException("The final Quest video could not be verified; local recording retained.");
                string finalHash = Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "sha256sum '" + destination + "'" }, 10000).Trim().Split(' ')[0];
                if (!string.Equals(localHash, finalHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("An existing Quest video differs from this session. It was not replaced; local recording retained.");
                try { Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d 'file://" + destination + "'" }, 10000); } catch { }
                return new PlaygroundQuestExportResult { success = true, location = destination };
            } catch (Exception ex) { return new PlaygroundQuestExportResult { error = ex.Message }; }
        }
        static string ResolveQuest(PlaygroundQuestStorageSettings settings) {
            if (string.IsNullOrEmpty(settings.adbExecutable) || !File.Exists(settings.adbExecutable)) throw new FileNotFoundException("Set ADB Executable for Quest Link transfer.");
                string devices = Adb(settings.adbExecutable, new[] { "devices", "-l" }, 10000);
                var connected = new List<string>();
                foreach (string line in devices.Split('\n')) {
                    var columns = Regex.Split(line.Trim(), "\\s+");
                    if (columns.Length > 1 && columns[1] == "device" && Regex.IsMatch(columns[0], "\\A[A-Za-z0-9_.:-]+\\z")) connected.Add(columns[0]);
                }
                string serial = settings.deviceSerial;
                if (!string.IsNullOrEmpty(serial)) { if (!connected.Contains(serial)) throw new IOException("The selected Quest is not connected or USB debugging is not authorized."); }
                else { if (connected.Count != 1) throw new IOException(connected.Count == 0 ? "Connect the Quest by USB and allow USB debugging in the headset. The PC recording is safely queued." : "More than one Android device is connected; set Quest Device Serial to choose the headset."); serial = connected[0]; }
                string manufacturer = Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "getprop ro.product.manufacturer" }, 10000).Trim();
                string model = Adb(settings.adbExecutable, new[] { "-s", serial, "shell", "getprop ro.product.model" }, 10000).Trim();
                if (model.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) < 0 && manufacturer.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) < 0 && manufacturer.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) < 0) throw new IOException("The connected Android device was not identified as a Meta/Oculus Quest; no video was transferred.");
            return serial;
        }
        static void EnsureDesktopFolders(PlaygroundQuestStorageSettings settings, string serial) {
            string root = RelativeFolder(settings.folderName);
            var paths = new List<string>();
            for (int i=1;i<=4;i++) paths.Add("'/sdcard/" + root + "/Module" + i + "'");
            if (!string.IsNullOrEmpty(settings.subfolderName)) paths.Add("'/sdcard/" + RelativeFolder(settings.folderName,settings.subfolderName) + "'");
            Adb(settings.adbExecutable,new[]{"-s",serial,"shell","mkdir -p " + string.Join(" ",paths)},10000);
        }
        public static async Task<PlaygroundQuestExportResult> PrepareFoldersAsync(PlaygroundQuestStorageSettings settings) {
            try {
                RelativeFolder(settings.folderName,settings.subfolderName);
                if (settings.android) return await PlaygroundQuestFolderAccess.PrepareAsync(settings);
                return await Task.Run(()=>{try {string serial=ResolveQuest(settings);EnsureDesktopFolders(settings,serial);return new PlaygroundQuestExportResult{success=true,location="/sdcard/"+RelativeFolder(settings.folderName,settings.subfolderName)};}catch(Exception ex){return new PlaygroundQuestExportResult{error=ex.Message};}});
            } catch(Exception ex){return new PlaygroundQuestExportResult{error=ex.Message};}
        }
        static string Adb(string executable, string[] arguments, int timeout) {
            string quoted = string.Join(" ", arguments.Select(arg => {
                if (arg.IndexOf('"') >= 0 || arg.IndexOf('\0') >= 0) throw new ArgumentException("Invalid ADB argument.");
                return "\"" + arg + "\"";
            }));
            var info = new System.Diagnostics.ProcessStartInfo(executable, quoted) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(info)) {
                if (process == null) throw new IOException("ADB could not start.");
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeout)) { try { process.Kill(); } catch { } throw new TimeoutException("Quest transfer timed out; local recording retained."); }
                string text = output.GetAwaiter().GetResult(); string error = errors.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new IOException("ADB: " + error.Trim());
                return text;
            }
        }
    }
}
