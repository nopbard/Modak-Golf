using UnityEngine;
using DG.Tweening;

namespace MiniGolf
{
    // 바닥 장애물. 공 (tag "Ball") 이 trigger 에 닿으면:
    //   - 공의 수평 속도를 ±90° (노이즈 포함) 슬립시킴. 속도 magnitude 는 유지
    //   - 바나나는 DOScale(0) + 360° spin 으로 "뿅" 하며 사라짐
    //   - 한 번 먹으면 끝 (consumed 플래그로 재진입 방지)
    [RequireComponent(typeof(Collider))]
    public sealed class BananaPeel : MonoBehaviour, IBlastImmune
    {
        [Header("Slip")]
        [Tooltip("공이 진행방향에서 꺾이는 기준 각도 (도). 90 = 옆으로.")]
        [SerializeField] private float deflectAngle = 90f;
        [Tooltip("꺾임 각도에 더할 랜덤 노이즈 (±도)")]
        [SerializeField] private float angleNoise = 10f;
        [Tooltip("튕김 후 속도 배율. 1 = 현재 속도 그대로 유지.")]
        [Range(0.1f, 2f)] [SerializeField] private float speedMultiplier = 1f;
        [Tooltip("이 속도(m/s) 이하로 들어오는 공은 무시 (멈춰있다 닿는 경우 오발 방지)")]
        [SerializeField] private float minTriggerSpeed = 0.5f;

        [Header("Pop FX")]
        [Tooltip("사라지는 시간 (초)")]
        [SerializeField] private float popDuration = 0.35f;
        [Tooltip("팝 중 회전 각도 (도). 360 = 한 바퀴")]
        [SerializeField] private float spinAmount = 360f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioClip slipSFX;
        [Range(0f, 1f)] [SerializeField] private float slipSFXVolume = 1f;

        private bool consumed;

        void Reset()
        {
            // 컴포넌트 처음 붙을 때 collider 를 Trigger 로 자동 전환
            var col = GetComponent<Collider>();
            if(col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if(consumed) return;
            if(!other.CompareTag("Ball")) return;

            var rb = other.attachedRigidbody;
            if(rb == null) return;

            Vector3 vel = rb.linearVelocity;
            // 수평 속도만 가지고 슬립 판단 (수직 낙하는 무시)
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
            float horizSpeed = horiz.magnitude;
            if(horizSpeed < minTriggerSpeed) return;

            consumed = true;

            // ── 튕김 방향: 수평면 ±90° + 랜덤 노이즈 ──────────────────
            float sign = Random.value < 0.5f ? -1f : 1f;
            float angle = deflectAngle * sign + Random.Range(-angleNoise, angleNoise);
            Vector3 newHoriz = Quaternion.Euler(0f, angle, 0f) * (horiz / horizSpeed);
            Vector3 newVel = newHoriz * (horizSpeed * speedMultiplier);
            newVel.y = vel.y;                                        // 수직 성분은 유지
            rb.linearVelocity = newVel;

            if(slipSFX != null)
                AudioSource.PlayClipAtPoint(slipSFX, transform.position, slipSFXVolume);

            // ── 팝 연출 ────────────────────────────────────────────────
            // collider 는 즉시 비활성 → 같은 프레임 재진입 방지
            foreach(var c in GetComponents<Collider>())
                c.enabled = false;

            // 기존 트윈이 있으면 정리
            transform.DOKill();

            var seq = DOTween.Sequence();
            // 먼저 살짝 부풀었다가 (anticipation) 0 으로 사라지기 = 뿅
            Vector3 startScale = transform.localScale;
            seq.Append(transform.DOScale(startScale * 1.25f, popDuration * 0.25f).SetEase(Ease.OutQuad));
            seq.Append(transform.DOScale(Vector3.zero, popDuration * 0.75f).SetEase(Ease.InBack));
            // 전체 구간에 걸쳐 회전
            seq.Join(transform.DOLocalRotate(
                new Vector3(0f, spinAmount, 0f),
                popDuration,
                RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad));
            seq.OnComplete(() => Destroy(gameObject));
        }

        void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
