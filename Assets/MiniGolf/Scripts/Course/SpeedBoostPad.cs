using UnityEngine;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

namespace MiniGolf
{
    // 바닥에 깔리는 "Speed" 부스터 패드.
    // 공이 트리거 범위에 들어오면 수평 속도를 부스트 (방향 유지).
    // 바닥 표시는 자식에 URP DecalProjector 나 SpriteRenderer 로 붙이고 Visual Root 필드에 연결.
    [ExecuteAlways]
    [RequireComponent(typeof(Collider))]
    public sealed class SpeedBoostPad : MonoBehaviour
    {
        [Header("Pad Size")]
        [Tooltip("패드의 가로(X) × 세로(Z) 크기 (m). 콜라이더와 Decal 영역 자동 동기화.")]
        [SerializeField] private Vector2 padSize = new Vector2(1f, 1f);
        [Tooltip("트리거 볼륨 높이 + Decal 투영 깊이 (m). 작을수록 바닥에 납작하게.")]
        [SerializeField] private float padHeight = 0.5f;

        [Header("Boost")]
        [Tooltip("현재 속도에 곱할 배율. 2.5 = 2.5배로 가속")]
        [SerializeField] private float boostMultiplier = 2.5f;
        [Tooltip("부스트 후 최소 보장 속도 (m/s). 느린 공도 확 튀어나가게.")]
        [SerializeField] private float minBoostSpeed = 8f;
        [Tooltip("최대 허용 속도 (m/s). 이상 시 클램프.")]
        [SerializeField] private float maxBoostSpeed = 20f;
        [Tooltip("연속 트리거 방지 쿨다운 (초). 패드 내에서 여러번 발동되지 않게.")]
        [SerializeField] private float cooldown = 0.4f;
        [Tooltip("이 속도 이하로 들어오는 공은 부스트 안 함 (멈춘 공 위에서 재발동 방지).")]
        [SerializeField] private float minEnterSpeed = 0.3f;

        [Header("Direction Override (optional)")]
        [Tooltip("ON 이면 공 방향을 이 Transform 의 forward 로 강제. OFF 면 공의 진행방향 유지.")]
        [SerializeField] private bool useDirectionalBoost = false;
        [Tooltip("direction override 시 사용할 Transform. 비우면 패드 자신의 transform.forward 사용.")]
        [SerializeField] private Transform directionSource;

        [Header("Visual FX")]
        [Tooltip("부스트 발동 시 punch scale 애니메이션 할 Transform (예: Decal / Sprite root)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float punchScale = 0.2f;
        [SerializeField] private float punchDuration = 0.3f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioClip boostSFX;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        private float lastTriggeredTime = -999f;
        private Vector3 visualOriginalScale = Vector3.one;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if(col != null) col.isTrigger = true;
            ApplySize();
        }

        void OnValidate()
        {
            if(!isActiveAndEnabled) return;
            ApplySize();
        }

        void Awake()
        {
            ApplySize();
            if(visualRoot != null) visualOriginalScale = visualRoot.localScale;
        }

        // padSize / padHeight 변경 시 BoxCollider 와 DecalProjector 사이즈를 자동 동기화.
        // Transform scale 과 함께 중첩 스케일되므로 padSize 는 "로컬 공간 기준" 크기.
        void ApplySize()
        {
            float halfH = padHeight * 0.5f;

            var box = GetComponent<BoxCollider>();
            if(box != null)
            {
                box.size   = new Vector3(padSize.x, padHeight, padSize.y);
                box.center = new Vector3(0f, halfH, 0f);
            }

            // 자식 DecalProjector 찾아서 영역 + 위치 동기화
            var decal = GetComponentInChildren<DecalProjector>(true);
            if(decal != null)
            {
                decal.size = new Vector3(padSize.x, padSize.y, padHeight);
                // Decal 의 로컬 위치: 패드 위쪽에 두고 아래로 투영 (pivotPos = 중심)
                var dt = decal.transform;
                Vector3 lp = dt.localPosition;
                lp.y = halfH;
                dt.localPosition = lp;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if(Time.time - lastTriggeredTime < cooldown) return;
            if(!other.CompareTag("Ball")) return;

            var rb = other.attachedRigidbody;
            if(rb == null) return;

            Vector3 vel = rb.linearVelocity;
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
            if(horiz.sqrMagnitude < minEnterSpeed * minEnterSpeed) return;

            lastTriggeredTime = Time.time;

            // 방향 결정
            Vector3 dir;
            if(useDirectionalBoost)
            {
                Transform src = directionSource != null ? directionSource : transform;
                Vector3 fwd = src.forward;
                fwd.y = 0f;
                dir = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : horiz.normalized;
            }
            else
            {
                dir = horiz.normalized;
            }

            // 속도 결정
            float newSpeed = Mathf.Clamp(
                Mathf.Max(horiz.magnitude * boostMultiplier, minBoostSpeed),
                0f, maxBoostSpeed);

            Vector3 newVel = dir * newSpeed;
            newVel.y = vel.y;                                  // 수직 성분 유지 (점프 등)
            rb.linearVelocity = newVel;

            PlayPunch();

            if(boostSFX != null)
                AudioSource.PlayClipAtPoint(boostSFX, transform.position, sfxVolume);
        }

        void PlayPunch()
        {
            if(visualRoot == null) return;
            visualRoot.DOKill();
            visualRoot.localScale = visualOriginalScale;
            visualRoot.DOPunchScale(Vector3.one * punchScale, punchDuration, 4, 0.5f);
        }

        void OnDestroy()
        {
            if(visualRoot != null) visualRoot.DOKill();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // 방향 시각화
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Transform src = useDirectionalBoost && directionSource != null ? directionSource : transform;
            Gizmos.color = useDirectionalBoost ? new Color(1f, 0.5f, 0f) : new Color(0.4f, 0.8f, 1f, 0.5f);
            if(useDirectionalBoost)
                Gizmos.DrawRay(origin, new Vector3(src.forward.x, 0, src.forward.z).normalized * 1.5f);
        }
#endif
    }
}
