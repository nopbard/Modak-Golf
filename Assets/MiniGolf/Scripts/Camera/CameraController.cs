using UnityEngine;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

namespace MiniGolf
{
    // 런타임에 Cinemachine 셋업을 자동 구성하는 카메라 컨트롤러.
    // 기존 씬 수정 없이 이 컴포넌트가 붙은 GameObject(카메라 Rig)에서
    // CinemachineBrain + CinemachineCamera + Follow 파이프라인을 생성한다.
    //
    // 동작:
    //  - 공이 움직일 때 카메라가 공을 부드럽게 추적 (Cinemachine 댐핑)
    //  - 휠로 확대/축소 (ortho size 변경)
    //  - 두 손가락 핀치로 확대/축소
    //  - 마우스 드래그로 일시 팬 → 버튼 떼면 부드럽게 공 위치로 복귀
    public class CameraController : MonoBehaviour
    {
        [Header("줌 (Orthographic Size)")]
        [SerializeField] private float minZoom = 3f;
        [SerializeField] private float maxZoom = 5f;
        [Tooltip("마우스 휠 1틱 당 ortho size 변화량")]
        [SerializeField] private float wheelZoomSpeed = 1.5f;
        [Tooltip("터치 핀치 1픽셀 당 ortho size 변화량")]
        [SerializeField] private float pinchZoomSpeed = 0.02f;
        [Tooltip("목표 줌까지 수렴 속도 (클수록 빠름). 부드러운 줌 인/아웃 연출")]
        [SerializeField] private float zoomSmoothSpeed = 8f;

        [Header("팬 (드래그 임시 오프셋)")]
        [Tooltip("드래그 민감도 배수")]
        [SerializeField] private float panSensitivity = 1f;
        [Tooltip("버튼 뗀 뒤 공으로 돌아가는 속도 (클수록 빠름)")]
        [SerializeField] private float returnSpeed = 4f;

        [Header("Cinemachine 추적")]
        [Tooltip("카메라가 공을 따라가는 부드러움. 0=즉시, 1~2=부드러움")]
        [SerializeField] private float followDamping = 1f;

        private Camera cam;
        private CinemachineCamera vcam;
        private CinemachineFollow follow;
        private Transform followTarget;

        private Vector3 panOffset;     // 누적된 팬 오프셋 (공 기준 월드 델타)
        private bool isPanning;
        private Vector3 lastMouseScreen;

        private float targetOrthoSize; // 입력에서 바로 갱신되는 목표 줌. 실제 lens는 이 값으로 lerp

        void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            if(cam == null)
            {
                Debug.LogError("CameraController: 자식 Camera를 찾지 못함");
                return;
            }

            // 기존 scene에서 세팅된 camera의 world 위치/회전 캡처 (Cinemachine 인계 전)
            Vector3 initialCamWorldPos = cam.transform.position;
            Quaternion initialCamWorldRot = cam.transform.rotation;
            // rig 기준 카메라 로컬 오프셋 (공 → 카메라 월드 벡터)
            Vector3 rigToCameraOffset = initialCamWorldPos - transform.position;

            // 1. Main Camera에 CinemachineBrain
            if(cam.GetComponent<CinemachineBrain>() == null)
                cam.gameObject.AddComponent<CinemachineBrain>();

            // 2. Follow Target (공을 따라다닐 빈 GO)
            var targetGO = new GameObject("CameraFollowTarget");
            targetGO.transform.SetParent(transform, false);
            followTarget = targetGO.transform;

            // 3. CinemachineCamera (vcam)
            var vcamGO = new GameObject("BallVCam");
            vcamGO.transform.SetParent(transform, false);
            vcamGO.transform.position = initialCamWorldPos;
            vcamGO.transform.rotation = initialCamWorldRot;

            vcam = vcamGO.AddComponent<CinemachineCamera>();
            vcam.Follow = followTarget;
            vcam.LookAt = null; // 회전은 고정 (iso 각도 유지)

            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = cam.orthographicSize > 0 ? cam.orthographicSize : 6f;
            lens.NearClipPlane = cam.nearClipPlane;
            lens.FarClipPlane = cam.farClipPlane;
            vcam.Lens = lens;

            // 초기 lens size가 클램프 범위를 벗어나면 안전하게 잘라줌 (최대치 내려갈 때 자동 수렴)
            targetOrthoSize = Mathf.Clamp(lens.OrthographicSize, minZoom, maxZoom);

            // 4. CinemachineFollow (Body) — 월드 공간 오프셋 + 댐핑
            follow = vcamGO.AddComponent<CinemachineFollow>();
            follow.FollowOffset = rigToCameraOffset;
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = Vector3.one * followDamping;
            follow.TrackerSettings = tracker;

            GameManager.Instance.OnLoadHole += (a, b) => OnLoadHole();
        }

        void OnLoadHole()
        {
            panOffset = Vector3.zero;
            // 새 홀 시작 시 카메라가 튀지 않도록 즉시 공 위치로 타깃 스냅
            if(Ball.Instance != null && followTarget != null)
            {
                Vector3 p = Ball.Instance.GetPosition();
                p.y = 0f;
                followTarget.position = p;
                // Cinemachine에 워프 알림 → 댐핑 없이 바로 이동
                if(vcam != null) vcam.OnTargetObjectWarped(followTarget, Vector3.zero);
            }
        }

        void Update()
        {
            UpdateFollowTarget();
            ApplyZoomSmoothing();

            // 공 조준 중이면 카메라 조작 입력 차단
            bool interactingWithBall = InputController.Instance != null && InputController.Instance.IsInteractingWithBall;
            if(interactingWithBall)
            {
                isPanning = false;
                return;
            }

            HandleZoom();
            HandlePan();
        }

        // 입력이 targetOrthoSize를 바꾸고, 실제 렌즈는 이 메서드에서 서서히 수렴
        void ApplyZoomSmoothing()
        {
            if(vcam == null) return;
            var lens = vcam.Lens;
            if(Mathf.Abs(lens.OrthographicSize - targetOrthoSize) < 0.0005f)
            {
                if(lens.OrthographicSize != targetOrthoSize)
                {
                    lens.OrthographicSize = targetOrthoSize;
                    vcam.Lens = lens;
                }
                return;
            }
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetOrthoSize, zoomSmoothSpeed * Time.deltaTime);
            vcam.Lens = lens;
        }

        void UpdateFollowTarget()
        {
            if(Ball.Instance == null || followTarget == null) return;

            // 드래그 떼면 팬 오프셋이 부드럽게 0으로 수렴 → 공으로 복귀
            if(!isPanning)
                panOffset = Vector3.Lerp(panOffset, Vector3.zero, returnSpeed * Time.deltaTime);

            Vector3 target = Ball.Instance.GetPosition();
            target.y = 0f; // 공 y 흔들림 무시 (iso 탑다운 가정)
            followTarget.position = target + panOffset;
        }

        void HandleZoom()
        {
            // 마우스 휠
            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            if(Mathf.Abs(wheel) > 0.0001f)
                ApplyZoom(-wheel * wheelZoomSpeed);

            // 터치 핀치 (두 손가락)
            if(Input.touchCount >= 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);
                Vector2 prev0 = t0.position - t0.deltaPosition;
                Vector2 prev1 = t1.position - t1.deltaPosition;
                float prevDist = Vector2.Distance(prev0, prev1);
                float curDist = Vector2.Distance(t0.position, t1.position);
                float delta = curDist - prevDist;
                if(Mathf.Abs(delta) > 0.1f)
                    ApplyZoom(-delta * pinchZoomSpeed);
            }
        }

        void ApplyZoom(float delta)
        {
            // 목표값만 즉시 갱신. 실제 렌즈는 ApplyZoomSmoothing()이 lerp
            targetOrthoSize = Mathf.Clamp(targetOrthoSize + delta, minZoom, maxZoom);
        }

        void HandlePan()
        {
            // 핀치 중이면 팬 금지
            if(Input.touchCount >= 2)
            {
                isPanning = false;
                return;
            }

            if(Input.GetMouseButtonDown(0))
            {
                // InputController가 공 터치로 간주하지 않은 경우에만 팬 시작
                // (IsInteractingWithBall은 Update 맨 앞에서 차단됨)
                lastMouseScreen = Input.mousePosition;
                isPanning = true;
            }
            else if(Input.GetMouseButton(0) && isPanning)
            {
                Vector3 cur = Input.mousePosition;
                Vector2 deltaPx = (Vector2)(cur - lastMouseScreen);
                lastMouseScreen = cur;

                // 픽셀 → 월드 변환. ortho: 화면 높이 = orthoSize*2 월드 유닛.
                float worldPerPixelY = (vcam.Lens.OrthographicSize * 2f) / Screen.height;
                float worldPerPixelX = worldPerPixelY; // ortho pixel은 x/y 동일 스케일

                // 화면 상 드래그 방향의 반대로 카메라가 움직이는 느낌 (공을 드래그해서 '당겨오는' 느낌)
                // XZ 평면 기준. cam forward를 XZ로 평탄화.
                Vector3 camRightXZ = new Vector3(cam.transform.right.x, 0f, cam.transform.right.z).normalized;
                Vector3 camForwardXZ = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;

                Vector3 worldDelta =
                    -camRightXZ * (deltaPx.x * worldPerPixelX)
                    - camForwardXZ * (deltaPx.y * worldPerPixelY);

                panOffset += worldDelta * panSensitivity;
            }
            else if(Input.GetMouseButtonUp(0))
            {
                isPanning = false;
            }
        }

        // HUDManager 등 외부 호환 API 유지. 카메라 위치를 즉시 공으로 스냅.
        public void SetPosition(Vector3 pos)
        {
            panOffset = Vector3.zero;
            if(followTarget != null)
            {
                pos.y = 0f;
                followTarget.position = pos;
                if(vcam != null) vcam.OnTargetObjectWarped(followTarget, Vector3.zero);
            }
        }

        // 외부에서 줌 호출용 (호환 유지, 현재 미사용)
        public void Zoom(float delta)
        {
            ApplyZoom(-delta * wheelZoomSpeed);
        }
    }
}
