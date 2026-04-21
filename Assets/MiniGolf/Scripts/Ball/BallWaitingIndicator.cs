using UnityEngine;

namespace MiniGolf
{
    [RequireComponent(typeof(MeshRenderer))]
    public class BallWaitingIndicator : MonoBehaviour
    {
        [SerializeField] private Ball ball;
        [SerializeField] private float scaleLerpSpeed;
        [SerializeField] private float scaleLerpSize;

        private float startScale;
        private MeshRenderer mr;

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
            mr.enabled = false;                        // 기본 숨김 — Update 가 상태 보고 켬
        }

        void Start()
        {
            startScale = transform.localScale.x;
        }

        void Update()
        {
            // 표시 조건: 공이 물리적으로 정지 + 사용자가 공 안 건드림 + 스폰 쿨다운 끝남
            bool shouldShow = ball != null
                              && !ball.IsMoving
                              && !ball.IsSettling
                              && InputController.Instance != null
                              && !InputController.Instance.IsInteractingWithBall;

            mr.enabled = shouldShow;
            if(!shouldShow) return;

            float s = Mathf.Sin(Time.time * scaleLerpSpeed) * scaleLerpSize;
            transform.localScale = Vector3.one * (s + startScale);
        }
    }
}
