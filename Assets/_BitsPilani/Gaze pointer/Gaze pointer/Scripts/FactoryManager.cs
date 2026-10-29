using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public class FactoryManager : MonoBehaviour
{
    public static FactoryManager Instance;

    [SerializeField] GameObject gazePointerPrefab;

    //[SerializeField] GameObject touchBtnPrefab;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }
    private static GazeButton CreateGagePointer(Transform location)
    {
        GameObject btn = Instantiate(Instance.gazePointerPrefab, location);
        btn.transform.position = location.position;
        btn.transform.rotation = location.rotation;

        return btn.GetComponent<GazeButton>();
    }



    public static void CreateGaze(Transform gazeButtonTransform, Action afterGazeClicked)
    {
        GazeButton gazeButton = CreateGagePointer(gazeButtonTransform);
       
        gazeButton.gazeButtonClick.onClick.AddListener(() =>
        {
            afterGazeClicked?.Invoke(); RemoveGazePointer(gazeButton);
        });

        gazeButton.onClick.AddListener(async () =>
        {
            afterGazeClicked?.Invoke();
            await RemoveGazePointer(gazeButton);
        });
    }

    private static async Task RemoveGazePointer(GazeButton gazeButton)
    {
        if (gazeButton != null)
        {
            UnityEngine.Object.Destroy(gazeButton.gameObject);
        }

        await Task.CompletedTask; // This completes the async operation
    }


   /* private static VR3DButton CreateVr3DBtn(Transform location)
    {
        GameObject btn = Instantiate(Instance.touchBtnPrefab, location);
        btn.transform.position = location.position;
        btn.transform.rotation = location.rotation;

        return btn.GetComponent<VR3DButton>();
    }*/


  /*  public static VR3DButton CreateTouchBtn(Transform vR3DButtonParent, Action Click, bool isUIBtn = false, bool canTouch = true,bool notRemoveTouchButton = false)
    {
        VR3DButton vr3DButton = CreateVr3DBtn(vR3DButtonParent);
        vr3DButton.isUIBtn = isUIBtn;

        if (!notRemoveTouchButton) vr3DButton.thisCollider.enabled = canTouch;

        vr3DButton.OnBtnClick += async () =>
        {
            Click?.Invoke();
            if (!notRemoveTouchButton) vr3DButton.SetOnBtnClick_Null();
          if(!notRemoveTouchButton)  await RemoveTouchBtn(vr3DButton);
        };

        return vr3DButton;
    }*/

    //private static async Task RemoveTouchBtn(VR3DButton vr3DButton)
    //{
    //    await Task.Delay(1000);
    //    if (vr3DButton != null)
    //    {
    //        UnityEngine.Object.Destroy(vr3DButton.gameObject);
    //    }

    //    await Task.CompletedTask;
    //}
}
