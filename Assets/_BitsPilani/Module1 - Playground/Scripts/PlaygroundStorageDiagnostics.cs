using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    // Captures app errors locally; never requests storage permission or opens a file browser.
    public static class PlaygroundStorageDiagnostics {
        static readonly object Sync = new object();
        static readonly Queue<string> Recent = new Queue<string>();
        static bool ready;
        static int entries;
        public static string LogFilePath { get; private set; }
        public static bool WriteCheckSucceeded { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() {
            Application.logMessageReceivedThreaded -= Capture;
            lock(Sync) { ready=false;entries=0;Recent.Clear(); }
            WriteCheckSucceeded=false;
            string folder=Path.Combine(Application.persistentDataPath,"StorageDiagnostics");
            LogFilePath=Path.Combine(folder,"LastRun.txt");
            try {
                Directory.CreateDirectory(folder);
                if(File.Exists(LogFilePath))File.Copy(LogFilePath,Path.Combine(folder,"PreviousRun.txt"),true);
                File.WriteAllText(LogFilePath,
                    "Storage diagnostic run: " + DateTime.Now.ToString("O")
                    + "\nPlatform: " + Application.platform
                    + "\nApplication: " + Application.identifier
                    + "\npersistentDataPath: " + Application.persistentDataPath
                    + "\ndataPath: " + Application.dataPath
                    + "\nstreamingAssetsPath: " + Application.streamingAssetsPath
                    + "\nPersistent app-storage write check: SUCCESS\n");
                lock(Sync)ready=true;
                WriteCheckSucceeded=true;
            } catch(Exception ex) {
                Debug.LogError("[StorageCheck] Failed to write diagnostic file: " + LogFilePath + "\n" + ex);
            }
            Application.logMessageReceivedThreaded += Capture;
            if(WriteCheckSucceeded)Debug.Log("[StorageCheck] Persistent app-storage write succeeded. Diagnostic file: " + LogFilePath);
        }

        static string Limit(string text,int length) { return text==null?"":text.Length>length?text.Substring(0,length)+" [truncated]":text; }
        static void Capture(string message,string stackTrace,LogType type) {
            if(message!=null && message.StartsWith("[StorageDiagnostics][SavedLog]",StringComparison.Ordinal))return;
            bool denied=message!=null&&(message.IndexOf("permission denied",StringComparison.OrdinalIgnoreCase)>=0
                ||message.IndexOf("EACCES",StringComparison.OrdinalIgnoreCase)>=0
                ||message.IndexOf("UnauthorizedAccess",StringComparison.OrdinalIgnoreCase)>=0);
            bool saveEvent=message!=null&&(message.StartsWith("[FolderManager] Saving session to:",StringComparison.Ordinal)
                ||message.StartsWith("[M1 Playground][RecordingSaved]",StringComparison.Ordinal));
            bool error=type==LogType.Error||type==LogType.Exception||type==LogType.Assert||denied;
            lock(Sync) {
                if(ready&&(error||saveEvent)&&entries<50) {
                    try {
                        string context=error?"\nRecent app messages:\n"+string.Join("\n",Recent.ToArray()):"";
                        File.AppendAllText(LogFilePath,"\n["+DateTime.Now.ToString("O")+"]["+type+"] "+Limit(message,8192)
                            +context+"\nStack trace:\n"+Limit(stackTrace,8192)+"\n");
                        entries++;
                    } catch { ready=false; } // Avoid a recursive logging failure if storage becomes unavailable.
                }
                Recent.Enqueue(Limit(message,2048));
                while(Recent.Count>8)Recent.Dequeue();
            }
        }

        public static void PrintSavedLog() {
            string path=LogFilePath??Path.Combine(Application.persistentDataPath,"StorageDiagnostics","LastRun.txt");
            try { Debug.Log("[StorageDiagnostics][SavedLog] " + path + "\n" + (File.Exists(path)?File.ReadAllText(path):"No saved run log exists yet.")); }
            catch(Exception ex) { Debug.LogError("[StorageDiagnostics] Could not read " + path + "\n" + ex); }
        }
    }
}
