using System.Collections;
using UnityEngine;

namespace MiniGolf
{
    public class Bomb : MonoBehaviour
    {
        [Header("Explosion")]
        [Tooltip("터질 때 생성할 파티클/이펙트 프리팹")]
        [SerializeField] private GameObject explosionPrefab;

        [Tooltip("공에 가하는 폭발 힘 (클수록 높이/멀리 날아감)")]
        [SerializeField] private float explosionForce = 14f;

        [Tooltip("위쪽 방향 보정 (0=수평, 1=수직). 0.6 정도가 자연스러움")]
        [SerializeField] [Range(0f, 1f)] private float upwardBias = 0.6f;

        [Header("Blast Radius (주변 Rigidbody 에 영향)")]
        [Tooltip("이 반경 내의 Rigidbody 오브젝트도 함께 날려버림 (공/바나나 등 인터랙티브 오브젝트는 제외)")]
        [SerializeField] private float blastRadius = 3f;

        [Tooltip("AddExplosionForce 로 주변 객체에 가할 힘. Impulse 라 mass=1 기준 ~같은 m/s 속도가 붙음.")]
        [SerializeField] private float blastForce = 15f;

        [Tooltip("AddExplosionForce 의 upwardsModifier. 값이 클수록 위로 튀어오름. 0.3~0.6 권장.")]
        [SerializeField] private float blastUpwards = 0.3f;

        [Tooltip("객체에 가할 수 있는 최대 속도 크기 (m/s). 너무 가벼운 rigidbody 가 우주로 날아가는 것 방지.")]
        [SerializeField] private float blastVelocityClamp = 20f;

        [Tooltip("여기 포함된 레이어만 영향 받음 (기본 Everything)")]
        [SerializeField] private LayerMask blastLayerMask = ~0;

        [Header("Audio")]
        [SerializeField] private AudioClip explosionSFX;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Timing")]
        [Tooltip("폭발 후 이펙트 오브젝트 자동 제거 시간(초)")]
        [SerializeField] private float effectLifetime = 3f;

        [Tooltip("폭발 후 리스폰(재등장)까지 대기 시간(초). 0 이하면 리스폰 안 함(영구 파괴).")]
        [SerializeField] private float respawnDelay = 2f;

        [Tooltip("리스폰 시 scale 0→원본 팝인 애니메이션 지속 (초). 0 이면 즉시 표시.")]
        [SerializeField] private float respawnPopDuration = 0.35f;

        private bool exploded;
        private Vector3 originalScale;
        private Coroutine respawnCo;

        // 공이 직접 충돌했을 때 (Rigidbody Collider)
        void OnCollisionEnter(Collision collision)
        {
            if(exploded) return;
            if(collision.gameObject.GetComponent<Ball>() == null) return;
            Explode(collision.gameObject.GetComponent<Rigidbody>(), collision.GetContact(0).point);
        }

        // 트리거 콜라이더로 설정된 경우도 대응
        void OnTriggerEnter(Collider other)
        {
            if(exploded) return;
            if(other.GetComponent<Ball>() == null) return;
            Explode(other.GetComponent<Rigidbody>(), transform.position);
        }

        void Explode(Rigidbody ballRig, Vector3 contactPoint)
        {
            exploded = true;
            Vector3 center = transform.position;

            // 이펙트 생성
            if(explosionPrefab != null)
            {
                GameObject fx = Instantiate(explosionPrefab, center, Quaternion.identity);
                Destroy(fx, effectLifetime);
            }

            // 효과음 — 2D 로 재생해서 카메라 거리 감쇠 없이 일정 볼륨으로 들리게
            if(explosionSFX != null)
                AudioUtil.PlaySfx2D(explosionSFX, sfxVolume);

            // ── 공: 기존 방식 유지 (velocity 초기화 후 방향+upwardBias 로 발사) ──
            if(ballRig != null)
            {
                Vector3 dir = (ballRig.position - center);
                if(dir.sqrMagnitude < 0.001f)
                    dir = Vector3.up;
                dir.y += upwardBias;
                dir = dir.normalized;

                ballRig.linearVelocity = Vector3.zero;
                ballRig.angularVelocity = Vector3.zero;
                ballRig.AddForce(dir * explosionForce, ForceMode.VelocityChange);
            }

            // ── 주변 Rigidbody: AddExplosionForce 로 날림 ──
            // OverlapSphere → 중복 Rigidbody 제거 → 인터랙티브 오브젝트 제외 → 힘 적용 → 속도 클램프
            var seen = new System.Collections.Generic.HashSet<Rigidbody>();
            Collider[] hits = Physics.OverlapSphere(center, blastRadius, blastLayerMask, QueryTriggerInteraction.Ignore);
            foreach(var col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if(rb == null) continue;
                if(!seen.Add(rb)) continue;                      // 같은 rb 의 여러 collider 중복 방지
                if(rb.isKinematic) continue;
                // 공 / 바나나 처럼 자체 로직을 가진 인터랙티브 오브젝트는 폭발에 안 쓸려감
                if(rb.GetComponentInChildren<IBlastImmune>() != null) continue;
                if(rb.GetComponentInParent<IBlastImmune>() != null) continue;

                rb.AddExplosionForce(blastForce, center, blastRadius, blastUpwards, ForceMode.Impulse);

                // 너무 가벼운 mass 나 너무 가까운 거리에서 생기는 "우주로 발사" 방지
                if(blastVelocityClamp > 0f && rb.linearVelocity.magnitude > blastVelocityClamp)
                    rb.linearVelocity = rb.linearVelocity.normalized * blastVelocityClamp;
            }

            // 폭탄 메시/콜라이더 숨김 (중복 폭발 방지)
            SetVisualsEnabled(false);

            // respawnDelay 양수 → 해당 시간 뒤 리스폰. 0 이하 → 기존 동작대로 제거.
            if(respawnDelay > 0f)
            {
                respawnCo = StartCoroutine(RespawnAfterDelay());
            }
            else
            {
                Destroy(gameObject, effectLifetime);
            }
        }

        void SetVisualsEnabled(bool enabled)
        {
            foreach(var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = enabled;
            foreach(var c in GetComponentsInChildren<Collider>(true))
                c.enabled = enabled;
        }

        IEnumerator RespawnAfterDelay()
        {
            // 원본 스케일 기억 (첫 실행 시만).
            if(originalScale == Vector3.zero) originalScale = transform.localScale;

            yield return new WaitForSeconds(respawnDelay);

            // 리스폰: 플래그 리셋 + 시각/콜라이더 복구 + 팝인.
            exploded = false;
            SetVisualsEnabled(true);

            if(respawnPopDuration > 0f)
            {
                transform.localScale = Vector3.zero;
                float t = 0f;
                while(t < respawnPopDuration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / respawnPopDuration);
                    // OutBack 느낌 easing
                    float eased = 1f - Mathf.Pow(1f - k, 3f);
                    transform.localScale = originalScale * eased;
                    yield return null;
                }
                transform.localScale = originalScale;
            }
            respawnCo = null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
#endif
    }
}
