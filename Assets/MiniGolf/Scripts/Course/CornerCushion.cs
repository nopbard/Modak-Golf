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

            // 입사 속도 재구성 (Unity 는 OnCollisionEnter 전에 이미 bounce 를 적용하므로
            // relativeVelocity 로 충돌 직전 상대 속도를 받음. 쿠션이 정적이라 이것이 곧 공의 입사 속도 크기)
            Vector3 incoming = -collision.relativeVelocity;
            float incomingSpeed = incoming.magnitude;
            if(incomingSpeed < minImpactSpeed) return;

            Vector3 normal = collision.GetContact(0).normal;

            // 완벽 mirror 반사
            Vector3 reflectedDir = Vector3.Reflect(incoming, normal).normalized;
            if(reflectedDir.sqrMagnitude < 0.0001f)
                reflectedDir = normal;                         // 극단적 엣지케이스 safe fallback

            float outSpeed = Mathf.Max(incomingSpeed * speedBoost, minOutSpeed);
            rb.linearVelocity = reflectedDir * outSpeed;
            rb.angularVelocity = Vector3.zero;                 // 회전 초기화 (예측 가능한 튕김)

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
