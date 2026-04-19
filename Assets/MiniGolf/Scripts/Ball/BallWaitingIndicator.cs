using UnityEngine;

namespace MiniGolf
{
    [RequireComponent(typeof(MeshRenderer))]
    public class BallWaitingIndicator : MonoBehaviour
    {
        [SerializeField]
        private Ball ball;

        [SerializeField]
        private float scaleLerpSpeed;

        [SerializeField]
        private float scaleLerpSize;

        private float startScale;

        private MeshRenderer mr;

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
        }

        void Start()
        {
            ball.OnBeginWaiting += EnableVisual;
            ball.OnBeginMoving += DisableVisual;

            startScale = transform.localScale.x;
        }

        void Update()
        {
            if(InputController.Instance.IsInteractingWithBall)
                DisableVisual();

            if(!mr.enabled)
                return;

            float s = Mathf.Sin(Time.time * scaleLerpSpeed) * scaleLerpSize;
            transform.localScale = Vector3.one * (s + startScale);
        }

        void EnableVisual()
        {
            mr.enabled = true;
        }

        void DisableVisual()
        {
            mr.enabled = false;
        }
    }
}