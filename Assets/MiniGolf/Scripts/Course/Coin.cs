using UnityEngine;
using DG.Tweening;

namespace MiniGolf
{
    // 바닥 아이템. 공(tag "Ball") 이 트리거에 닿으면:
    //   - CoinCounter.Add(value) 호출 (UI 자동 갱신)
    //   - DOTween 팝업: 위로 튀어오르면서 Y축 spin + scale 커졌다 사라짐
    //   - SFX 재생
    //   - 오브젝트 파괴
    [RequireComponent(typeof(Collider))]
    public sealed class Coin : MonoBehaviour
    {
        [Header("Value")]
        [SerializeField] private int value = 1;

        [Header("Idle Animation")]
        [Tooltip("대기 상태에서 천천히 회전시킬지")]
        [SerializeField] private bool idleSpin = true;
        [Tooltip("초당 회전 속도 (deg/s)")]
        [SerializeField] private float idleSpinSpeed = 120f;
        [Tooltip("대기중 위아래 보빙 높이 (m). 0이면 off")]
        [SerializeField] private float idleBobHeight = 0.05f;
        [Tooltip("보빙 주기 (초)")]
        [SerializeField] private float idleBobPeriod = 1.2f;

        [Header("Pickup FX (DOTween)")]
        [Tooltip("튀어오르는 높이 (m)")]
        [SerializeField] private float popRise = 0.8f;
        [Tooltip("팝 애니메이션 총 시간 (초)")]
        [SerializeField] private float popDuration = 0.4f;
        [Tooltip("팝 중 회전 (deg, Y축). 720 = 두 바퀴")]
        [SerializeField] private float popSpin = 720f;
        [Tooltip("팝 중 최대로 커지는 배율")]
        [SerializeField] private float popScale = 1.4f;

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSFX;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        private bool collected;
        private Vector3 basePos;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if(col != null) col.isTrigger = true;
        }

        void Awake()
        {
            basePos = transform.position;
        }

        void Update()
        {
            if(collected) return;
            if(idleSpin)
                transform.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.World);
            if(idleBobHeight > 0f && idleBobPeriod > 0f)
            {
                float y = Mathf.Sin((Time.time / idleBobPeriod) * Mathf.PI * 2f) * idleBobHeight;
                transform.position = basePos + new Vector3(0f, y, 0f);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if(collected) return;
            if(!other.CompareTag("Ball")) return;

            collected = true;

            // 카운트 증가 (UI 는 이벤트로 반응)
            CoinCounter.Add(value);

            // SFX
            if(pickupSFX != null)
                AudioSource.PlayClipAtPoint(pickupSFX, transform.position, sfxVolume);

            // 추가 픽업 방지
            foreach(var c in GetComponents<Collider>()) c.enabled = false;

            // ── 팝 연출: 위로 튀어오르며 회전 + 살짝 커졌다 사라짐 ──
            transform.DOKill();
            Vector3 startScale = transform.localScale;

            var seq = DOTween.Sequence();
            // Y 축 위로 튀기 (OutQuad 로 감속)
            seq.Join(transform.DOMoveY(transform.position.y + popRise, popDuration)
                .SetEase(Ease.OutQuad));
            // 스케일: 커졌다 사라짐 (InOutBack 느낌)
            seq.Join(transform.DOScale(startScale * popScale, popDuration * 0.3f)
                .SetEase(Ease.OutQuad));
            seq.Insert(popDuration * 0.3f,
                transform.DOScale(Vector3.zero, popDuration * 0.7f).SetEase(Ease.InBack));
            // 회전
            seq.Join(transform.DOLocalRotate(new Vector3(0f, popSpin, 0f), popDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
            seq.OnComplete(() => Destroy(gameObject));
        }

        void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
