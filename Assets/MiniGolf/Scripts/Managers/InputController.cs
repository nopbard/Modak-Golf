using UnityEngine;

namespace MiniGolf
{
    public class InputController : MonoBehaviour
    {
        [SerializeField]
        private LayerMask ballTouchLayerMask;

        public event System.Action OnBallTouchEnter;
        public event System.Action OnBallTouchExit;
        public event System.Action OnBallTouchDown;
        public event System.Action OnBallTouchUp;

        private bool isTouchOverBall;
        public bool IsInteractingWithBall { get; private set; }

        public static InputController Instance;

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            // Raycast to see if we're touching the ball
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit ballHit;
            Physics.Raycast(ray, out ballHit, 1000, ballTouchLayerMask);

            // Check ball touch enter/exit
            if(ballHit.collider != null && !isTouchOverBall)
            {
                isTouchOverBall = true;
                OnBallTouchEnter?.Invoke();
            }
            else if(ballHit.collider == null && isTouchOverBall)
            {
                isTouchOverBall = false;
                OnBallTouchExit?.Invoke();
            }

            // Check mouse down and up
            if(Input.GetMouseButtonDown(0))
            {
                if(isTouchOverBall)
                {
                    IsInteractingWithBall = true;
                    OnBallTouchDown?.Invoke();
                }
            }
            else if(Input.GetMouseButtonUp(0))
            {
                if(IsInteractingWithBall)
                {
                    IsInteractingWithBall = false;
                    OnBallTouchUp?.Invoke();
                }
            }
        }
    }
}