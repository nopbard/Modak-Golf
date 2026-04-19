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

        private float isMovingVelocityThreshold = 0.01f;
        private Vector3 lastWaitingPosition;
        public State CurState { get; private set; }
        private Rigidbody rig;

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
                    rig.position = lastWaitingPosition;
                }
            }
        }

        public void SetPosition(Vector3 position)
        {
            rig.position = position;
            rig.linearVelocity = Vector3.zero;
            rig.angularVelocity = Vector3.zero;
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