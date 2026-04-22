using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace MiniGolf
{
    // 메뉴 씬의 "월드 속 UX" 컨트롤러.
    //
    // 플로우:
    //   [씬 로드] → GameStart 지하에서 뿅 등장 + "Game Start" 텍스트 팝업
    //          → DOTween 끝나는 시점에 공을 GameStart 의 BallStart 위치에 스폰
    //          → 유저가 공 홀인
    //          → GameStart 지하로 하강 → CourseSelect 지하에서 뿅 등장
    //          → DOTween 끝나는 시점에 공을 CourseSelect 의 BallStart 위치에 스폰
    //          → 유저가 코스 홀에 공 넣기 → 해당 코스 씬 로드
    //
    // Ball 은 Hierarchy 상 GameStart / CourseSelect 자식이 아니어도 됨 (두트윈 애니메이션 영향 X).
    // 단순히 OnComplete 시점에 SetPosition + SetActive(true).
    [DefaultExecutionOrder(100)]
    public sealed class MenuSceneController : MonoBehaviour
    {
        [Header("Scene Objects")]
        [SerializeField] private Transform gameStartRoot;
        [SerializeField] private Transform courseSelectRoot;
        [Tooltip("GameStart 등장 완료 시 공이 놓일 위치. GameStart 의 BallStart 자식.")]
        [SerializeField] private Transform gameStartBallSpawn;
        [Tooltip("CourseSelect 등장 완료 시 공이 놓일 위치. CourseSelect 의 BallStart 자식.")]
        [SerializeField] private Transform courseSelectBallSpawn;

        [Header("Game Start Label")]
        [Tooltip("'Game Start' 라벨들. 씬 로드 시 scale 0→원본 으로 팝업 (여러 개 가능 — 텍스트 + 스프라이트 조합 등)")]
        [SerializeField] private Transform[] gameStartLabels;

        [Header("Course Select Label")]
        [Tooltip("CourseSelect 등장 시 scale 0→원본 으로 팝업할 라벨들. 코스 선택(씬 전환) 시에는 다시 0으로 축소 (여러 개 가능 — 텍스트 + 스프라이트 조합 등)")]
        [SerializeField] private Transform[] courseSelectLabels;

        [Header("Coin HUD")]
        [Tooltip("Menu 씬의 코인 HUD 를 CourseSelect 등장에 맞춰 팝업시킬 대상 Transform. 보통 CoinCountHUD/Container 를 연결 (ScreenSpaceOverlay Canvas 루트는 스케일이 CanvasScaler 에 의해 덮어써지므로 Canvas 내부 자식을 지정해야 함). 비워두면 HUD 팝업 스킵.")]
        [SerializeField] private Transform coinHudContent;

        [Header("CourseSelect Coin Spawn")]
        [Tooltip("CourseSelect 등장 후 일정 시간 뒤 생성할 코인 프리팹. 비워두면 스폰 스킵.")]
        [SerializeField] private Coin courseSelectCoinPrefab;
        [Tooltip("코인 생성 위치/회전/스케일/부모의 기준이 되는 Transform 들. 각 마커마다 코인 1개 생성. 마커 GameObject 는 비활성 상태여도 됨 (위치만 읽음).")]
        [SerializeField] private Transform[] courseSelectCoinSpawnPoints;
        [Tooltip("CourseSelect 등장 애니메이션 완료 후 첫 코인이 생성되기까지의 대기 시간 (초)")]
        [SerializeField] private float courseSelectCoinSpawnDelay = 1.5f;
        [Tooltip("여러 마커일 때 다음 코인이 생성되기까지의 추가 대기 시간 (초). 0 이면 동시에 생성.")]
        [SerializeField] private float courseSelectCoinSpawnStagger = 0.08f;

        [Header("Animation")]
        [Tooltip("지하에서 뿅 올라오는 Y 오프셋 (m)")]
        [SerializeField] private float popDepth = 3f;
        [Tooltip("등장 시간 (초)")]
        [SerializeField] private float popInDuration = 0.7f;
        [Tooltip("퇴장 시간 (초)")]
        [SerializeField] private float popOutDuration = 0.5f;
        [Tooltip("코스 로드 전 짧은 대기 시간 (연출용)")]
        [SerializeField] private float loadDelay = 0.6f;
        [Tooltip("GameStart 홀 인 이후 CourseSelect 전환까지의 지연 (Flag sink 연출 시간)")]
        [SerializeField] private float postSinkDelay = 1.0f;

        [Header("Refs")]
        [SerializeField] private CourseList courseList;

        [Header("CourseSelect Physics")]
        [Tooltip("CourseSelect 등장 애니메이션 동안 물리(Rigidbody)를 얼려둘 루트. 보통 CourseSelect/MODAK PUTT 같은 글자 컨테이너를 연결. 비워두면 동결 스킵.")]
        [SerializeField] private Transform courseSelectPhysicsFreezeRoot;

        [Header("Ball Spawn")]
        [Tooltip("GameStart/CourseSelect 두트윈 끝난 뒤, 공이 등장하기까지의 대기 시간 (초)")]
        [SerializeField] private float ballSpawnDelay = 0.2f;
        [Tooltip("공이 착지해 멈추기 전까지 인디케이터/조준을 막는 시간 (초)")]
        [SerializeField] private float ballSettleDuration = 1.0f;
        [Tooltip("공이 스폰된 뒤 카메라가 공 팔로우를 시작할 때까지의 대기 시간 (초). GameStart/CourseSelect 모두에 적용. 이 동안 카메라는 제자리에 멈춰 있다가 이후 자연스럽게 따라가기 시작.")]
        [UnityEngine.Serialization.FormerlySerializedAs("firstSpawnCameraFollowDelay")]
        [SerializeField] private float spawnCameraFollowDelay = 1.5f;

        enum Phase { Intro, ShowingGameStart, Transitioning, ShowingCourseSelect, Loading }
        Phase phase = Phase.Intro;

        Vector3 gameStartTargetPos;
        Vector3 courseSelectTargetPos;
        Vector3[] gameStartLabelTargetScales;
        Vector3[] courseSelectLabelTargetScales;
        Vector3 coinHudTargetScale;

        // CourseSelect 등장 애니메이션 동안 얼려둘 리지드바디들. Awake 에서 한 번 캐시.
        Rigidbody[] courseSelectFrozenBodies;

        void Awake()
        {
            // GameStart 지하로, CourseSelect 지하+비활성. 공은 Intro 끝날 때 까지 숨김.
            if(gameStartRoot != null)
            {
                gameStartTargetPos = gameStartRoot.position;
                gameStartRoot.position = gameStartTargetPos + Vector3.down * popDepth;
            }
            if(courseSelectRoot != null)
            {
                courseSelectTargetPos = courseSelectRoot.position;
                courseSelectRoot.position = courseSelectTargetPos + Vector3.down * popDepth;
                courseSelectRoot.gameObject.SetActive(false);
            }
            if(gameStartLabels != null && gameStartLabels.Length > 0)
            {
                gameStartLabelTargetScales = new Vector3[gameStartLabels.Length];
                for(int i = 0; i < gameStartLabels.Length; i++)
                {
                    if(gameStartLabels[i] == null) continue;
                    gameStartLabelTargetScales[i] = gameStartLabels[i].localScale;
                    gameStartLabels[i].localScale = Vector3.zero;
                }
            }
            if(courseSelectLabels != null && courseSelectLabels.Length > 0)
            {
                courseSelectLabelTargetScales = new Vector3[courseSelectLabels.Length];
                for(int i = 0; i < courseSelectLabels.Length; i++)
                {
                    if(courseSelectLabels[i] == null) continue;
                    courseSelectLabelTargetScales[i] = courseSelectLabels[i].localScale;
                    courseSelectLabels[i].localScale = Vector3.zero;
                }
            }

            // CourseSelect 등장 시 튀는 것을 막기 위해, 지정 루트 밑의 모든 Rigidbody 를 미리 캐시하고 kinematic 처리.
            // 애니메이션이 끝나는 시점(OnComplete)에 isKinematic=false 로 해제 → 그때부터 중력 적용.
            if(courseSelectPhysicsFreezeRoot != null)
            {
                courseSelectFrozenBodies = courseSelectPhysicsFreezeRoot.GetComponentsInChildren<Rigidbody>(true);
                SetCourseSelectBodiesKinematic(true);
            }

            // 코인 HUD: Menu 씬에서는 CourseSelect 단계에서만 보이게. Intro/GameStart 구간 동안은 scale 0 으로 숨김.
            // 인스펙터에서 지정 안 했으면 자동 탐색 (CoinCountHUD/Container 관례).
            if(coinHudContent == null)
            {
                var hudGO = GameObject.Find("CoinCountHUD");
                if(hudGO != null && hudGO.transform.childCount > 0)
                    coinHudContent = hudGO.transform.GetChild(0);
            }
            // 방어: ScreenSpaceOverlay Canvas 루트가 할당된 경우 → 스케일 조작이 CanvasScaler 에 의해 무효화되므로,
            // 첫 자식(Container 관례) 으로 자동 전환.
            if(coinHudContent != null)
            {
                var canvas = coinHudContent.GetComponent<Canvas>();
                if(canvas != null && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && coinHudContent.childCount > 0)
                    coinHudContent = coinHudContent.GetChild(0);
            }
            if(coinHudContent != null)
            {
                coinHudTargetScale = coinHudContent.localScale;
                coinHudContent.localScale = Vector3.zero;
            }

            // Ball.Instance 는 Ball.Awake 에서 세팅됨. DefaultExecutionOrder 덕에 우리가 나중에 실행돼서 접근 가능.
            if(Ball.Instance != null)
                Ball.Instance.gameObject.SetActive(false);
        }

        // 캐시된 CourseSelect 자식 Rigidbody 들의 kinematic 상태 일괄 설정.
        // kinematic=true : 물리 영향 X (중력/충격/충돌 무시) → 부모 DOTween 이동에도 튀지 않음
        // kinematic=false : 일반 리지드바디로 복귀 → 중력 적용
        void SetCourseSelectBodiesKinematic(bool kinematic)
        {
            if(courseSelectFrozenBodies == null) return;
            for(int i = 0; i < courseSelectFrozenBodies.Length; i++)
            {
                var rb = courseSelectFrozenBodies[i];
                if(rb == null) continue;
                if(kinematic)
                {
                    // kinematic 전환 전에 속도 제거 (kinematic 상태에선 velocity 세팅이 경고 발생).
                    // 이렇게 해야 해제 시점에 잔여 속도로 튀는 것도 같이 방지됨.
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = kinematic;
            }
        }

        IEnumerator Start()
        {
            yield return null;
            PlayGameStartIntro();
        }

        void Update()
        {
            // R 키: 현재 씬(Menu)을 다시 로드 → 인트로부터 재시작.
            // 개발/테스트 편의용. 빌드에 남아있어도 큰 영향 없지만 필요 시 #if UNITY_EDITOR 로 감쌀 수 있음.
            if(Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ── Public Entry Points (UnityEvent 연결용) ──────────────────────────
        public void OnGameStartHoleSunk()
        {
            if(phase != Phase.ShowingGameStart) return;
            phase = Phase.Transitioning;            // 추가 클릭/입력 방지
            StartCoroutine(DelayedTransition());
        }

        IEnumerator DelayedTransition()
        {
            if(postSinkDelay > 0f)
                yield return new WaitForSeconds(postSinkDelay);
            yield return TransitionToCourseSelect();
        }

        public void LoadCourse(int courseIndex, int playerCount)
        {
            if(phase == Phase.Loading) return;
            phase = Phase.Loading;

            playerCount = Mathf.Clamp(playerCount, 1, 2);
            PlayerPrefs.SetInt("PlayerCount", playerCount);
            PlayerPrefs.SetInt("CourseToPlay", courseIndex);

            string sceneName = (courseList != null && courseIndex >= 0 && courseIndex < courseList.Courses.Length)
                ? courseList.Courses[courseIndex].GameScene
                : null;

            if(string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"MenuSceneController: courseIndex {courseIndex} 에 대응하는 GameScene 을 못 찾았습니다.");
                phase = Phase.ShowingCourseSelect;
                return;
            }

            // 씬 페이드 전에 CourseSelect 라벨 축소 (GameStart 라벨 퇴장 패턴과 동일: InBack)
            if(courseSelectLabels != null)
            {
                foreach(var lbl in courseSelectLabels)
                {
                    if(lbl != null)
                        lbl.DOScale(Vector3.zero, popOutDuration * 0.6f).SetEase(Ease.InBack);
                }
            }

            StartCoroutine(LoadSceneAfterDelay(sceneName));
        }

        IEnumerator LoadSceneAfterDelay(string sceneName)
        {
            yield return new WaitForSeconds(loadDelay);
            if(ScreenFade.Instance != null)
                ScreenFade.Instance.BeginTransition(() => SceneManager.LoadScene(sceneName));
            else
                SceneManager.LoadScene(sceneName);
        }

        // ── Animations ─────────────────────────────────────────────────────────
        void PlayGameStartIntro()
        {
            phase = Phase.Intro;

            if(gameStartRoot != null)
            {
                gameStartRoot.DOMoveY(gameStartTargetPos.y, popInDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        phase = Phase.ShowingGameStart;
                        StartCoroutine(DelayedSpawn(gameStartBallSpawn));
                    });
            }
            else
            {
                phase = Phase.ShowingGameStart;
                StartCoroutine(DelayedSpawn(gameStartBallSpawn));
            }

            if(gameStartLabels != null)
            {
                for(int i = 0; i < gameStartLabels.Length; i++)
                {
                    var lbl = gameStartLabels[i];
                    if(lbl == null) continue;
                    Vector3 targetScale = gameStartLabelTargetScales != null && i < gameStartLabelTargetScales.Length
                        ? gameStartLabelTargetScales[i]
                        : Vector3.one;
                    lbl.localScale = Vector3.zero;
                    lbl.DOScale(targetScale, popInDuration)
                        .SetEase(Ease.OutBack)
                        .SetDelay(popInDuration * 0.3f);
                }
            }
        }

        IEnumerator TransitionToCourseSelect()
        {
            phase = Phase.Transitioning;

            // 공 숨김
            if(Ball.Instance != null) Ball.Instance.gameObject.SetActive(false);

            if(gameStartLabels != null)
            {
                foreach(var lbl in gameStartLabels)
                {
                    if(lbl != null)
                        lbl.DOScale(Vector3.zero, popOutDuration * 0.6f).SetEase(Ease.InBack);
                }
            }

            if(gameStartRoot != null)
                gameStartRoot.DOMoveY(gameStartTargetPos.y - popDepth, popOutDuration).SetEase(Ease.InBack);

            yield return new WaitForSeconds(popOutDuration + 0.1f);

            if(gameStartRoot != null) gameStartRoot.gameObject.SetActive(false);

            if(courseSelectRoot != null)
            {
                courseSelectRoot.gameObject.SetActive(true);
                courseSelectRoot.position = courseSelectTargetPos + Vector3.down * popDepth;
                // 등장 직전에도 kinematic 보장(Awake 이후 누가 풀었을 수도 있음). 이후 OnComplete 에서 해제.
                SetCourseSelectBodiesKinematic(true);
                courseSelectRoot.DOMoveY(courseSelectTargetPos.y, popInDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        phase = Phase.ShowingCourseSelect;
                        // 애니메이션 완료 → 글자들에 중력 적용 시작 (자연스럽게 착지/안정화)
                        SetCourseSelectBodiesKinematic(false);
                        StartCoroutine(DelayedSpawn(courseSelectBallSpawn));
                        StartCoroutine(SpawnCourseSelectCoin());
                    });
            }
            else
            {
                phase = Phase.ShowingCourseSelect;
                SetCourseSelectBodiesKinematic(false);
                StartCoroutine(DelayedSpawn(courseSelectBallSpawn));
                StartCoroutine(SpawnCourseSelectCoin());
            }

            // CourseSelect 라벨들 팝업 (GameStart 라벨 패턴과 동일: OutBack 이징 + 약간의 딜레이)
            if(courseSelectLabels != null)
            {
                for(int i = 0; i < courseSelectLabels.Length; i++)
                {
                    var lbl = courseSelectLabels[i];
                    if(lbl == null) continue;
                    Vector3 targetScale = courseSelectLabelTargetScales != null && i < courseSelectLabelTargetScales.Length
                        ? courseSelectLabelTargetScales[i]
                        : Vector3.one;
                    lbl.localScale = Vector3.zero;
                    lbl.DOScale(targetScale, popInDuration)
                        .SetEase(Ease.OutBack)
                        .SetDelay(popInDuration * 0.3f);
                }
            }

            // 코인 HUD 팝업 — 라벨보다 살짝 늦게 등장해서 시선 이동 자연스럽게
            if(coinHudContent != null)
            {
                coinHudContent.DOKill();
                coinHudContent.localScale = Vector3.zero;
                coinHudContent.DOScale(coinHudTargetScale, popInDuration)
                    .SetEase(Ease.OutBack)
                    .SetDelay(popInDuration * 0.5f);
            }
        }

        IEnumerator DelayedSpawn(Transform spawn)
        {
            if(ballSpawnDelay > 0f)
                yield return new WaitForSeconds(ballSpawnDelay);
            SpawnBallAt(spawn);
        }

        // CourseSelect 등장 완료 시점부터 courseSelectCoinSpawnDelay 후에 마커들 각각 위치에 코인 프리팹 생성.
        // 마커가 여러 개면 courseSelectCoinSpawnStagger 간격으로 순차 스폰.
        // 생성된 코인은 PlaySpawnPopIn 으로 scale 0→원본 팝인 → 이후 기존 idle (회전/보빙) 애니메이션이 자연스럽게 이어짐.
        IEnumerator SpawnCourseSelectCoin()
        {
            if(courseSelectCoinPrefab == null) yield break;
            if(courseSelectCoinSpawnPoints == null || courseSelectCoinSpawnPoints.Length == 0) yield break;

            if(courseSelectCoinSpawnDelay > 0f)
                yield return new WaitForSeconds(courseSelectCoinSpawnDelay);

            for(int i = 0; i < courseSelectCoinSpawnPoints.Length; i++)
            {
                // 유저가 빠르게 코스 선택해서 페이즈가 바뀐 경우 중단
                if(phase != Phase.ShowingCourseSelect) yield break;

                var marker = courseSelectCoinSpawnPoints[i];
                if(marker == null) continue;

                // 이미 이 세션에서 먹은 코인이면 스폰 스킵.
                // Coin.Awake 의 localPosition 계산과 일치시키기 위해 parent 기준으로 변환.
                Vector3 expectedLocalPos = marker.parent != null
                    ? marker.parent.InverseTransformPoint(marker.position)
                    : marker.position;
                string coinId = Coin.BuildId("Menu", expectedLocalPos);
                if(Coin.IsCollected(coinId)) continue;

                Coin coin = Instantiate(
                    courseSelectCoinPrefab,
                    marker.position,
                    marker.rotation,
                    marker.parent);
                coin.transform.localScale = marker.localScale;
                coin.gameObject.SetActive(true);
                coin.PlaySpawnPopIn();

                // 다음 코인 사이 stagger
                if(i < courseSelectCoinSpawnPoints.Length - 1 && courseSelectCoinSpawnStagger > 0f)
                    yield return new WaitForSeconds(courseSelectCoinSpawnStagger);
            }
        }

        // 공을 지정 위치로 옮기고 활성화. 연출 없음.
        void SpawnBallAt(Transform spawn)
        {
            if(Ball.Instance == null) return;
            Ball.Instance.gameObject.SetActive(true);

            var camCtrl = FindAnyObjectByType<CameraController>();

            if(spawn != null)
            {
                Ball.Instance.SetPosition(spawn.position);

                if(camCtrl != null)
                {
                    // 매 스폰마다(GameStart/CourseSelect) 동일하게 일정 시간 카메라 팔로우 잠금.
                    // 끝나면 followTarget 이 공 위치로 스냅되고 Cinemachine damping 이 부드럽게 이어받아 추적.
                    camCtrl.FreezeFollow(spawnCameraFollowDelay);
                }
            }

            // 스폰 직후 착지 전까지 인디케이터/조준 숨김
            Ball.Instance.StartSpawnSettle(ballSettleDuration);
        }
    }
}
