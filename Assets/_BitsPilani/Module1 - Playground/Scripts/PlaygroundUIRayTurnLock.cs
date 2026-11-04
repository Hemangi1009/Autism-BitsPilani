using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    [DefaultExecutionOrder(-9000)]
    public sealed class PlaygroundUIRayTurnLock : MonoBehaviour {
        public BNG.VRUISystem vrUi;
        public BNG.PlayerRotation[] rotationDrivers;
        public bool desktopMousePreview;
        public bool IsBlockingTurn { get; private set; }
        readonly Dictionary<BNG.PlayerRotation,bool> original = new Dictionary<BNG.PlayerRotation,bool>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        public void SetRayOverUI(bool value) {
            if (value == IsBlockingTurn) return;
            IsBlockingTurn = value;
            if (value) {
                original.Clear();
                if (rotationDrivers != null) foreach (var driver in rotationDrivers) if (driver) { original[driver] = driver.AllowInput; driver.AllowInput = false; }
            } else { foreach (var pair in original) if (pair.Key) pair.Key.AllowInput = pair.Value; original.Clear(); }
        }
        void Update() {
            var events = EventSystem.current;
            if (!events) { SetRayOverUI(false); return; }
            PointerEventData data = null;
            if (vrUi && vrUi.enabled && vrUi.CameraCasterReady()) data = vrUi.EventData;
            else if (desktopMousePreview && Mouse.current != null) data = new PointerEventData(events) { position = Mouse.current.position.ReadValue() };
            if (data == null) { SetRayOverUI(false); return; }
            hits.Clear(); events.RaycastAll(data, hits);
            bool over = hits.Count > 0 && hits[0].gameObject && hits[0].module is GraphicRaycaster && hits[0].gameObject.GetComponentInParent<Canvas>();
            SetRayOverUI(over);
        }
        void OnDisable() { SetRayOverUI(false); }
    }
}
