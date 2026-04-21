using UnityEngine;

namespace MiniGolf
{
    [RequireComponent(typeof(Camera))]
    public class CopyParentCameraProperties : MonoBehaviour
    {
        private Camera parentCam;
        private Camera cam;

        void Start()
        {
            parentCam = transform.parent.GetComponent<Camera>();
            cam = GetComponent<Camera>();
        }

        void Update()
        {
            if(!parentCam)
                return;

            cam.orthographic     = parentCam.orthographic;
            cam.orthographicSize = parentCam.orthographicSize;
            cam.fieldOfView      = parentCam.fieldOfView;
        }
    }
}
