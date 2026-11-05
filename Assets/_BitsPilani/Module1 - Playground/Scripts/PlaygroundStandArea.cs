using UnityEngine;

namespace BitsPilani.Module1.Playground {
    // A scene-authored destination. Showing it never changes its transform or particle settings.
    public sealed class PlaygroundStandArea : MonoBehaviour {
        [Min(0)] public float secondsAfterArrival = 2f;
        public bool HasArrived { get; private set; }
        public bool ArrivalComplete { get; private set; }
        float arrivedAt;
        bool showing;

        public void Show() {
            HasArrived = false;
            ArrivalComplete = false;
            showing = true;
            gameObject.SetActive(true);
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true)) {
                if (!particles.gameObject.activeInHierarchy) continue;
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(false);
            }
        }

        // Called by the flow even when narration is still playing, including when already inside.
        public bool AdvanceArrival(float now, bool playerInside) {
            if (ArrivalComplete) return true;
            if (!showing) return false;
            if (!HasArrived && playerInside) {
                HasArrived = true;
                arrivedAt = now;
            }
            if (!HasArrived || now - arrivedAt < secondsAfterArrival) return false;
            ArrivalComplete = true;
            Hide();
            return true;
        }

        public void Hide() {
            showing = false;
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
