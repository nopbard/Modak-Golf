using UnityEngine;
using DG.Tweening;

namespace MiniGolf
{
    // 코너 쿠션. 공(Ball)이 부딪히면:
    //   1. 쿠션이 트램펄린처럼 "띠용띠용" DOPunchScale 애니메이션
    //   2. 공은 접촉 법선 기준으로 mirror 반사 (입사각 = 반사각)
    //   3. 속도는 speedBoost 배율로 증가
    //
    // 물리엔진이 자체적으로 bounce 를 적용하지만 OnCollisionEnter 에서 velocity 를 override 하기 때문에
    // Collider 의 PhysicsMaterial bounciness 값은 무관 (원하면 0 으로 둬도 됨).
    [RequireComponent(typeof(Collider))]
    public sealed class CornerCushion : MonoBehaviour
    {
        [Header("Reflection")]
        [Tooltip("공 튕겨나가는 속도 배율. 1 = 입사속도 그대로, 1.3 = 30% 증가")]
        [SerializeField] private float speedBoost = 1.3f;
        [Tooltip("이 속도(m/s) 이하로 들어오는 공은 튕기지 않음 (멈춘 공에 살짝 닿는 경우 무시)")]
        [SerializeField] private float minImpactSpeed = 0.3f;
        [Tooltip("튕긴 뒤 이 값보다 느리면 강제로 이 값까지 올림 (트램펄린 느낌)")]
        [SerializeField] private float minOutSpeed = 4f;

        [Header("Trampoline FX (DOTween PunchScale)")]
        [Tooltip("스케일이 이만큼 변화하며 튐")]
        [SerializeField] private float punchScale = 0.3f;
        [Tooltip("튀는 애니메이션 전체 시간 (초)")]
        [SerializeField] private float punchDuration = 0.5f;
        [Tooltip("튐 횟수 (oscillation). 클수록 더 떨림")]
        [SerializeField] private int punchVibrato = 6;
        [Tooltip("탄성. 0=단단, 1=매우 탄성")]
        [Range(0f, 1f)] [SerializeField] private float punchElasticity = 0.6f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioClip bounceSFX;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        private Vector3 originalScale;

        void Awake()
        {
            originalScale = transform.localScale;
        }

        void OnCollisionEnter(Collision collision)
        {
            if(!collision.collider.CompareTag("Ball")) return;
            var rb = collision.rigidbody;
            if(rb == null) return;

            float approachSpeed = collision.relativeVelocity.magnitude;
            if(approachSpeed < minImpactSpeed) return;

            // Unity 물리엔진이 이미 PhysicsMaterial 의 bounciness 로 mirror reflection 을 적용함.
            // (bounciness 1 이면 완벽 탄성 → 45° 빗면에서 +Z 입사가 -X 로 튕김 = 90° 꺾임)
            // 여기서는 튕긴 뒤 속도만 부스트/최소값 보정.
            Vector3 outVel = rb.linearVelocity;
            float outSpeed = outVel.magnitude;
            if(outSpeed < 0.01f) outVel = collision.GetContact(0).normal * approachSpeed;  // fallback

            float targetSpeed = Mathf.Max(approachSpeed * speedBoost, minOutSpeed);
            rb.linearVelocity = outVel.normalized * targetSpeed;

            PlayPunch();

            if(bounceSFX != null)
                AudioSource.PlayClipAtPoint(bounceSFX, transform.position, sfxVolume);
        }

        void PlayPunch()
        {
            transform.DOKill();
            transform.localScale = originalScale;              // 이전 튕김 스케일 초기화
            transform.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, punchElasticity);
        }

        void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
