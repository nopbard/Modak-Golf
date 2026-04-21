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

        [Header("Audio")]
        [SerializeField] private AudioClip explosionSFX;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Timing")]
        [Tooltip("폭발 후 이펙트 오브젝트 자동 제거 시간(초)")]
        [SerializeField] private float effectLifetime = 3f;

        private bool exploded;

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

            // 이펙트 생성
            if(explosionPrefab != null)
            {
                GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                Destroy(fx, effectLifetime);
            }

            // 효과음 (오브젝트 파괴 후에도 재생되도록 PlayClipAtPoint 사용)
            if(explosionSFX != null)
                AudioSource.PlayClipAtPoint(explosionSFX, transform.position, sfxVolume);

            // 공에 힘 적용
            if(ballRig != null)
            {
                // 폭탄 → 공 방향에 위쪽 보정을 더한 뒤 정규화
                Vector3 dir = (ballRig.position - transform.position);
                if(dir.sqrMagnitude < 0.001f)
                    dir = Vector3.up;   // 정확히 겹쳐있으면 위로
                dir.y += upwardBias;
                dir = dir.normalized;

                // 기존 속도를 먼저 초기화 → 일관된 발사 거리
                ballRig.linearVelocity = Vector3.zero;
                ballRig.angularVelocity = Vector3.zero;
                ballRig.AddForce(dir * explosionForce, ForceMode.VelocityChange);
            }

            // 폭탄 메시 즉시 숨기기, 오브젝트는 effectLifetime 후 제거
            foreach(var r in GetComponentsInChildren<Renderer>())
                r.enabled = false;

            // 콜라이더도 끄기 (중복 폭발 방지)
            foreach(var c in GetComponentsInChildren<Collider>())
                c.enabled = false;

            Destroy(gameObject, effectLifetime);
        }
    }
}
