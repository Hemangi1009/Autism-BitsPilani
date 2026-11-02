using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace BitsPilani.Module1.Playground {
    // Permission only. This does not start a microphone or change the game-audio recorder.
    public static class PlaygroundMicrophonePermission {
        public static IEnumerator RequestAtModuleStart() {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) {
                Debug.Log("[MicrophonePermission] Already granted."); yield break;
            }
            var callbacks = new PermissionCallbacks();
            bool completed = false;
            Action<string> granted = _ => { if (completed) return; completed = true; Debug.Log("[MicrophonePermission] Granted."); };
            Action<string> denied = _ => { if (completed) return; completed = true; Debug.LogWarning("[MicrophonePermission] Denied. The module will continue without microphone access."); };
            Action<string> dismissed = _ => { if (completed) return; completed = true; Debug.LogWarning("[MicrophonePermission] Dismissed. The module will continue without microphone access."); };
            callbacks.PermissionGranted += granted;
            callbacks.PermissionDenied += denied;
            callbacks.PermissionRequestDismissed += dismissed;
#pragma warning disable CS0618
            callbacks.PermissionDeniedAndDontAskAgain += denied;
#pragma warning restore CS0618
            try {
                try {
                    Debug.Log("[MicrophonePermission] Requesting access at module start, before video capture begins.");
                    Permission.RequestUserPermission(Permission.Microphone, callbacks);
                } catch (Exception ex) {
                    completed = true;
                    Debug.LogWarning("[MicrophonePermission] Request could not be shown; continuing the module. " + ex.Message);
                }
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!completed && Time.realtimeSinceStartup < deadline) yield return null;
                if (!completed) Debug.LogWarning("[MicrophonePermission] No permission result received; continuing the module without blocking startup.");
            } finally {
                callbacks.PermissionGranted -= granted;
                callbacks.PermissionDenied -= denied;
                callbacks.PermissionRequestDismissed -= dismissed;
#pragma warning disable CS0618
                callbacks.PermissionDeniedAndDontAskAgain -= denied;
#pragma warning restore CS0618
            }
#else
            // Quest Link is a Windows process, not an Android permission context.
            yield break;
#endif
        }
    }
}
