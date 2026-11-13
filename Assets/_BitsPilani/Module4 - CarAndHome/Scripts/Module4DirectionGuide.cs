using UnityEngine;

namespace BitsPilani.Module4 {
    /// <summary>
    /// Read-only observer of the module: guides head direction without completing gazes,
    /// changing steps, moving the player, or modifying the animated prefab child.
    /// </summary>
    [DefaultExecutionOrder(10020)]
    public sealed class Module4DirectionGuide : MonoBehaviour {
        [Header("Scene references")]
        [Tooltip("Existing flow controller. Its state is read only; this guide never advances it.")]
        public Module4SceneController flow;
        [Tooltip("Tracked centre-eye camera. Arrow placement follows this transform.")]
        public Transform head;
        [Tooltip("Root of your Face_Arrow scene instance. Only this transform is moved/rotated. Do not assign Arrow06.")]
        public Transform arrowRoot;

        [Header("Look-angle thresholds (degrees)")]
        [Range(1, 90), Tooltip("Hide when the player looks within this angle of the required direction.")]
        public float hideWithinAngle = 35f;
        [Range(1, 90), Tooltip("Show again beyond this angle. Keep it slightly higher than Hide Within Angle to avoid flicker.")]
        public float showOutsideAngle = 40f;
        [Tooltip("Compare horizontal facing direction, so looking slightly up/down does not trigger the arrow. Turn off for full 3D alignment.")]
        public bool horizontalFacingOnly = true;

        [Header("Placement relative to the camera (metres)")]
        [Min(.15f), Tooltip("Distance in front of the tracked camera. The script preserves the Face_Arrow root's authored scale.")]
        public float cameraDistance = .85f;
        [Tooltip("Horizontal/vertical offset in the camera's view. Positive X is right; positive Y is up.")]
        public Vector2 cameraOffset = new Vector2(0, -.12f);

        [Header("Prefab direction calibration")]
        [Tooltip("Tip direction of the unrotated prefab in its XY plane: 0 = right, 90 = up, 180 = left. Your Face_Arrow points left, so use 180. Only its root is rotated.")]
        public float prefabTipAngle = 180f;

        [Header("Optional target override")]
        [Tooltip("Leave None to guide the ID panel, child, park checkpoint or current interaction panel automatically.")]
        public Transform targetOverride;

        [Header("Diagnostics")]
        [Tooltip("Log target changes and arrow show/hide transitions, rather than logging every frame.")]
        public bool logStateChanges = true;
        [Tooltip("Optional periodic angle diagnostics while a target is active.")]
        public bool logAngleSamples;
        [Min(.25f)] public float angleLogInterval = 2f;

        public bool ArrowVisible { get; private set; }
        public float CurrentLookAngle { get; private set; }
        public Transform CurrentTarget { get; private set; }
        float lastTurnSign = 1f;
        float nextAngleLogAt;
        bool missingReferencesLogged;

        void OnEnable() { Application.onBeforeRender += BeforeRender; }
        void OnDisable() {
            Application.onBeforeRender -= BeforeRender;
            if (Application.isPlaying) SetVisible(false, "Guide disabled");
        }
        void LateUpdate() { RefreshGuidance(Time.unscaledTime); }
        [BeforeRenderOrder(10000)]
        void BeforeRender() {
            // Re-read late XR poses without touching camera/controller tracking transforms.
            if (Application.isPlaying) RefreshGuidance(Time.unscaledTime);
        }

        public Transform ResolveTarget() {
            if(targetOverride)return targetOverride;
            if(!flow||!flow.ui||flow.IsTransitioning)return null;
            var ui=flow.ui;
            if(ui.idCanvas&&ui.idCanvas.gameObject.activeInHierarchy)return ui.idCanvas.transform;
            if(flow.WaitingForChildGaze)return flow.childGaze;
            if(flow.WaitingForWalkCheckpoint)return flow.walkCheckpoint;
            if(ui.childReturnCanvas&&ui.childReturnCanvas.gameObject.activeInHierarchy)return ui.childReturnCanvas.transform;
            if(flow.rig&&flow.rig.InChildView)return flow.parentBody?flow.parentBody.transform:flow.childGaze;
            bool observing=flow.CurrentStage==Module4Stage.WalkObserve || flow.CurrentStage==Module4Stage.ReplayWalk || flow.CurrentStage==Module4Stage.HomeIntro;
            bool continueReady=ui.primary&&ui.primary.gameObject.activeInHierarchy&&ui.primary.interactable;
            if(observing&&!continueReady&&flow.child)return flow.child.head?flow.child.head:flow.child.transform;
            return ui.mainCanvas&&ui.mainCanvas.gameObject.activeInHierarchy?ui.mainCanvas.transform:null;
        }

        public void RefreshGuidance(float now) {
            if (!head || !arrowRoot) {
                if (!missingReferencesLogged && logStateChanges) {
                    Debug.LogWarning("[M4][FaceArrow][Configuration] Assign the tracked camera and Face_Arrow root. No scene or flow state was changed.", this);
                    missingReferencesLogged = true;
                }
                SetVisible(false, "Missing references");
                return;
            }
            missingReferencesLogged = false;
            var target = ResolveTarget();
            bool sameTarget = target == CurrentTarget;
            if (!sameTarget) {
                CurrentTarget = target;
                if (logStateChanges) Debug.Log("[M4][FaceArrow][TargetChanged] " + (target ? target.name : "None (UI or no active world task)"), this);
            }
            if (!target) { CurrentLookAngle = 0; SetVisible(false, "No required world target"); return; }

            Vector3 direction = target.position - head.position;
            Vector3 forward = head.forward;
            if (horizontalFacingOnly) {
                direction = Vector3.ProjectOnPlane(direction, Vector3.up);
                forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            }
            if (direction.sqrMagnitude < .0025f) { CurrentLookAngle = 0; SetVisible(false, "Already at target"); return; }
            if (forward.sqrMagnitude < .0001f) forward = head.forward;
            CurrentLookAngle = Vector3.Angle(forward, direction);
            float hide = Mathf.Clamp(hideWithinAngle, 0, 179);
            float show = Mathf.Clamp(showOutsideAngle, hide, 180);
            bool shouldShow = CurrentLookAngle > (sameTarget && ArrowVisible ? hide : show);

            if (shouldShow) {
                var local = head.InverseTransformDirection(direction.normalized);
                if (Mathf.Abs(local.x) > .001f) lastTurnSign = Mathf.Sign(local.x);
                Vector2 screenDirection = new Vector2(local.x, local.y);
                // Directly behind has no unique screen direction. Keep a stable left/right turn cue.
                if (local.z < 0 || screenDirection.sqrMagnitude < .0001f)
                    screenDirection = new Vector2(lastTurnSign, 0);
                float screenAngle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg;
                Vector3 position = head.TransformPoint(new Vector3(cameraOffset.x, cameraOffset.y, Mathf.Max(.15f, cameraDistance)));
                Quaternion rotation = head.rotation * Quaternion.AngleAxis(screenAngle - prefabTipAngle, Vector3.forward);
                // Deliberately operate on Face_Arrow only. Arrow06/Animator/animation curves stay untouched.
                arrowRoot.SetPositionAndRotation(position, rotation);
            }
            SetVisible(shouldShow, shouldShow ? "Look away from required target" : "Facing required target");
            if (logAngleSamples && now >= nextAngleLogAt) {
                nextAngleLogAt = now + Mathf.Max(.25f, angleLogInterval);
                Debug.Log($"[M4][FaceArrow][Angle] target={target.name}; angle={CurrentLookAngle:F1}; hideWithin={hide:F1}; showOutside={show:F1}; visible={ArrowVisible}", this);
            }
        }

        void SetVisible(bool visible, string reason) {
            if (arrowRoot && arrowRoot.gameObject.activeSelf != visible) arrowRoot.gameObject.SetActive(visible);
            if (ArrowVisible == visible) return;
            ArrowVisible = visible;
            if (logStateChanges) Debug.Log($"[M4][FaceArrow][{(visible ? "Shown" : "Hidden")}] {reason}; target={(CurrentTarget ? CurrentTarget.name : "None")}; angle={CurrentLookAngle:F1}", this);
        }
    }
}
