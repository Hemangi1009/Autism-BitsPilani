using UnityEngine;

namespace BitsPilani.Module1.Playground {
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlaygroundMovementBoundary : MonoBehaviour {
        [Header("Player rig - assign only the matching module")]
        [Tooltip("Module 1 rig. Keep the existing assignment for Module 1; leave empty when using Module 4 Rig below.")]
        public PlaygroundRigAdapter rig;
        [Tooltip("Module 4 rig. In Module4_Walk, drag the object carrying Module4Rig here and leave the Module 1 Rig field empty. No second rig adapter is needed.")]
        public BitsPilani.Module4.Module4Rig module4Rig;
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
            // Preserve the original Module 1 binding; use Module 4 only when it is empty.
            var playerRoot = rig ? rig.playerRoot : module4Rig ? module4Rig.playerRoot : null;
            var head = rig ? rig.head : module4Rig ? module4Rig.head : null;
            var body = rig ? rig.body : module4Rig ? module4Rig.body : null;
            bool inChildView = rig ? rig.InChildView : module4Rig && module4Rig.InChildView;
            if (!playerRoot || !head || !bounds || !bounds.enabled || inChildView) return false;
            var correction = ConstrainPosition(head.position) - head.position;
            if (correction.sqrMagnitude < .00000001f) return false;
            // Correct the locomotion origin, never the tracked camera pose. No teleport fade.
            bool wasEnabled = body && body.enabled;
            if (wasEnabled) body.enabled = false;
            playerRoot.position += correction;
            if (wasEnabled) body.enabled = true;
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
