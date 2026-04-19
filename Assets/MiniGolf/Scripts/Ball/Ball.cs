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

        [SerializeField]
        private AudioClip sinkSFX;

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

            rig.AddForce(hitForce, ForceMode.VelocityChange);
            OnHit?.Invoke();

            ballAudioSource.PlayOneShot(hitSFX);
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
                    SetPosition(lastWaitingPosition);
                }
            }
        }

        // OOB 태그에 닿으면 outOfBoundsRespawnDelay 초 뒤 직전 샷 위치로 리스폰.
        // 단, 그 시간 안에 OOB에서 벗어나면 예약 취소 (벽 튕김으로 스친 경우 오탐 방지).
        void OnCollisionEnter(Collision collision)
        {
            if(collision.collider != null && collision.collider.CompareTag("OutOfBounds"))
                TryScheduleRespawn();
        }

        void OnCollisionExit(Collision collision)
        {
            if(collision.collider != null && collision.collider.CompareTag("OutOfBounds"))
                CancelRespawn();
        }

        void OnTriggerEnter(Collider other)
        {
            if(other != null && other.CompareTag("OutOfBounds"))
                TryScheduleRespawn();
        }

        void OnTriggerExit(Collider other)
        {
            if(other != null && other.CompareTag("OutOfBounds"))
                CancelRespawn();
        }

        void TryScheduleRespawn()
        {
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
            // (벽 튕김으로 스쳐서 Exit 콜백이 누락되는 경우 대비)
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