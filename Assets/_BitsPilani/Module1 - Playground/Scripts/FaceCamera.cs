using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 5f;
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
        if (!mainCam)
            Debug.LogWarning("FaceCamera: No camera tagged as MainCamera found.");
    }

    private void LateUpdate()
    {
        if (!mainCam) return;

        // Direction from eye to camera
        Vector3 toCam = mainCam.transform.position - transform.position;

        // Create base look rotation (Z faces camera)
        Quaternion targetRotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);

        // Rotate so Y faces camera instead of Z
        targetRotation *= Quaternion.Euler(90f, 0f, 0f);

        // Smoothly rotate toward that orientation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }
}
