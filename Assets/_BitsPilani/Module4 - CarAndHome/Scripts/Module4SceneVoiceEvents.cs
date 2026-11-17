using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace BitsPilani.Module4 {
    [Serializable]
    public sealed class Module4SceneVoiceBinding {
        [Tooltip("The same event name used in the Module 4 VO definition.")]
        public Module4Event voiceEvent;
        [Tooltip("Called when this VO event starts, before its optional delay/audio. Accepts scene hierarchy objects.")]
        public UnityEvent onStarted = new UnityEvent();
        [Tooltip("Called when the VO finishes. Empty VO fields complete immediately. Skip runs matching Start/End lifecycle callbacks without playing audio; other narration cancellation does not fire End.")]
        public UnityEvent onCompleted = new UnityEvent();
    }
    [DisallowMultipleComponent]
    [AddComponentMenu("BitsPilani/Module 4/Scene VO Events")]
    public sealed class Module4SceneVoiceEvents : MonoBehaviour {
        [Header("Hierarchy callbacks for this scene")]
        [Tooltip("Add an entry, choose the VO event, then drag hierarchy objects into On Started / On Completed. Clips remain in the VO definition asset. Multiple entries for the same event are supported.")]
        public List<Module4SceneVoiceBinding> events = new List<Module4SceneVoiceBinding>();
        public void InvokeStarted(Module4Event id){Invoke(id,true);}
        public void InvokeCompleted(Module4Event id){Invoke(id,false);}
        void Invoke(Module4Event id,bool starting){
            // Never call targets belonging to a suspended or unloaded scene.
            if(!this||!isActiveAndEnabled)return;
            var snapshot=events.ToArray();
            foreach(var binding in snapshot){
                if(!this||!isActiveAndEnabled)break;
                if(binding==null||binding.voiceEvent!=id)continue;
                Debug.Log("[M4][SceneVOEvent] "+gameObject.scene.name+" | "+id+" | "+(starting?"Start":"End"),this);
                if(starting)binding.onStarted?.Invoke();else binding.onCompleted?.Invoke();
            }
        }
    }
}
