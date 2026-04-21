using UnityEngine;
using DG.Tweening;

namespace MiniGolf
{
    public class Flag : MonoBehaviour
    {
        [SerializeField]
        private Transform flagObject;

        [SerializeField]
        private float startRiseDistance;

        [SerializeField]
        private float maxRiseHeight;

        [Header("Sink FX")]
        [Tooltip("공이 홀에 빠졌을 때 '뿅' 사라지는 애니메이션 시간")]
        [SerializeField] private float sinkDuration = 0.4f;
        [Tooltip("사라지기 전 살짝 위로 튀어오르는 높이 (m)")]
        [SerializeField] private float sinkHopHeight = 0.2f;
        [Tooltip("사라질 때 회전 각도 (도)")]
        [SerializeField] private float sinkSpin = 360f;

        private Vector3 flagStartPos;
        private bool sunk;

        void Start()
        {
            flagStartPos = flagObject.localPosition;
        }

        void Update()
        {
            if(sunk) return;                         // sink 중엔 rise 로직 멈춤
            if(Ball.Instance == null) return;
            float ballDistance = Vector3.Distance(transform.position, Ball.Instance.transform.position);
            float f = Mathf.Clamp01(startRiseDistance - ballDistance);

            flagObject.localPosition = flagStartPos + (Vector3.up * f * maxRiseHeight);
        }

        // 공이 들어갔을 때 호출. 뿅하고 사라진 뒤 비활성화.
        public void Sink()
        {
            if(sunk) return;
            sunk = true;

            transform.DOKill();
            Vector3 startScale = transform.localScale;
            Vector3 pos = transform.position;

            var seq = DOTween.Sequence();
            // 살짝 위로 튀면서 동시에 스케일 0 으로
            if(sinkHopHeight > 0f)
                seq.Join(transform.DOMoveY(pos.y + sinkHopHeight, sinkDuration).SetEase(Ease.OutQuad));
            seq.Join(transform.DOScale(Vector3.zero, sinkDuration).SetEase(Ease.InBack));
            if(Mathf.Abs(sinkSpin) > 0.01f)
                seq.Join(transform.DOLocalRotate(new Vector3(0f, sinkSpin, 0f), sinkDuration, RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad));
            seq.OnComplete(() => gameObject.SetActive(false));
        }

        void OnDestroy()
        {
            transform.DOKill();
        }
    }
}