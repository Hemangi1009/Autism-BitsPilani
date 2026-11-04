using System;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [Serializable] public class PlaygroundUITextEntry {
        public string id;
        [Tooltip("The original use of this message.")] public string context;
        [TextArea(2,8)] public string text;
    }
    public sealed class PlaygroundUITextCatalog : MonoBehaviour {
        public PlaygroundUITextEntry[] entries;
        public string Get(string id) {
            if (entries != null) foreach (var entry in entries) if (entry != null && entry.id == id) return entry.text ?? "";
            Debug.LogWarning("[Module UI] Missing authored text: " + id, this); return id;
        }
    }
}
