using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [Serializable] public class PlaygroundLogEntry {
        public string utc, step, action, detail;
        public float elapsed;
    }
    [Serializable] public class PlaygroundGuess {
        public string idea;
        public int confidence;
        public string ConfidenceLabel => confidence == 0 ? "Not sure" : confidence == 1 ? "Somewhat sure" : "Very sure";
    }
    [Serializable] public class PlaygroundSessionData {
        public string sessionId, startedUtc, completedUtc;
        public List<string> clues = new List<string>();
        public List<PlaygroundGuess> guesses = new List<PlaygroundGuess>();
        public string firstResponse = "No response", firstOutcome = "Not observed";
        public float firstResponseSeconds = -1;
        public string[] reflectionAnswers = new string[3];
        public int replayCount;
        public bool replayIntervenedEarly;
        public float replaySecondsBeforePop;
        public string replayResponse = "No response", replayOutcome = "Not observed";
        public List<PlaygroundLogEntry> events = new List<PlaygroundLogEntry>();
    }

    public sealed class PlaygroundSessionLog : MonoBehaviour {
        public bool saveSessionToDisk = true;
        public PlaygroundSessionData Data { get; private set; }
        public string LastSavedPath { get; private set; }
        float started;
        bool exported;

        public void BeginSession() {
            started = Time.realtimeSinceStartup;
            exported = false;
            Data = new PlaygroundSessionData {
                sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"),
                startedUtc = DateTime.UtcNow.ToString("o")
            };
            Record("Loading", "SessionStarted", "Module 1 / Too Loud on the Slide");
        }

        public void Record(string step, string action, string detail = "") {
            if (Data == null) return;
            Data.events.Add(new PlaygroundLogEntry {
                utc = DateTime.UtcNow.ToString("o"), elapsed = Time.realtimeSinceStartup - started,
                step = step, action = action, detail = detail
            });
            Debug.Log($"[M1 Playground][{Data.sessionId}][{step}][{action}] {detail}", this);
        }

        public void Finish() {
            if (Data == null || exported) return;
            Data.completedUtc = DateTime.UtcNow.ToString("o");
            Record("Summary", "ModuleCompleted", $"Replays={Data.replayCount}; early prevention={Data.replayIntervenedEarly}");
            exported = true;
            if (!saveSessionToDisk) return;
            try {
                string folder = Path.Combine(Application.persistentDataPath, "Module1Playground");
                Directory.CreateDirectory(folder);
                LastSavedPath = Path.Combine(folder, "Session_" + Data.sessionId + ".json");
                File.WriteAllText(LastSavedPath, JsonUtility.ToJson(Data, true));
                Debug.Log("[M1 Playground][SessionSaved] " + LastSavedPath, this);
            } catch (Exception ex) {
                Debug.LogError("[M1 Playground][SessionSaveFailed] " + ex.Message, this);
            }
        }
    }
}
