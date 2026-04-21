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
            startScale = transform.localScale.x;
        }

        void Update()
        {
            // 공이 Waiting 상태이고 조준 중이 아닐 때만 표시
            // 이벤트 기반이 아닌 상태 직접 폴링 → 연타 시 꺼진 채 유지되는 버그 방지
            bool shouldShow = ball.CurState == Ball.State.Waiting
                              && !InputController.Instance.IsInteractingWithBall;

            mr.enabled = shouldShow;

            if(!shouldShow)
                return;

            float s = Mathf.Sin(Time.time * scaleLerpSpeed) * scaleLerpSize;
            transform.localScale = Vector3.one * (s + startScale);
        }
    }
}
