using UnityEngine;

namespace BitsPilani.Module1.Playground {
    public enum PlaygroundChildState { Calm, EarlyWarning, Distressed, Calming, Escalated }

    public sealed class PlaygroundCharacterPose : MonoBehaviour {
        public Transform[] upperArms, forearms, hands;
        public Transform torso;
        public Transform soundSource;
        public GameObject earDefenders;
        public PlaygroundSurfaceAnchor grounding;
        public PlaygroundChildState State { get; private set; }
        Transform[] parts;
        Vector3[] positions, scales;
        Quaternion[] rotations;
        Vector3 home;
        Quaternion homeRotation;
        bool captured;

        public void CaptureRestPose() {
            if (captured) return;
            home = transform.position; homeRotation = transform.rotation;
            parts = new Transform[upperArms.Length + forearms.Length + hands.Length + 1];
            int n = 0;
            foreach (var p in upperArms) parts[n++] = p;
            foreach (var p in forearms) parts[n++] = p;
            foreach (var p in hands) parts[n++] = p;
            parts[n] = torso;
            positions = new Vector3[parts.Length]; scales = new Vector3[parts.Length]; rotations = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++) { positions[i] = parts[i].localPosition; scales[i] = parts[i].localScale; rotations[i] = parts[i].localRotation; }
            captured = true;
        }

        public void ResetChild() {
            CaptureRestPose();
            transform.SetPositionAndRotation(home, homeRotation);
            if (earDefenders) earDefenders.SetActive(false);
            SetState(PlaygroundChildState.Calm);
        }

        // Changes are instantaneous still poses. There are no Animators, tweens, or rocking loops.
        public void SetState(PlaygroundChildState state) {
            CaptureRestPose(); State = state;
            for (int i = 0; i < parts.Length; i++) { parts[i].localPosition = positions[i]; parts[i].localScale = scales[i]; parts[i].localRotation = rotations[i]; }
            for (int i = 0; i < hands.Length; i++) {
                bool lifted = state == PlaygroundChildState.Distressed || state == PlaygroundChildState.Escalated || (state == PlaygroundChildState.EarlyWarning && i == 0) || (state == PlaygroundChildState.Calming && i == 1);
                if (!lifted) continue;
                float side = i == 0 ? -1f : 1f;
                Vector3 shoulder = new Vector3(side * .29f, 1.36f, 0);
                Vector3 elbow = new Vector3(side * .43f, 1.52f, -.05f);
                Vector3 hand = new Vector3(side * .205f, 1.76f, -.015f);
                Segment(upperArms[i], shoulder, elbow); Segment(forearms[i], elbow, hand); hands[i].localPosition = hand;
            }
            if (state == PlaygroundChildState.Escalated) torso.localRotation = Quaternion.Euler(0, 0, 9);
            if (state == PlaygroundChildState.Calm) transform.rotation = homeRotation;
            else if (soundSource) {
                Vector3 direction = soundSource.position - transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            }
            if (grounding) grounding.SnapToSupport();
        }

        static void Segment(Transform part, Vector3 a, Vector3 b) {
            part.localPosition = (a + b) * .5f;
            part.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            var scale = part.localScale; scale.y = Vector3.Distance(a, b) * .5f; part.localScale = scale;
        }

        public void OfferDefenders() { if (earDefenders) earDefenders.SetActive(true); }
        public void MoveToQuietSpot(Transform spot) {
            transform.SetPositionAndRotation(spot.position, spot.rotation);
            if (grounding) grounding.SnapToSupport();
        }
    }
}
