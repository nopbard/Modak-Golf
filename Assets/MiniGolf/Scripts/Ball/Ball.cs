using System.Collections;
using UnityEngine;
using Unity.Collections;

namespace MiniGolf
{
    [RequireComponent(typeof(Rigidbody))]
    public class Ball : MonoBehaviour
    {
        public enum State
        {
            Waiting,
            PreWait,
            Moving,
            Sunk
        }

        [SerializeField]
        private float maxHitForce;

        //[SerializeField]
        //[Range(0.0f, 1.0f)]
        //private float wallBounce = 0.5f;

        [SerializeField]
        private AudioSource ballAudioSource;

        [SerializeField]
        private AudioClip hitSFX;

        [Tooltip("이 파워 비율 이상에서는 hitSFXStrong 재생 (0~1)")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float strongHitPowerThreshold = 0.75f;

        [Tooltip("강하게 친 경우 재생할 효과음. 비워두면 hitSFX 사용")]
        [SerializeField]
        private AudioClip hitSFXStrong;

        [Tooltip("공을 칠 때 공 위치에서 생성할 파티클 프리팹 (옵션). 회전은 발사 방향에 맞춤")]
        [SerializeField]
        private GameObject hitParticlePrefab;

        [SerializeField]
        private AudioClip sinkSFX;

        [Header("Wall Collision")]
        [SerializeField]
        private AudioClip wallHitSFX;

        [SerializeField]
        private GameObject wallHitParticlePrefab;

        [Tooltip("벽 충돌음 연속 재생을 막는 최소 간격(초)")]
        [SerializeField]
        private float wallHitCooldown = 0.15f;

        [Header("OutOfBounds")]
        [SerializeField]
        private AudioClip outOfBoundsHitSFX;

        public bool IsAiming { get; private set; }
        public float CurrentPowerPercent { get; private set; }
        public Vector3 CurrentAimDirection { get; private set; }

        [Tooltip("OOB 태그에 닿은 뒤 리스폰까지 대기 시간(초)")]
        [SerializeField]
        private float outOfBoundsRespawnDelay = 1.0f;

        private float isMovingVelocityThreshold = 0.01f;
        private Vector3 lastWaitingPosition;
        // 마지막으로 샷을 친 시점의 공 위치 (OOB 리스폰용)
        private Vector3 lastShotPosition;
        // OOB 리스폰 코루틴 핸들 (취소용)
        private Coroutine respawnCoroutine;
        // 벽 충돌음 연속 재생 방지용
        private float lastWallHitTime = -10f;
        // 첫 샷 전엔 OOB 효과음 재생 안 함 (스폰 시 OOB 트리거 오발화 방지)
        private bool hasBeenHit;
        // 같은 OOB 진입 동안 효과음 1회만 재생. SetPosition(리스폰)에서 false로 리셋.
        private bool outOfBoundsFxPlayed;
        public State CurState { get; private set; }
        private Rigidbody rig;
        private TrailRenderer trailRenderer;

        private float preWaitStartTime = 0.0f;

        public event System.Action OnHit;
        public event System.Action OnBeginMoving;
        public event System.Action OnBeginWaiting;

        public static Ball Instance;

        void Awake()
        {
            Instance = this;

            // Get components.
            rig = GetComponent<Rigidbody>();
            trailRenderer = GetComponent<TrailRenderer>();
        }

        void Start()
        {
            // Subscribe to input events.
            InputController.Instance.OnBallTouchDown += OnBallTouchDown;
            InputController.Instance.OnBallTouchUp += OnBallTouchUp;
            GameManager.Instance.OnBallSunk += OnBallSunk;
            GameManager.Instance.OnLoadHole += OnLoadHole;

            lastWaitingPosition = transform.position;
        }

        void OnBallTouchDown()
        {
            if(GameManager.Instance.BallInHole)
                return;

            if(CurState != State.Waiting)
                return;

            IsAiming = true;
        }

        void OnBallTouchUp()
        {
            if(!IsAiming)
                return;

            if(GameManager.Instance.BallInHole)
                return;

            IsAiming = false;
            Hit(CurrentAimDirection * CurrentPowerPercent * maxHitForce);
            CurrentAimDirection = Vector3.zero;
            CurrentPowerPercent = 0.0f;
        }

        void Update()
        {
            if(!IsAiming)
                return;

            // This is where we calculate the aiming direction and power:

            // 1. Convert the ball's position to viewport coordinates.
            Vector3 ballPoint = Camera.main.WorldToViewportPoint(transform.position);
            ballPoint.x *= Camera.main.aspect;
            ballPoint.z = 0;

            // 2. Do the same with our finger/mouse position.
            Vector3 touchPoint = Camera.main.ScreenToViewportPoint(Input.mousePosition);
            touchPoint.x *= Camera.main.aspect;

            // 3. Calculate the direction between these two points.
            Vector3 screenDir = (ballPoint - touchPoint);

            // 4. Get the camera's forward direction - ignoring the Y axis.
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0;
            camForward.Normalize();

            // 5. Convert screen direction to a world direction.
            Vector3 aimDir = Camera.main.transform.right * screenDir.x + camForward * screenDir.y;

            CurrentAimDirection = aimDir.normalized;
            CurrentPowerPercent = aimDir.magnitude * 3;
            CurrentPowerPercent = Mathf.Clamp01(CurrentPowerPercent);
        }

        void LateUpdate()
        {
            CheckState();
        }

        void CheckState()
        {
            float velocity = rig.linearVelocity.magnitude;

            if(CurState == State.Waiting && velocity >= isMovingVelocityThreshold)
            {
                SetState(State.Moving);
            }
            else if(CurState == State.Moving && velocity < isMovingVelocityThreshold)
            {
                SetState(State.PreWait);
            }
            else if(CurState == State.PreWait && Time.time - preWaitStartTime > 0.2f)
            {
                SetState(State.Waiting);
            }
        }

        void SetState(State newState)
        {
            CurState = newState;

            if(CurState == State.Moving)
            {
                OnBeginMoving?.Invoke();
            }
            else if(CurState == State.Waiting)
            {
                CheckIfOutOfBounds();
                lastWaitingPosition = transform.position;
                OnBeginWaiting?.Invoke();
            }
            else if(CurState == State.PreWait)
            {
                preWaitStartTime = Time.time;
            }
        }

        public bool CanHit()
        {
            return rig.linearVelocity.magnitude < 0.02f;
        }

        public void Hit(Vector3 hitForce)
        {
            // 샷 직전 위치 기억 (OOB 감지 시 이 위치로 리스폰)
            lastShotPosition = transform.position;
            hasBeenHit = true;

            rig.AddForce(hitForce, ForceMode.VelocityChange);
            OnHit?.Invoke();

            float powerRatio = maxHitForce > 0 ? hitForce.magnitude / maxHitForce : 0;
            AudioClip clipToPlay = (powerRatio >= strongHitPowerThreshold && hitSFXStrong != null)
                ? hitSFXStrong
                : hitSFX;
            ballAudioSource.PlayOneShot(clipToPlay);

            if(hitParticlePrefab != null)
            {
                Vector3 dir = hitForce.sqrMagnitude > 0.0001f ? hitForce.normalized : transform.forward;
                GameObject fx = Instantiate(hitParticlePrefab, transform.position, Quaternion.LookRotation(dir));
                Destroy(fx, 2f);
            }
        }

        // Reset the ball's position if we are off the course.
        void CheckIfOutOfBounds()
        {
            Ray ray = new Ray(transform.position, Vector3.down);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit, 10))
            {
                if(hit.collider && hit.collider.CompareTag("OutOfBounds"))
                {
                    PlayOutOfBoundsFeedback(transform.position);
                    SetPosition(lastWaitingPosition);
                    return;
                }
            }
        }

        // OOB 태그에 닿으면 outOfBoundsRespawnDelay 초 뒤 직전 샷 위치로 리스폰.
        // 단, 그 시간 안에 OOB에서 벗어나면 예약 취소 (벽 튕김으로 스친 경우 오탐 방지).
        void OnCollisionEnter(Collision collision)
        {
            if(collision.collider != null && collision.collider.CompareTag("OutOfBounds"))
            {
                Vector3 fxPos = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                TryScheduleRespawn(fxPos);
            }
        }

        // 코스 프리팹은 바닥과 벽이 같은 MeshCollider라서 Enter 한 번만 발생.
        // 벽 충돌은 Stay에서 매 프레임 contact 노멀과 접근 속도로 감지.
        void OnCollisionStay(Collision collision)
        {
            if(collision.collider == null || collision.collider.CompareTag("OutOfBounds"))
                return;
            if(Time.time - lastWallHitTime < wallHitCooldown)
                return;

            for(int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if(contact.normal.y >= 0.3f)
                    continue;
                // 벽으로 향하는 속도 성분이 있어야 진짜 충돌. 가만히 기댄 상태에서 울리는 것 방지.
                float approachSpeed = -Vector3.Dot(collision.relativeVelocity, contact.normal);
                if(approachSpeed < 0.3f)
                    continue;

                if(wallHitSFX != null && ballAudioSource != null)
                    ballAudioSource.PlayOneShot(wallHitSFX);

                if(wallHitParticlePrefab != null)
                {
                    Quaternion rot = Quaternion.LookRotation(contact.normal);
                    GameObject fx = Instantiate(wallHitParticlePrefab, contact.point, rot);
                    Destroy(fx, 2f);
                }

                lastWallHitTime = Time.time;
                return;
            }
        }

        // OOB 진입 시 즉시 효과음. 첫 샷 전·홀인 후에는 무시. 다음 리스폰 전까지 1회만.
        void PlayOutOfBoundsFeedback(Vector3 fxPosition)
        {
            if(!hasBeenHit)
                return;
            if(CurState == State.Sunk || (GameManager.Instance != null && GameManager.Instance.BallInHole))
                return;
            if(outOfBoundsFxPlayed)
                return;
            outOfBoundsFxPlayed = true;

            if(outOfBoundsHitSFX != null && ballAudioSource != null)
                ballAudioSource.PlayOneShot(outOfBoundsHitSFX);
        }

        void OnCollisionExit(Collision collision)
        {
            if(collision.collider != null && collision.collider.CompareTag("OutOfBounds"))
                CancelRespawn();
        }

        void OnTriggerEnter(Collider other)
        {
            if(other != null && other.CompareTag("OutOfBounds"))
                TryScheduleRespawn(transform.position);
        }

        void OnTriggerExit(Collider other)
        {
            if(other != null && other.CompareTag("OutOfBounds"))
                CancelRespawn();
        }

        void TryScheduleRespawn(Vector3 fxPosition)
        {
            PlayOutOfBoundsFeedback(fxPosition);

            // 이미 예약돼 있으면 중복 트리거 무시
            if(respawnCoroutine != null)
                return;
            respawnCoroutine = StartCoroutine(RespawnAfterDelay());
        }

        void CancelRespawn()
        {
            if(respawnCoroutine == null)
                return;
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(outOfBoundsRespawnDelay);
            // 타이머 끝난 시점에 실제로 아직 OOB 위에 있는지 재확인.
            if(IsCurrentlyOverOutOfBounds())
            {
                SetPosition(lastShotPosition);
            }
            respawnCoroutine = null;
        }

        bool IsCurrentlyOverOutOfBounds()
        {
            // 공 중심에서 아래로 raycast. OOB 태그를 먼저 만나면 OOB 위에 있는 것.
            // 아무것도 안 맞으면 월드 밖으로 떨어진 것이므로 OOB 취급.
            Ray ray = new Ray(transform.position, Vector3.down);
            if(Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                return hit.collider != null && hit.collider.CompareTag("OutOfBounds");
            }
            return true;
        }

        public void SetPosition(Vector3 position)
        {
            rig.position = position;
            transform.position = position;
            rig.linearVelocity = Vector3.zero;
            rig.angularVelocity = Vector3.zero;
            // 새 스폰 위치를 '직전 샷 위치'로 초기화 (첫 샷 전에 OOB에 빠져도 안전)
            lastShotPosition = position;
            // 다음 OOB부터 다시 효과음 재생되도록 잠금 해제
            outOfBoundsFxPlayed = false;
            // 텔레포트 직후 이전 위치와 잇는 트레일 잔상 제거
            if(trailRenderer != null)
                trailRenderer.Clear();
        }

        public Vector3 GetPosition()
        {
            return rig.position;
        }

        void OnBallSunk()
        {
            SetState(State.Sunk);
            ballAudioSource.PlayOneShot(sinkSFX);
        }

        void OnLoadHole(CourseData course, int hole)
        {
            SetState(State.Waiting);
        }

        /*
        void OnCollisionStay (Collision collision)
        {
            // When we hit a wall we want to bounce off it
            for(int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 normal = contact.normal;

                // Continue if this is not a wall
                if(normal.y > 0.2f)
                    continue;

                Vector3 impulse = collision.GetContact(i).impulse;

                rig.AddForce(impulse * wallBounce, ForceMode.VelocityChange);
            }
        }
        */
    }
}