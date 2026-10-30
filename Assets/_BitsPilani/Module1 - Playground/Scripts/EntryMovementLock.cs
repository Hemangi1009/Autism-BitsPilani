using UnityEngine;
namespace BitsPilani.Module1.Playground {
    public sealed class EntryMovementLock : MonoBehaviour {
        public PlaygroundRigAdapter rig;
        void Start() { if(rig)rig.SetMovementLocked(true); }
    }
}
