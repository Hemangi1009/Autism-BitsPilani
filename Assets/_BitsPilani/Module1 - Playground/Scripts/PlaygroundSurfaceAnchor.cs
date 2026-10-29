using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundSurfaceAnchor : MonoBehaviour {
        public Renderer[] contactParts;
        public float rayHeight = 25f;
        public float LastGroundError { get; private set; }

        public static bool TrySupport(Vector3 point, out float height) {
            height = float.NegativeInfinity;
            var hits = Physics.RaycastAll(new Vector3(point.x, point.y + 25f, point.z), Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits) {
                if (hit.collider.GetComponent<PlaygroundWalkableSurface>() && hit.point.y > height) height = hit.point.y;
            }
            return !float.IsNegativeInfinity(height);
        }

        [ContextMenu("Snap feet or base to support surface")]
        public void SnapToSupport() {
            if (contactParts == null || contactParts.Length == 0) return;
            Physics.SyncTransforms();
            float bottom = float.PositiveInfinity, support = float.NegativeInfinity;
            foreach (var part in contactParts) if (part) {
                bottom = Mathf.Min(bottom, part.bounds.min.y);
                if (TrySupport(part.bounds.center, out float h)) support = Mathf.Max(support, h);
            }
            if (float.IsInfinity(bottom) || float.IsInfinity(support)) {
                Debug.LogWarning("[M1 Playground][Grounding] No contact parts or support beneath " + name, this);
                return;
            }
            transform.position += Vector3.up * (support - bottom);
            Physics.SyncTransforms();
            float actualBottom = float.PositiveInfinity;
            foreach (var part in contactParts) if (part) actualBottom = Mathf.Min(actualBottom, part.bounds.min.y);
            LastGroundError = actualBottom - support;
        }

        void Start() { SnapToSupport(); }
    }
}
