using System.Collections;using UnityEngine;
namespace BitsPilani.Module4 {
    public sealed class Module4Fader : MonoBehaviour {
        public Canvas canvas;public CanvasGroup group;
        [Min(.1f)] public float duration=.65f;
        public IEnumerator Fade(bool toBlack){
            if(!canvas||!group)yield break;
            canvas.gameObject.SetActive(true);group.blocksRaycasts=true;
            float start=group.alpha,target=toBlack?1:0,elapsed=0;
            while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;group.alpha=Mathf.Lerp(start,target,Mathf.Clamp01(elapsed/duration));yield return null;}
            group.alpha=target;group.blocksRaycasts=toBlack;if(!toBlack)canvas.gameObject.SetActive(false);
        }
        public void Black(){if(canvas)canvas.gameObject.SetActive(true);if(group){group.alpha=1;group.blocksRaycasts=true;}}
    }
}

