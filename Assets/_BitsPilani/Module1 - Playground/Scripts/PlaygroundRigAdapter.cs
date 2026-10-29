using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundRigAdapter : MonoBehaviour {
        public Transform playerRoot, head, cameraRig;
        public BNG.BNGPlayerController playerController;
        public BNG.PlayerGravity gravity;
        public CharacterController body;
        public Behaviour[] locomotionBehaviours;
        public BNG.VRUISystem vrUi;
        public InputSystemUIInputModule desktopUi;
        public BNG.UIPointer[] controllerPointers;
        public Canvas[] moduleCanvases;
        [Tooltip("Enable only for mouse/keyboard preview. Leave off for controller-ray interaction, including XR simulators.")]
        public bool desktopPreview = false;
        public float desktopMoveSpeed = 2f;
        public bool MovementLocked { get; private set; }
        public bool InChildView { get; private set; }
        readonly Dictionary<Behaviour, bool> movementStates = new Dictionary<Behaviour, bool>();
        Vector3 parentPosition, parentCameraOffset;
        Quaternion parentRotation;
        bool playerWasEnabled, gravityWasEnabled, bodyWasEnabled;
        float pitch;
        bool desktopActive, inputConfigured;
        readonly List<UnityEngine.XR.InputDevice> trackedControllers = new List<UnityEngine.XR.InputDevice>();

        IEnumerator Start() {
            // The reused head-gaze reticle is visual feedback, not an interactable UI surface.
            // Configure this new scene instance only; never edit the shared Pointer package script/prefab.
            foreach (var gaze in GetComponentsInChildren<global::Pointer>(true)) {
                foreach (var graphic in gaze.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
                foreach (var raycaster in gaze.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true)) raycaster.enabled = false;
            }
            ConfigureUIInput();
            // BNG's Start assigns cameras on every canvas. Reapply our mode afterwards.
            yield return null;
            ConfigureUIInput();
        }

        bool UseDesktopInput() {
            if (!desktopPreview || XRSettings.isDeviceActive) return false;
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, trackedControllers);
            return trackedControllers.Count == 0;
        }

        public void ConfigureUIInput() {
            bool desktop = UseDesktopInput();
            desktopActive = desktop; inputConfigured = true;
            if (vrUi) {
                foreach (var p in controllerPointers) if (p) {
                    if (p.ControllerSide == BNG.ControllerHand.Right) {
                        vrUi.RightPointerTransform = p.transform;
                        if (!desktop) p.gameObject.SetActive(true);
                    } else vrUi.LeftPointerTransform = p.transform;
                }
                vrUi.SelectedHand = BNG.ControllerHand.Right;
                vrUi.ControllerInput = new List<BNG.ControllerBinding> { BNG.ControllerBinding.RightTrigger };
                vrUi.UpdateControllerHand(BNG.ControllerHand.Right);
            }
            if (desktopUi) { desktopUi.enabled = desktop; if (desktop) desktopUi.AssignDefaultActions(); }
            if (vrUi) vrUi.enabled = !desktop;
            foreach (var p in controllerPointers) if (p) p.enabled = !desktop;
            if (desktop) {
                foreach (var p in controllerPointers) if (p) {
                    p.enabled = false;
                    if (p.lineRenderer) p.lineRenderer.enabled = false;
                }
                foreach (var c in moduleCanvases) if (c) c.worldCamera = head.GetComponent<Camera>();
            } else if (vrUi) {
                foreach (var c in moduleCanvases) if (c) vrUi.AddCanvas(c);
            }
            Debug.Log("[M1 Playground][UIInput] " + (desktop ? "Desktop mouse preview" : "Right-controller ray and trigger") + "; pointer=" + (vrUi && vrUi.RightPointerTransform ? vrUi.RightPointerTransform.name : "unassigned"));
        }

        public void SetMovementLocked(bool locked) {
            if (MovementLocked == locked) return;
            MovementLocked = locked;
            if (locked) {
                // Include current rig components as well as Inspector references. New or replaced
                // locomotion components must not bypass the first observation's movement lock.
                var drivers = new HashSet<Behaviour>();
                if (locomotionBehaviours != null) foreach (var behaviour in locomotionBehaviours) if (behaviour) drivers.Add(behaviour);
                if (playerRoot) foreach (var behaviour in playerRoot.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (behaviour is BNG.LocomotionManager || behaviour is BNG.SmoothLocomotion || behaviour is BNG.PlayerTeleport || behaviour is BNG.PlayerRotation) drivers.Add(behaviour);
                }
                foreach (var behaviour in drivers) { movementStates[behaviour] = behaviour.enabled; behaviour.enabled = false; }
            } else {
                foreach (var entry in movementStates) if (entry.Key) entry.Key.enabled = entry.Value;
                movementStates.Clear();
            }
            Debug.Log("[M1 Playground][Movement] Controller movement " + (locked ? "locked" : "restored") + "; head tracking remains active.");
        }

        public bool IsAt(Transform point, float radius) {
            if (!head || !point) return false;
            Vector3 d = head.position - point.position; d.y = 0; return d.sqrMagnitude <= radius * radius;
        }

        public void Face(Transform target) {
            Vector3 d = target.position - head.position; d.y = 0;
            if (d.sqrMagnitude < .001f) return;
            float desired = Quaternion.LookRotation(d).eulerAngles.y;
            float delta = Mathf.DeltaAngle(head.eulerAngles.y, desired);
            playerRoot.RotateAround(head.position, Vector3.up, delta);
        }

        public void EnterChildView(Transform floorPoint, float eyeHeight) {
            if (InChildView) return;
            parentPosition = playerRoot.position; parentRotation = playerRoot.rotation; parentCameraOffset = cameraRig.localPosition;
            SetMovementLocked(true);
            playerWasEnabled = playerController.enabled; gravityWasEnabled = gravity && gravity.enabled; bodyWasEnabled = body && body.enabled;
            playerController.enabled = false; if (gravity) gravity.enabled = false; if (body) body.enabled = false;
            float yawDelta = Mathf.DeltaAngle(head.eulerAngles.y, floorPoint.eulerAngles.y);
            playerRoot.RotateAround(head.position, Vector3.up, yawDelta);
            Vector3 delta = floorPoint.position - head.position; delta.y = 0;
            playerRoot.position += delta;
            cameraRig.position += Vector3.up * (floorPoint.position.y + eyeHeight - head.position.y);
            InChildView = true;
        }

        public void ReturnToParentView() {
            if (!InChildView) return;
            playerRoot.SetPositionAndRotation(parentPosition, parentRotation); cameraRig.localPosition = parentCameraOffset;
            playerController.enabled = playerWasEnabled; if (gravity) gravity.enabled = gravityWasEnabled; if (body) body.enabled = bodyWasEnabled;
            InChildView = false; SetMovementLocked(false);
        }

        void Update() {
            if (!inputConfigured || desktopActive != UseDesktopInput()) ConfigureUIInput();
            if (!desktopActive || Keyboard.current == null || MovementLocked || InChildView) return;
            var keys = Keyboard.current;
            Vector3 motion = Vector3.zero;
            if (keys.wKey.isPressed) motion += Vector3.forward; if (keys.sKey.isPressed) motion += Vector3.back;
            if (keys.aKey.isPressed) motion += Vector3.left; if (keys.dKey.isPressed) motion += Vector3.right;
            motion = Quaternion.Euler(0, head.eulerAngles.y, 0) * motion.normalized * desktopMoveSpeed * Time.deltaTime;
            if (body && body.enabled) body.Move(motion);
            if (keys.qKey.isPressed) playerRoot.RotateAround(head.position, Vector3.up, -60f * Time.deltaTime);
            if (keys.eKey.isPressed) playerRoot.RotateAround(head.position, Vector3.up, 60f * Time.deltaTime);
        }

        void LateUpdate() {
            if (!desktopActive || !head || Mouse.current == null) return;
            if (Mouse.current.rightButton.isPressed) {
                Vector2 delta = Mouse.current.delta.ReadValue();
                playerRoot.RotateAround(head.position, Vector3.up, delta.x * .08f);
                pitch = Mathf.Clamp(pitch - delta.y * .08f, -70f, 70f);
            }
            // Only the desktop preview uses a synthetic pitch; headset tracking remains untouched.
            head.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void OnDisable() { ReturnToParentView(); SetMovementLocked(false); }
    }
}
