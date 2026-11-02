using UnityEngine;
using UnityEngine.EventSystems;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundPanelHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        public bool IsHovered { get; private set; }
        public void OnPointerEnter(PointerEventData data) { IsHovered = true; }
        public void OnPointerExit(PointerEventData data) { IsHovered = false; }
        void OnDisable() { IsHovered = false; }
    }
}
