using UnityEngine;
namespace BitsPilani.Module4 {
    public enum Module4ChildPose { Calm, Shoes, Ready, ExpectingPark, Upset, Settling, Calmer, MoreUpset }
    public sealed class Module4Actor : MonoBehaviour {
        [Header("Scene-authored static pose parts")]
        public Transform torso, head, leftArm, rightArm;
        public GameObject shoes, usualBag, pictureBoard;
        public Transform preparationPosition, doorwayPosition, walkPosition;
        public Module4ChildPose CurrentPose {get;private set;}
        Vector3 torsoScale;Quaternion torsoRotation,headRotation,leftRotation,rightRotation;bool captured;
        void Capture(){if(captured)return;captured=true;if(torso){torsoScale=torso.localScale;torsoRotation=torso.localRotation;}if(head)headRotation=head.localRotation;if(leftArm)leftRotation=leftArm.localRotation;if(rightArm)rightRotation=rightArm.localRotation;}
        public void Pose(Module4ChildPose pose){
            Capture();CurrentPose=pose;
            if(torso){torso.localScale=torsoScale;torso.localRotation=torsoRotation;}
            if(head)head.localRotation=headRotation;if(leftArm)leftArm.localRotation=leftRotation;if(rightArm)rightArm.localRotation=rightRotation;
            if(shoes)shoes.SetActive(pose!=Module4ChildPose.Calm);
            if(usualBag)usualBag.SetActive(pose!=Module4ChildPose.Calm&&pose!=Module4ChildPose.Shoes);
            if(pictureBoard)pictureBoard.SetActive(false);
            // Instant still poses only: no Animator, tween, bouncing or walking animation.
            if(pose==Module4ChildPose.ExpectingPark&&head)head.localRotation=headRotation*Quaternion.Euler(0,-35,0);
            if(pose==Module4ChildPose.Upset||pose==Module4ChildPose.MoreUpset){
                if(leftArm)leftArm.localRotation=leftRotation*Quaternion.Euler(-55,0,-35);
                if(rightArm)rightArm.localRotation=rightRotation*Quaternion.Euler(-55,0,35);
                if(head)head.localRotation=headRotation*Quaternion.Euler(15,-25,0);
                if(torso&&pose==Module4ChildPose.MoreUpset)torso.localRotation=torsoRotation*Quaternion.Euler(0,0,8);
            }
        }
        public void At(Transform location){if(location)transform.SetPositionAndRotation(location.position,location.rotation);}
        public void ShowBoard(bool visible){if(pictureBoard)pictureBoard.SetActive(visible);}
        public void SetBodyVisible(bool visible){foreach(var r in GetComponentsInChildren<Renderer>(true))r.enabled=visible;}
    }
}

