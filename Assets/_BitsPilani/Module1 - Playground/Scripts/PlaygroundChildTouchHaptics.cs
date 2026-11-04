using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace BitsPilani.Module1.Playground {
    public sealed class PlaygroundChildTouchHaptics : MonoBehaviour {
        public Transform childRoot;
        public PlaygroundRigAdapter rig;
        public BNG.InputBridge inputBridge;
        public BNG.ControllerHand hand;
        [Range(0,1)] public float amplitude = .25f;
        [Range(.01f,.5f)] public float duration = .08f;
        [Min(.05f)] public float cooldown = .2f;
        public bool emitHaptics = true;
        public bool disableInChildView = true;
        public UnityEvent onChildTouched = new UnityEvent();
        float nextTouch;
        public bool HandleTouch(Transform other,float now) {
            if (!other || !childRoot || !other.IsChildOf(childRoot) || now < nextTouch || disableInChildView && rig && rig.InChildView) return false;
            nextTouch = now + cooldown;
            if (emitHaptics) {
                if (inputBridge) inputBridge.VibrateController(100, amplitude, duration, hand);
                else {
                    var device = InputDevices.GetDeviceAtXRNode(hand == BNG.ControllerHand.Left ? XRNode.LeftHand : XRNode.RightHand);
                    if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) device.SendHapticImpulse(0, amplitude, duration);
                }
            }
            onChildTouched.Invoke(); return true;
        }
        void OnTriggerEnter(Collider other) { HandleTouch(other.transform, Time.unscaledTime); }
    }
}
