using System.Collections.Generic;
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

        [Header("Spawn Pop-in (외부 호출용)")]
        [Tooltip("PlaySpawnPopIn() 시 scale 0→원본 되돌리는 시간 (초)")]
        [SerializeField] private float spawnPopDuration = 0.45f;
        [Tooltip("PlaySpawnPopIn() 이징")]
        [SerializeField] private Ease spawnPopEase = Ease.OutBack;

        private bool collected;
        // 부모(홀 등) 가 트윈으로 움직여도 영향 안 받도록 localPosition 기준으로 보빙.
        private Vector3 baseLocalPos;
        private bool spawnPoppingIn;   // 팝인 중이면 idle (회전/보빙) 일시 정지

        // 한 세션 동안 수집된 코인 ID 기록. 도메인 리로드(플레이 모드 재시작) 시 자동 초기화 → 게임 재실행 전까지 유지.
        private static readonly HashSet<string> s_CollectedIds = new HashSet<string>();

        // 이 코인의 식별자. Awake 에서 컨텍스트(코스/홀 or "Menu") + localPosition 으로 계산.
        private string coinId;

        // 외부 체크용: 특정 id 가 이미 수집됐는지
        public static bool IsCollected(string id) => s_CollectedIds.Contains(id);

        // id 생성 규칙 (외부에서 pre-instantiate 체크할 때도 같은 규칙으로 만들어 쓸 수 있게 노출)
        public static string BuildId(string context, Vector3 localPos)
        {
            return context + "@"
                 + localPos.x.ToString("F2") + ","
                 + localPos.y.ToString("F2") + ","
                 + localPos.z.ToString("F2");
        }

        // 현재 프레임의 컨텍스트 — 게임 씬이면 "CourseName/HN", 아니면 "Menu"
        public static string GetCurrentContext()
        {
            if(GameManager.Instance != null && GameManager.Instance.CurrentCourse != null)
                return GameManager.Instance.CurrentCourse.name + "/H" + GameManager.Instance.CurrentHole;
            return "Menu";
        }

        // 디버그/테스트용 수동 초기화
        public static void ResetCollected() => s_CollectedIds.Clear();

        void Reset()
        {
            var col = GetComponent<Collider>();
            if(col != null) col.isTrigger = true;
        }

        void Awake()
        {
            baseLocalPos = transform.localPosition;

            // 이미 이 세션에서 수집된 코인이면 즉시 제거 (재방문/재플레이 시 다시 안 뜸)
            coinId = BuildId(GetCurrentContext(), baseLocalPos);
            if(s_CollectedIds.Contains(coinId))
            {
                Destroy(gameObject);
                return;
            }
        }

        void Update()
        {
            if(collected) return;
            if(spawnPoppingIn) return;   // 팝인 중엔 idle 연출 보류
            if(idleSpin)
                transform.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.World);
            if(idleBobHeight > 0f && idleBobPeriod > 0f)
            {
                float y = Mathf.Sin((Time.time / idleBobPeriod) * Mathf.PI * 2f) * idleBobHeight;
                transform.localPosition = baseLocalPos + new Vector3(0f, y, 0f);
            }
        }

        // 외부(예: MenuSceneController) 에서 호출. 현재 localScale 을 목표로 기록 → 0 부터 OutBack 으로 튕겨 복귀.
        // 팝인 중엔 Update 의 idle 애니메이션이 멈춰있다가, 완료 시점에 idle 이 다시 작동함.
        public void PlaySpawnPopIn()
        {
            Vector3 target = transform.localScale;
            transform.DOKill();
            transform.localScale = Vector3.zero;
            spawnPoppingIn = true;
            transform.DOScale(target, spawnPopDuration)
                .SetEase(spawnPopEase)
                .OnComplete(() => { spawnPoppingIn = false; });
        }

        void OnTriggerEnter(Collider other)
        {
            if(collected) return;
            if(!other.CompareTag("Ball")) return;

            collected = true;

            // 이 세션에서 수집 기록. 이후 같은 코스/씬 재진입해도 이 코인은 다시 안 뜸.
            if(!string.IsNullOrEmpty(coinId))
                s_CollectedIds.Add(coinId);

            // 카운트 증가 (UI 는 이벤트로 반응)
            CoinCounter.Add(value);

            // SFX — 2D 로 재생해서 카메라 거리 감쇠 없이 일정 볼륨으로 들리게
            if(pickupSFX != null)
                AudioUtil.PlaySfx2D(pickupSFX, sfxVolume);

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
