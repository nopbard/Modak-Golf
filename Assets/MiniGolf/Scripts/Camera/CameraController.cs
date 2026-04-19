using UnityEngine;

namespace MiniGolf
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField]
        private float minZoom;

        [SerializeField]
        private float maxZoom;

        private Vector3 lastFrameTouchPos;

        private Camera cam;

        void Awake()
        {
            cam = GetComponentInChildren<Camera>();

            GameManager.Instance.OnLoadHole += (a, b) => OnLoadHole();
        }

        void Start()
        {
            lastFrameTouchPos = cam.ScreenToViewportPoint(Input.mousePosition);
        }

        void OnLoadHole()
        {
            SetPosition(Ball.Instance.GetPosition());
        }

        void Update()
        {
            // Don't move the camera if we're interacting with the ball
            if(InputController.Instance.IsInteractingWithBall)
                return;

            if(Input.GetAxisRaw("Mouse ScrollWheel") != 0)
            {
                Zoom(Input.GetAxisRaw("Mouse ScrollWheel"));
            }

            if(!Input.GetMouseButton(0))
                return;

            if(Input.GetMouseButtonDown(0))
                lastFrameTouchPos = cam.ScreenToViewportPoint(Input.mousePosition);

            Vector3 touchPos = cam.ScreenToViewportPoint(Input.mousePosition);
            Vector3 touchDelta = touchPos - lastFrameTouchPos;

            touchDelta.x *= cam.orthographicSize * cam.aspect * 2;
            touchDelta.y *= cam.orthographicSize * 2;

            transform.Translate(-transform.right * touchDelta.x, Space.World);
            transform.Translate(-transform.forward * touchDelta.y, Space.World);

            lastFrameTouchPos = touchPos;
        }

        public void SetPosition(Vector3 pos)
        {
            pos.y = 0;
            transform.position = pos;
        }

        public void Zoom(float delta)
        {
            cam.orthographicSize -= delta * 1.5f;

            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }
}
