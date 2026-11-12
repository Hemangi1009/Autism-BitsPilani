using UnityEngine;

namespace BitsPilani.Module1.Playground {
    /// <summary>
    /// Read-only observer of the module: guides head direction without completing gazes,
    /// changing steps, moving the player, or modifying the animated prefab child.
    /// </summary>
    [DefaultExecutionOrder(10020)]
    public sealed class PlaygroundFaceArrowGuide : MonoBehaviour {
        [Header("Scene references")]
        [Tooltip("Existing flow controller. Its state is read only; this guide never advances it.")]
        public PlaygroundFlowController flow;
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

        [Header("World targets")]
        [Tooltip("Child/onlooker/party gaze anchors in their existing order. The newest active gaze is followed automatically.")]
        public Transform[] introGazeTargets;
        [Tooltip("A transform on the child, such as the torso. Follows the child when moved to the quiet area.")]
        public Transform childLookTarget;
        [Tooltip("Speaker/balloon focus used during child perspective.")]
        public Transform childViewLookTarget;
        [Tooltip("Optional explicit target. Leave None to follow the current module task automatically.")]
        public Transform targetOverride;

        [Header("Guide these world interactions")]
        public bool guideIntro = true;
        public bool guideFirstObservation = true;
        public bool guideChildApproachAndResponse = true;
        public bool guideChildPerspective = true;
        public bool guideOnlooker = true;
        public bool guideReplay = true;

        [Header("Startup ID guidance")]
        [Tooltip("Guide users to the visible participant-ID panel during Loading, then to visible startup instructions once ID entry is complete.")]
        public bool guideStartupPanels = true;
        [Tooltip("When the participant keyboard is open, point to it instead of the ID panel. No keyboard or ID behaviour is changed.")]
        public bool guideParticipantKeyboard = true;

        [Header("Panel guidance")]
        [Tooltip("Direct attention to visible clue/guess cards, response choices/review and reflection questions. Hidden panels are never targeted.")]
        public bool guideMainPanels = true;

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
            if (targetOverride) return targetOverride;
            if (!flow || !flow.isActiveAndEnabled) return null;
            switch (flow.CurrentStep) {
                case PlaygroundStep.Loading:
                    return guideStartupPanels ? StartupPanelTarget() : null;
                case PlaygroundStep.EnterPlayground:
                    if (!guideIntro) return null;
                    if (flow.IntroGazesComplete)
                        return flow.observationCircle && flow.observationCircle.activeInHierarchy ? flow.observationCircle.transform : null;
                    // FactoryManager creates the next gaze before destroying the previous one.
                    // Reverse order selects that newer gaze during their one-frame overlap.
                    if (introGazeTargets != null) for (int i = introGazeTargets.Length - 1; i >= 0; i--) {
                        var anchor = introGazeTargets[i];
                        if (anchor && anchor.gameObject.activeInHierarchy && anchor.GetComponentInChildren<global::GazeButton>()) return anchor;
                    }
                    return null;
                case PlaygroundStep.CollectClues:
                case PlaygroundStep.NoteGuesses:
                case PlaygroundStep.Reflect:
                    return MainPanelTarget();
                case PlaygroundStep.WatchChanges:
                    return guideFirstObservation ? ChildTarget() : null;
                case PlaygroundStep.TestGuesses:
                    if (flow.ReviewingGuesses || flow.ResponseReady) return MainPanelTarget();
                    if (!guideChildApproachAndResponse) return null;
                    return flow.approachCircle && flow.approachCircle.activeInHierarchy ? flow.approachCircle.transform : ChildTarget();
                case PlaygroundStep.ChildPerspective:
                    return guideChildPerspective && flow.rig && flow.rig.InChildView
                        ? (childViewLookTarget ? childViewLookTarget : flow.partyFocus) : null;
                case PlaygroundStep.OnlookerThought:
                    if (!guideOnlooker) return null;
                    return flow.ThoughtOpen && flow.ui && flow.ui.thoughtCanvas ? flow.ui.thoughtCanvas.transform : flow.gazeThought;
                case PlaygroundStep.ReplayAndPrevent:
                    if (!guideReplay || !flow.ReplayStarted) return null;
                    return !flow.ReplayAreaReached && flow.observationCircle ? flow.observationCircle.transform : ChildTarget();
                default:
                    // Summary and completed modules have no required direction target.
                    return null;
            }
        }

        Transform StartupPanelTarget() {
            // Read the existing scene references; never open/close the panel or submit an ID here.
            var recording = flow ? flow.recording : null;
            if (recording && recording.participantIdPanel && recording.participantIdPanel.activeInHierarchy) {
                if (guideParticipantKeyboard && recording.participantIdInput) {
                    var keyboard = recording.participantIdInput.GetComponent<PlaygroundParticipantKeyboard>();
                    if (keyboard && keyboard.IsOpen && keyboard.KeyboardCanvas && keyboard.KeyboardCanvas.gameObject.activeInHierarchy)
                        return keyboard.KeyboardCanvas.transform;
                }
                return recording.participantIdPanel.transform;
            }
            return MainPanelTarget();
        }

        Transform MainPanelTarget() {
            return guideMainPanels && flow && flow.ui && flow.ui.mainCanvas && flow.ui.mainCanvas.gameObject.activeInHierarchy
                ? flow.ui.mainCanvas.transform : null;
        }

        Transform ChildTarget() { return childLookTarget ? childLookTarget : flow.child ? flow.child.transform : null; }

        public void RefreshGuidance(float now) {
            if (!head || !arrowRoot) {
                if (!missingReferencesLogged && logStateChanges) {
                    Debug.LogWarning("[M1 Playground][FaceArrow][Configuration] Assign the tracked camera and Face_Arrow root. No scene or flow state was changed.", this);
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
                if (logStateChanges) Debug.Log("[M1 Playground][FaceArrow][TargetChanged] " + (target ? target.name : "None (UI or no active world task)"), this);
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
                Debug.Log($"[M1 Playground][FaceArrow][Angle] target={target.name}; angle={CurrentLookAngle:F1}; hideWithin={hide:F1}; showOutside={show:F1}; visible={ArrowVisible}", this);
            }
        }

        void SetVisible(bool visible, string reason) {
            if (arrowRoot && arrowRoot.gameObject.activeSelf != visible) arrowRoot.gameObject.SetActive(visible);
            if (ArrowVisible == visible) return;
            ArrowVisible = visible;
            if (logStateChanges) Debug.Log($"[M1 Playground][FaceArrow][{(visible ? "Shown" : "Hidden")}] {reason}; target={(CurrentTarget ? CurrentTarget.name : "None")}; angle={CurrentLookAngle:F1}", this);
        }
    }
}
