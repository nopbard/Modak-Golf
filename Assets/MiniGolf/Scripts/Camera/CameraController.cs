using System.Collections;
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
    //  - 휠로 확대/축소 (Orthographic Size 변경)
    //  - 두 손가락 핀치로 확대/축소
    //  - 마우스 드래그로 일시 팬 → 버튼 떼면 부드럽게 공 위치로 복귀
    public class CameraController : MonoBehaviour
    {
        [Header("줌 (Ortho Size)")]
        [Tooltip("기본 Orthographic Size")]
        [SerializeField] private float baseOrthoSize = 6.5f;
        [Tooltip("가장 확대된 상태의 Ortho Size (작을수록 zoom-in)")]
        [SerializeField] private float minOrthoSize = 3f;
        [Tooltip("가장 축소된 상태의 Ortho Size (클수록 zoom-out)")]
        [SerializeField] private float maxOrthoSize = 10f;
        [Tooltip("마우스 휠 1틱 당 Ortho Size 변화량")]
        [SerializeField] private float wheelZoomSpeed = 0.5f;
        [Tooltip("터치 핀치 1픽셀 당 Ortho Size 변화량")]
        [SerializeField] private float pinchZoomSpeed = 0.01f;
        [Tooltip("목표 줌까지 수렴 속도 (클수록 빠름)")]
        [SerializeField] private float zoomSmoothSpeed = 8f;

        [Header("해상도 보정")]
        [Tooltip("기준 종횡비. 16:9 = 1.777")]
        [SerializeField] private float idealAspect = 16f / 9f;
        [Tooltip("비이상 비율일 때 Ortho Size를 늘리는 강도")]
        [SerializeField] private float nonIdealRatioOffset = 9f;
        [Tooltip("해상도 보정 계산에 쓰이는 기본 카메라-타깃 거리")]
        [SerializeField] private float baseDistance = 24f;

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

        private Vector3 panOffset;
        private bool isPanning;
        private Vector3 lastMouseScreen;

        // 입력에서 바로 갱신되는 목표 Ortho Size. 실제 lens는 이 값으로 lerp
        private float targetOrthoSize;

        // 이 Time.time 시점 전까지는 followTarget 위치 갱신을 멈춤(= 카메라가 공을 따라가지 않음).
        // 0 이면 항상 추적. FreezeFollow() 로 설정.
        private float followResumeTime;

        void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            if(cam == null)
            {
                Debug.LogError("CameraController: 자식 Camera를 찾지 못함");
                return;
            }

            Vector3 initialCamWorldPos = cam.transform.position;
            Quaternion initialCamWorldRot = cam.transform.rotation;
            Vector3 rigToCameraOffset = initialCamWorldPos - transform.position;

            // 1. Main Camera에 CinemachineBrain
            if(cam.GetComponent<CinemachineBrain>() == null)
                cam.gameObject.AddComponent<CinemachineBrain>();

            // 2. Follow Target
            var targetGO = new GameObject("CameraFollowTarget");
            targetGO.transform.SetParent(transform, false);
            followTarget = targetGO.transform;

            // 3. CinemachineCamera (vcam) — Orthographic 모드
            var vcamGO = new GameObject("BallVCam");
            vcamGO.transform.SetParent(transform, false);
            vcamGO.transform.position = initialCamWorldPos;
            vcamGO.transform.rotation = initialCamWorldRot;

            vcam = vcamGO.AddComponent<CinemachineCamera>();
            vcam.Follow = followTarget;
            vcam.LookAt = null;

            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = baseOrthoSize;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            vcam.Lens = lens;

            targetOrthoSize = Mathf.Clamp(baseOrthoSize, minOrthoSize, maxOrthoSize);

            // 4. CinemachineFollow (Body)
            follow = vcamGO.AddComponent<CinemachineFollow>();
            follow.FollowOffset = rigToCameraOffset;
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = Vector3.one * followDamping;
            follow.TrackerSettings = tracker;

            if(GameManager.Instance != null)
                GameManager.Instance.OnLoadHole += (a, b) => OnLoadHole();
        }

        void Start()
        {
            if(Ball.Instance != null && followTarget != null)
            {
                followTarget.position = Ball.Instance.GetPosition();
                if(vcam != null) vcam.OnTargetObjectWarped(followTarget, Vector3.zero);
            }
        }

        void OnLoadHole()
        {
            panOffset = Vector3.zero;
            if(Ball.Instance != null && followTarget != null)
            {
                Vector3 p = Ball.Instance.GetPosition();
                followTarget.position = p;
                if(vcam != null) vcam.OnTargetObjectWarped(followTarget, Vector3.zero);
            }
        }

        void Update()
        {
            UpdateFollowTarget();
            ApplyZoomSmoothing();

            bool interactingWithBall = InputController.Instance != null && InputController.Instance.IsInteractingWithBall;
            if(interactingWithBall)
            {
                isPanning = false;
                return;
            }

            HandleZoom();
            HandlePan();
        }

        void ApplyZoomSmoothing()
        {
            if(vcam == null) return;
            float effective = targetOrthoSize + ComputeResolutionOrthoOffset();
            var lens = vcam.Lens;
            if(Mathf.Abs(lens.OrthographicSize - effective) < 0.005f)
            {
                if(lens.OrthographicSize != effective)
                {
                    lens.OrthographicSize = effective;
                    vcam.Lens = lens;
                }
                return;
            }
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, effective, zoomSmoothSpeed * Time.deltaTime);
            vcam.Lens = lens;
        }

        // 화면이 idealAspect보다 좁을 때 더 넓은 영역을 보이도록 OrthoSize를 키움
        float ComputeResolutionOrthoOffset()
        {
            float currentAspect = (float)Screen.width / Screen.height;
            float ratioOverflow = Mathf.Max(0f, idealAspect / currentAspect - 1f);
            if(ratioOverflow < 0.0001f) return 0f;
            return targetOrthoSize * ratioOverflow * (nonIdealRatioOffset / baseDistance);
        }

        void UpdateFollowTarget()
        {
            if(Ball.Instance == null || followTarget == null) return;

            // Freeze 구간이면 followTarget 위치를 갱신하지 않음 → 카메라는 제자리 유지.
            // 끝나는 시점에 followTarget 이 공 위치로 스냅되고, Cinemachine damping 으로 부드럽게 이어서 따라감.
            if(Time.time < followResumeTime)
                return;

            if(!isPanning)
                panOffset = Vector3.Lerp(panOffset, Vector3.zero, returnSpeed * Time.deltaTime);

            Vector3 ballPos = Ball.Instance.GetPosition();
            followTarget.position = ballPos + panOffset;
        }

        void HandleZoom()
        {
            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            if(Mathf.Abs(wheel) > 0.0001f)
                ApplyZoom(-wheel * wheelZoomSpeed);

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
            targetOrthoSize = Mathf.Clamp(targetOrthoSize + delta, minOrthoSize, maxOrthoSize);
        }

        void HandlePan()
        {
            if(Input.touchCount >= 2)
            {
                isPanning = false;
                return;
            }

            if(Input.GetMouseButtonDown(0))
            {
                lastMouseScreen = Input.mousePosition;
                isPanning = true;
            }
            else if(Input.GetMouseButton(0) && isPanning)
            {
                Vector3 cur = Input.mousePosition;
                Vector2 deltaPx = (Vector2)(cur - lastMouseScreen);
                lastMouseScreen = cur;

                // Orthographic: 화면 높이 = 2 * orthoSize 월드 유닛 (거리 무관)
                float orthoSize = vcam != null ? vcam.Lens.OrthographicSize : baseOrthoSize;
                float worldPerPixelY = (2f * orthoSize) / Screen.height;
                float worldPerPixelX = worldPerPixelY;

                Vector3 camRightXZ   = new Vector3(cam.transform.right.x,   0f, cam.transform.right.z).normalized;
                Vector3 camForwardXZ = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;

                Vector3 worldDelta =
                    -camRightXZ   * (deltaPx.x * worldPerPixelX)
                    - camForwardXZ * (deltaPx.y * worldPerPixelY);

                panOffset += worldDelta * panSensitivity;
            }
            else if(Input.GetMouseButtonUp(0))
            {
                isPanning = false;
            }
        }

        public void SetPosition(Vector3 pos)
        {
            panOffset = Vector3.zero;
            if(followTarget != null)
            {
                followTarget.position = pos;
                if(vcam != null) vcam.OnTargetObjectWarped(followTarget, Vector3.zero);
            }
        }

        public void Zoom(float delta)
        {
            ApplyZoom(-delta * wheelZoomSpeed);
        }

        // 일정 시간 동안 damping 값을 다른 값으로 덮어쓰기 (예: 스폰 직후 부드러운 추적).
        // 끝나면 인스펙터 기본값(followDamping) 으로 복원.
        public void SetSmoothDamping(float damping, float duration)
        {
            StopCoroutine(nameof(DampingCoroutine));
            StartCoroutine(DampingCoroutine(damping, duration));
        }

        // 지정된 시간 동안 followTarget 위치 갱신을 중단(= 카메라가 공을 따라가지 않음).
        // 이미 더 먼 시점까지 freeze 예약돼 있으면 그 쪽이 우선 (짧은 호출이 기존 예약을 단축시키지 않음).
        public void FreezeFollow(float seconds)
        {
            if(seconds <= 0f) return;
            followResumeTime = Mathf.Max(followResumeTime, Time.time + seconds);
        }

        IEnumerator DampingCoroutine(float target, float duration)
        {
            ApplyDamping(target);
            yield return new WaitForSeconds(duration);
            ApplyDamping(followDamping);
        }

        void ApplyDamping(float value)
        {
            if(follow == null) return;
            var tracker = follow.TrackerSettings;
            tracker.PositionDamping = Vector3.one * value;
            follow.TrackerSettings = tracker;
        }
    }
}
