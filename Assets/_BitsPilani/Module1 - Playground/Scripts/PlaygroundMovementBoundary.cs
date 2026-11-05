using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlaygroundMovementBoundary : MonoBehaviour {
        public PlaygroundRigAdapter rig;
        [Tooltip("Edit this box's Center/Size or its scene transform. Rotate around Y only. Its horizontal footprint is the allowed movement area; height does not limit headset tracking.")]
        public BoxCollider bounds;
        [Min(0), Tooltip("Keep the player's viewpoint this far inside the edges, in world metres.")]
        public float edgeInset = .15f;
        public Color sceneOutline = new Color(.15f, .85f, 1f, .8f);

        public Vector3 ConstrainPosition(Vector3 worldPosition) {
            if (!bounds) return worldPosition;
            var local = bounds.transform.InverseTransformPoint(worldPosition) - bounds.center;
            var half = bounds.size * .5f;
            var scale = bounds.transform.lossyScale;
            float insetX = edgeInset / Mathf.Max(.0001f, Mathf.Abs(scale.x));
            float insetZ = edgeInset / Mathf.Max(.0001f, Mathf.Abs(scale.z));
            float limitX = Mathf.Max(0, half.x - insetX), limitZ = Mathf.Max(0, half.z - insetZ);
            local.x = Mathf.Clamp(local.x, -limitX, limitX);
            local.z = Mathf.Clamp(local.z, -limitZ, limitZ);
            var result = bounds.transform.TransformPoint(local + bounds.center);
            result.y = worldPosition.y;
            return result;
        }

        public bool EnforceBoundary() {
            if (!rig || !rig.playerRoot || !rig.head || !bounds || !bounds.enabled || rig.InChildView) return false;
            var correction = ConstrainPosition(rig.head.position) - rig.head.position;
            if (correction.sqrMagnitude < .00000001f) return false;
            // Correct the locomotion origin, never the tracked camera pose. No teleport fade.
            bool wasEnabled = rig.body && rig.body.enabled;
            if (wasEnabled) rig.body.enabled = false;
            rig.playerRoot.position += correction;
            if (wasEnabled) rig.body.enabled = true;
            return true;
        }

        void LateUpdate() { EnforceBoundary(); }

        void Reset() {
            bounds = GetComponent<BoxCollider>();
            bounds.isTrigger = true;
        }

        void OnDrawGizmos() {
            var box = bounds ? bounds : GetComponent<BoxCollider>();
            if (!box) return;
            Gizmos.color = sceneOutline;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
