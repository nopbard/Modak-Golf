using UnityEngine;
using System.Collections;
using DG.Tweening;

namespace MiniGolf
{
    [DefaultExecutionOrder(-1)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private CourseList courseList;

        [Tooltip("Menu를 거치지 않고 Game 씬에서 바로 Play할 때 사용할 코스 인덱스")]
        [SerializeField] private int editorDefaultCourseIndex = 0;

        [Tooltip("에디터 직접 테스트용 플레이어 수 (1 또는 2)")]
        [SerializeField] private int editorDefaultPlayerCount = 1;

        [Header("2P Multi-Ball")]
        [Tooltip("2P 모드에서 2P 공을 생성할 때 사용할 Ball 프리펩. 씬의 Ball 인스턴스를 1P 공으로 사용.")]
        [SerializeField] private GameObject ballPrefab;

        [Tooltip("2P 공의 Model 메쉬에 적용할 머터리얼 (주황색)")]
        [SerializeField] private Material ball2PMaterial;

        [Tooltip("공이 멈춘 뒤 턴 전환까지 대기 시간 (초)")]
        [SerializeField] private float turnSwitchDelay = 0.6f;

        [Header("Hole Intro Animation (Menu GameStart 와 동일한 느낌)")]
        [Tooltip("새 홀 로드 시 코스가 지하에서 뿅 올라오는 두트윈 연출 사용 여부")]
        [SerializeField] private bool holeIntroPopUp = true;
        [Tooltip("지하에서 뿅 올라오는 Y 오프셋 (m)")]
        [SerializeField] private float holeIntroPopDepth = 3f;
        [Tooltip("등장 시간 (초)")]
        [SerializeField] private float holeIntroPopDuration = 0.7f;
        [Tooltip("코스 등장 완료 후 공이 팝인 되기까지의 대기 시간 (초). Menu 씬의 동전 스폰 대기와 유사한 breathing room.")]
        [SerializeField] private float holeIntroBallSpawnDelay = 0.3f;
        [Tooltip("공이 나타난 뒤 카메라 팔로우 시작까지의 fallback 대기 시간. Ball.PlaySpawnPopIn 이 유효하면 그 지속시간(= Ball 의 Spawn Pop Duration)이 우선 사용됨. 공 pop-in 이 끝나는 순간 정확히 카메라가 따라오기 시작하도록.")]
        [SerializeField] private float holeIntroCameraFollowDelay = 1.5f;
        [Tooltip("공 스폰 후 조준 입력이 차단되는 시간 (공 정착 연출)")]
        [SerializeField] private float holeIntroBallSettleDuration = 1.0f;

        public CourseData CurrentCourse  { get; private set; }
        public GameObject CurrentHoleObject { get; private set; }

        public int  PlayerCount    { get; private set; }
        public int  CurrentPlayer  { get; private set; }   // 0-based (0=1P, 1=2P)
        public int  CurrentHole    { get; private set; }
        public bool BallInHole     { get; private set; }

        // Strokes[player][hole-1]
        public int[][] Strokes { get; private set; }

        public int CurrentStroke
        {
            get => Strokes != null ? Strokes[CurrentPlayer][CurrentHole - 1] : -1;
            private set { if(Strokes != null) Strokes[CurrentPlayer][CurrentHole - 1] = value; }
        }

        public int CurrentPar { get; private set; }

        // 2P: 각 플레이어가 이번 홀에서 이미 홀인 했는지. SpawnHole 에서 false 로 초기화.
        private bool[] sunkPerPlayer;
        // 2P: 멀티볼 배열. balls[0]=1P, balls[1]=2P. 씬 Ball 은 1P, 2P 는 런타임 Instantiate.
        private Ball[] balls;
        // 턴 전환 코루틴 중복 실행 방지. 외부에서 입력/인디케이터 차단용으로도 사용.
        private bool turnSwitching;

        // 턴 전환 중(공이 멈추고 상대에게 넘어가는 대기 시간) 여부. 이 동안은 입력/대기 인디케이터 둘 다 block.
        // 더블샷(공 멈추자마자 바로 또 때리기) 방지용.
        public bool IsTurnSwitching => turnSwitching;

        public event System.Action<CourseData>      OnLoadCourse;
        public event System.Action<CourseData, int> OnLoadHole;
        public event System.Action                  OnBallSunk;
        public event System.Action<int>             OnPlayerChanged;   // 넘겨주는 값: 새 플레이어 (0-based)

        public static GameManager Instance;

        // Menu 에서 코스 선택 후 씬을 로드한 경우 true. GameManager.Start 가 읽고 바로 false 로 리셋.
        // 에디터에서 Game 씬을 직접 Play 할 때는 false 로 유지 → PlayerPrefs 의 stale 값 무시하고
        // 인스펙터의 editorDefault* 값 사용. 빌드/정상 플레이에는 영향 없음.
        public static bool CameFromMenu;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            int courseIndex;
            int playerCount;
            if(CameFromMenu)
            {
                // Menu 경유로 진입 → PlayerPrefs 우선. 없으면 editor default fallback.
                courseIndex = PlayerPrefs.HasKey("CourseToPlay")
                    ? PlayerPrefs.GetInt("CourseToPlay") : editorDefaultCourseIndex;
                playerCount = PlayerPrefs.HasKey("PlayerCount")
                    ? PlayerPrefs.GetInt("PlayerCount") : editorDefaultPlayerCount;
                CameFromMenu = false;
            }
            else
            {
                // 씬 직접 Play (에디터 테스트) → 인스펙터의 editorDefault 값만 사용.
                // 이전 Menu 플레이가 남긴 PlayerPrefs 의 stale 값은 무시.
                courseIndex = editorDefaultCourseIndex;
                playerCount = editorDefaultPlayerCount;
            }

            PlayerCount = Mathf.Clamp(playerCount, 1, 2);

            // 2P 멀티볼 설정: 씬에 있는 Ball 을 1P 로 쓰고, 필요하면 2P Ball 을 프리펩에서 생성.
            SetupBalls();

            LoadCourse(courseIndex);

            // OnAnyHit: 어느 공이든 치면 호출. 현재 턴 플레이어만 stroke 카운트.
            Ball.OnAnyHit += OnAnyBallHit;
            // OnAnyBeginWaiting: 어느 공이든 멈추면 호출. 현재 턴 공이 멈추면 턴 전환 로직.
            Ball.OnAnyBeginWaiting += OnAnyBallBeginWaiting;
        }

        void OnDestroy()
        {
            Ball.OnAnyHit -= OnAnyBallHit;
            Ball.OnAnyBeginWaiting -= OnAnyBallBeginWaiting;
        }

        // 2P 멀티볼 초기화. 씬의 기존 Ball 을 1P 로 배정하고, 2P 모드면 Ball 프리펩 Instantiate.
        void SetupBalls()
        {
            balls = new Ball[PlayerCount];

            // 1P: 씬에 존재하는 Ball (Ball.All[0]).
            if(Ball.All.Count > 0)
            {
                balls[0] = Ball.All[0];
                balls[0].PlayerIndex = 0;
            }
            else
            {
                Debug.LogError("GameManager: 씬에 Ball 이 없습니다.");
                return;
            }

            // 2P 모드: 프리펩으로 2P 공 생성 + 주황 머터리얼.
            if(PlayerCount >= 2)
            {
                if(ballPrefab == null)
                {
                    Debug.LogError("GameManager: 2P 모드지만 ballPrefab 이 비어있습니다. Inspector 에서 Ball.prefab 할당 필요.");
                    return;
                }
                GameObject ball2PGO = Instantiate(ballPrefab);
                ball2PGO.name = "Ball_2P";
                balls[1] = ball2PGO.GetComponent<Ball>();
                balls[1].PlayerIndex = 1;
                if(ball2PMaterial != null)
                    balls[1].SetBodyMaterial(ball2PMaterial);
            }

            sunkPerPlayer = new bool[PlayerCount];

            // 1P 를 기본 활성 공으로.
            Ball.SetActive(balls[0]);
        }

        void LoadCourse(int courseListIndex)
        {
            CurrentCourse = courseList.Courses[courseListIndex];

            // Strokes[플레이어 수][홀 수] 초기화
            Strokes = new int[PlayerCount][];
            for(int p = 0; p < PlayerCount; p++)
                Strokes[p] = new int[CurrentCourse.Holes.Length];

            if(CurrentCourse.Holes.Length == 0)
            {
                Debug.LogError($"Course '{CurrentCourse.DisplayName}' has no holes.");
                return;
            }

            CurrentPlayer = 0;
            OnLoadCourse?.Invoke(CurrentCourse);
            LoadHole(1);
        }

        void LoadHole(int hole)
        {
            CurrentHole = hole;
            BallInHole  = false;

            if(hole > CurrentCourse.Holes.Length)
            {
                Debug.LogError($"Hole {hole} doesn't exist.");
                return;
            }

            if(CurrentCourse.Holes[hole - 1].HolePrefab == null)
            {
                Debug.LogError($"Hole {hole} has no prefab.");
                return;
            }

            CurrentPar = CurrentCourse.Holes[hole - 1].Par;

            SpawnHole();
            OnLoadHole?.Invoke(CurrentCourse, hole);
        }

        // 홀 프리팹 생성 + 공 스폰. 2P 는 두 공을 동시에 각자의 BallStart 위치에 배치.
        void SpawnHole()
        {
            if(CurrentHoleObject != null)
                Destroy(CurrentHoleObject);

            CurrentHoleObject = Instantiate(
                CurrentCourse.Holes[CurrentHole - 1].HolePrefab,
                Vector3.zero, Quaternion.identity);

            // 이번 홀의 플레이어별 sunk 상태 초기화.
            if(sunkPerPlayer != null)
            {
                for(int i = 0; i < sunkPerPlayer.Length; i++) sunkPerPlayer[i] = false;
            }

            // BallStart 컴포넌트 기준으로 각 플레이어별 스폰 위치 선택.
            BallStart[] starts = CurrentHoleObject.GetComponentsInChildren<BallStart>(true);
            Transform[] spawnTransforms = new Transform[PlayerCount];

            if(starts.Length > 0)
            {
                // 각 플레이어용 BallStart 매칭
                for(int p = 0; p < PlayerCount; p++)
                {
                    foreach(var s in starts)
                    {
                        if(s.PlayerIndex == p)
                        {
                            spawnTransforms[p] = s.transform;
                            break;
                        }
                    }
                    if(spawnTransforms[p] == null)
                    {
                        int fallbackP = (p == 0 ? 1 : 0);
                        Debug.LogError($"Hole {CurrentHole}: BallStart with playerIndex={p} 없음. {fallbackP}P 위치로 fallback.");
                        spawnTransforms[p] = starts[0].transform;
                    }
                }
            }
            else
            {
                // 레거시: tag 기반 fallback. 한 위치만 있음 → 모든 플레이어 같은 위치.
                GameObject tagStart = GameObject.FindGameObjectWithTag("BallStart");
                if(tagStart == null)
                {
                    Debug.LogError($"Hole {CurrentHole} has no BallStart.");
                    return;
                }
                for(int p = 0; p < PlayerCount; p++) spawnTransforms[p] = tagStart.transform;
            }

            // 공 위치 먼저 세팅 (카메라 워프 대비). initialSpawn 도 같이 기록해서 OOB 시 BallStart 로 복귀 가능.
            for(int p = 0; p < PlayerCount; p++)
            {
                if(balls[p] != null && spawnTransforms[p] != null)
                {
                    balls[p].SetInitialSpawn(spawnTransforms[p].position);
                    balls[p].SetPosition(spawnTransforms[p].position);
                }
            }

            // BallStart 마커는 전부 숨김.
            foreach(var s in starts) s.gameObject.SetActive(false);

            // 턴: 매 홀마다 1P 가 먼저.
            if(CurrentPlayer != 0)
            {
                CurrentPlayer = 0;
                OnPlayerChanged?.Invoke(CurrentPlayer);
            }
            Ball.SetActive(balls[0]);

            // 인트로 연출: 홀을 지하로, 공 숨김 → 팝인.
            if(holeIntroPopUp)
            {
                for(int p = 0; p < PlayerCount; p++)
                {
                    if(balls[p] != null) balls[p].gameObject.SetActive(false);
                }
                float originalY = CurrentHoleObject.transform.position.y;
                CurrentHoleObject.transform.position += Vector3.down * holeIntroPopDepth;
                StartCoroutine(HoleIntroAnimation(originalY, spawnTransforms));
            }
        }

        // 홀 인트로: 코스가 지하에서 올라온 뒤 양쪽 공 모두 팝인. 활성 공만 조준 settle, 카메라 팔로우는 활성 공 기준.
        IEnumerator HoleIntroAnimation(float targetY, Transform[] ballStartTransforms)
        {
            if(CurrentHoleObject != null)
                yield return CurrentHoleObject.transform
                    .DOMoveY(targetY, holeIntroPopDuration)
                    .SetEase(Ease.OutBack)
                    .WaitForCompletion();

            if(holeIntroBallSpawnDelay > 0f)
                yield return new WaitForSeconds(holeIntroBallSpawnDelay);

            // 모든 플레이어의 공 활성화 + 팝인 애니메이션.
            for(int p = 0; p < PlayerCount; p++)
            {
                if(balls[p] == null) continue;
                balls[p].gameObject.SetActive(true);
                if(ballStartTransforms != null && p < ballStartTransforms.Length && ballStartTransforms[p] != null)
                {
                    balls[p].SetInitialSpawn(ballStartTransforms[p].position);
                    balls[p].SetPosition(ballStartTransforms[p].position);
                }
                balls[p].StartSpawnSettle(holeIntroBallSettleDuration);
                balls[p].PlaySpawnPopIn();
            }
            // 활성 공 재지정 (팝인 직후 kinematic 상태 갱신).
            Ball.SetActive(balls[CurrentPlayer]);

            var camCtrl = FindAnyObjectByType<CameraController>();
            if(camCtrl != null)
            {
                float freezeDuration = (Ball.Instance != null && Ball.Instance.SpawnPopDuration > 0f)
                    ? Ball.Instance.SpawnPopDuration
                    : holeIntroCameraFollowDelay;
                camCtrl.FreezeFollow(freezeDuration);
            }
        }

        // 특정 공이 홀에 들어갔을 때 Hole.cs 가 호출.
        public void BallSinked(Ball ball)
        {
            if(ball == null) return;
            int p = ball.PlayerIndex;
            if(p < 0 || p >= PlayerCount) return;
            if(sunkPerPlayer != null && sunkPerPlayer[p]) return;  // 중복 방지

            if(sunkPerPlayer != null) sunkPerPlayer[p] = true;
            ball.MarkSunk();
            OnBallSunk?.Invoke();

            // 모든 플레이어가 홀인 → 다음 홀. 아니면 남은 플레이어로 턴 전환.
            if(AllPlayersSunk())
            {
                BallInHole = true;
                StartCoroutine(AdvanceAfterAllSunk());
            }
            else
            {
                // 남은 플레이어로 턴 전환 (공 멈추는 이벤트 기다리지 않고 바로).
                StartCoroutine(SwitchTurnCoroutine(fromSink: true));
            }
        }

        bool AllPlayersSunk()
        {
            if(sunkPerPlayer == null) return false;
            for(int i = 0; i < sunkPerPlayer.Length; i++)
                if(!sunkPerPlayer[i]) return false;
            return true;
        }

        IEnumerator AdvanceAfterAllSunk()
        {
            yield return new WaitForSeconds(2.0f);
            if(CurrentHole < CurrentCourse.Holes.Length)
                ScreenFade.Instance.BeginTransition(() => {
                    BallInHole = false;
                    LoadHole(CurrentHole + 1);
                });
            else
                ScreenFade.Instance.BeginTransition(() => UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"));
        }

        // 어느 공이든 멈추면 호출. 현재 턴 플레이어 공이면 상대 턴으로 전환.
        void OnAnyBallBeginWaiting(Ball ball)
        {
            if(balls == null || PlayerCount <= 1) return;
            if(ball.PlayerIndex != CurrentPlayer) return;
            if(sunkPerPlayer != null && sunkPerPlayer[CurrentPlayer]) return;
            if(BallInHole) return;
            if(turnSwitching) return;

            StartCoroutine(SwitchTurnCoroutine(fromSink: false));
        }

        IEnumerator SwitchTurnCoroutine(bool fromSink)
        {
            turnSwitching = true;
            // 시각적 버퍼 (홀인 이펙트 또는 공 멈춤 직후 짧게 대기).
            yield return new WaitForSeconds(fromSink ? 1.5f : turnSwitchDelay);

            // 다음 남은 (아직 홀인 하지 않은) 플레이어 찾기.
            int next = CurrentPlayer;
            for(int offset = 1; offset <= PlayerCount; offset++)
            {
                int candidate = (CurrentPlayer + offset) % PlayerCount;
                if(sunkPerPlayer == null || !sunkPerPlayer[candidate])
                {
                    next = candidate;
                    break;
                }
            }

            if(next != CurrentPlayer)
            {
                CurrentPlayer = next;
                Ball.SetActive(balls[CurrentPlayer]);
                OnPlayerChanged?.Invoke(CurrentPlayer);
            }
            turnSwitching = false;
        }

        // 어느 공이든 치면 호출. 현재 턴 플레이어만 stroke 카운트 증가.
        void OnAnyBallHit(Ball ball)
        {
            if(ball.PlayerIndex != CurrentPlayer) return;
            CurrentStroke++;
        }
    }
}
