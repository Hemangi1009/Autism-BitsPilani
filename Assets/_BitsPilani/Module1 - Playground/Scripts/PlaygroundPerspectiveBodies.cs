using System.Collections.Generic;
using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundPerspectiveBodies : MonoBehaviour {
        public Transform childBodyRoot;
        public GameObject parentBody;
        public Renderer[] parentRigBodyRenderers;
        public bool IsShowingChildView { get; private set; }
        readonly Dictionary<Renderer, bool> visibility = new Dictionary<Renderer, bool>();
        bool parentWasActive;
        Vector3 originalParentPosition;
        Quaternion originalParentRotation;

        public void EnterChildView(PlaygroundRigAdapter rig) {
            if (IsShowingChildView) return;
            IsShowingChildView = true;
            visibility.Clear();
            if (childBodyRoot) foreach (var renderer in childBodyRoot.GetComponentsInChildren<Renderer>(true)) Hide(renderer);
            if (parentRigBodyRenderers != null) foreach (var renderer in parentRigBodyRenderers) Hide(renderer);
            if (parentBody) {
                parentWasActive = parentBody.activeSelf;
                originalParentPosition = parentBody.transform.position;
                originalParentRotation = parentBody.transform.rotation;
                Vector3 feet = rig.head.position;
                feet.y = rig.body ? rig.body.bounds.min.y : rig.playerRoot.position.y;
                if (PlaygroundSurfaceAnchor.TrySupport(feet, out float surface)) feet.y = surface;
                Vector3 forward = Vector3.ProjectOnPlane(rig.head.forward, Vector3.up);
                parentBody.transform.SetPositionAndRotation(feet, forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : rig.playerRoot.rotation);
                parentBody.SetActive(true);
            }
            Debug.Log("[M1 Playground][Perspective] Child body hidden; parent body shown at the position where the view button was clicked.");
        }

        void Hide(Renderer renderer) {
            if (!renderer || visibility.ContainsKey(renderer)) return;
            visibility.Add(renderer, renderer.enabled); renderer.enabled = false;
        }

        public void RestoreParentView() {
            if (!IsShowingChildView) return;
            foreach (var entry in visibility) if (entry.Key) entry.Key.enabled = entry.Value;
            visibility.Clear();
            if (parentBody) {
                parentBody.SetActive(parentWasActive);
                parentBody.transform.SetPositionAndRotation(originalParentPosition, originalParentRotation);
            }
            IsShowingChildView = false;
            Debug.Log("[M1 Playground][Perspective] Original child and parent-body visibility restored.");
        }
        void OnDisable() { RestoreParentView(); }
    }
}
