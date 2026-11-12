using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BitsPilani.Module1.Playground {
    /// <summary>
    /// A scene-authored, single-button fallback for child view. Independent of the wrist UI.
    /// Existing flow callbacks and readiness gates remain in charge of returning to parent view.
    /// </summary>
    [DefaultExecutionOrder(10010)]
    public sealed class PlaygroundChildViewReturnControl : MonoBehaviour {
        [Header("Existing module references")]
        public PlaygroundFlowController flow;
        public Transform head;
        public Canvas mainPanel;

        [Header("Scene-authored return UI (not generated at runtime)")]
        public Canvas returnCanvas;
        public Button returnButton;

        [Header("When the fallback button is needed")]
        [Tooltip("Show only when returning is allowed and the main panel is far away or hidden. Disable this distance rule to show whenever returning is allowed in child view.")]
        public bool onlyWhenPanelFarOrHidden = true;
        [Min(.5f), Tooltip("Main-panel distance from the child-view camera above which the nearby return button appears.")]
        public float farPanelDistance = 3f;

        [Header("Placement near the camera (metres)")]
        [Min(.4f)] public float cameraDistance = 1.1f;
        public Vector2 cameraOffset = new Vector2(0, -.25f);

        [Header("Diagnostics")]
        public bool logStateChanges = true;

        public bool ReturnRequested { get; private set; }
        public bool ReturnReady { get; private set; }
        public bool Visible => returnCanvas && returnCanvas.gameObject.activeSelf;
        UnityAction returnAction;
        Button boundButton;
        bool lastVisible, lastReady;

        public void SetReturnState(bool visible, bool ready, UnityAction callback) {
            ReturnRequested = visible;
            ReturnReady = ready && callback != null;
            returnAction = callback;
            BindButton();
            RefreshControl();
        }

        void BindButton() {
            if (boundButton == returnButton) return;
            if (boundButton) boundButton.onClick.RemoveListener(ReturnClicked);
            boundButton = returnButton;
            if (boundButton) boundButton.onClick.AddListener(ReturnClicked);
        }

        void ReturnClicked() {
            if (!Visible || !ReturnReady || !returnButton || !returnButton.interactable) return;
            // Use the original ReturnToMyView callback; never bypass its flow guard.
            var callback = returnAction;
            callback?.Invoke();
        }

        public void RefreshControl() {
            if (!returnCanvas || !returnButton) return;
            bool inChildView = ReturnRequested && head && flow && flow.isActiveAndEnabled &&
                flow.CurrentStep == PlaygroundStep.ChildPerspective && flow.rig && flow.rig.InChildView;
            bool mainVisible = mainPanel && mainPanel.gameObject.activeInHierarchy;
            bool far = mainPanel && head && Vector3.Distance(head.position, mainPanel.transform.position) > Mathf.Max(.5f, farPanelDistance);
            bool show = inChildView && ReturnReady && (!onlyWhenPanelFarOrHidden || !mainVisible || far);
            if (show) UpdatePose();
            if (returnCanvas.gameObject.activeSelf != show) returnCanvas.gameObject.SetActive(show);
            returnButton.interactable = show && ReturnReady;
            if (logStateChanges && (show != lastVisible || returnButton.interactable != lastReady))
                Debug.Log($"[M1 Playground][ChildViewReturn] visible={show}; ready={returnButton.interactable}; mainPanelVisible={mainVisible}; mainPanelFar={far}", this);
            lastVisible = show; lastReady = returnButton.interactable;
        }

        void UpdatePose() {
            // Only this new canvas follows the eye pose. Its authored scale, button text and style stay editable.
            returnCanvas.transform.SetPositionAndRotation(
                head.TransformPoint(new Vector3(cameraOffset.x, cameraOffset.y, Mathf.Max(.4f, cameraDistance))), head.rotation);
        }

        void OnEnable() { Application.onBeforeRender += BeforeRender; }
        void OnDisable() {
            Application.onBeforeRender -= BeforeRender;
            if (Application.isPlaying && returnCanvas) returnCanvas.gameObject.SetActive(false);
        }
        void LateUpdate() { RefreshControl(); }
        [BeforeRenderOrder(10010)]
        void BeforeRender() { if (Application.isPlaying && Visible) UpdatePose(); }
    }
}
