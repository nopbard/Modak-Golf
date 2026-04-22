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

        public event System.Action<CourseData>      OnLoadCourse;
        public event System.Action<CourseData, int> OnLoadHole;
        public event System.Action                  OnBallSunk;
        public event System.Action<int>             OnPlayerChanged;   // 넘겨주는 값: 새 플레이어 (0-based)

        public static GameManager Instance;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            #if UNITY_EDITOR
                int courseIndex  = editorDefaultCourseIndex;
                int playerCount  = editorDefaultPlayerCount;
            #else
                int courseIndex  = PlayerPrefs.HasKey("CourseToPlay")
                    ? PlayerPrefs.GetInt("CourseToPlay") : editorDefaultCourseIndex;
                int playerCount  = PlayerPrefs.GetInt("PlayerCount", 1);
            #endif

            PlayerCount = Mathf.Clamp(playerCount, 1, 2);
            LoadCourse(courseIndex);
            Ball.Instance.OnHit += OnBallHit;
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

        // 홀 프리팹 생성 + 공 스폰. 플레이어 전환 시에도 재사용.
        void SpawnHole()
        {
            if(CurrentHoleObject != null)
                Destroy(CurrentHoleObject);

            CurrentHoleObject = Instantiate(
                CurrentCourse.Holes[CurrentHole - 1].HolePrefab,
                Vector3.zero, Quaternion.identity);

            // BallStart 컴포넌트 기준으로 스폰 위치 선택 (playerIndex 매칭).
            // 컴포넌트가 붙어있지 않은 레거시 프리팹은 tag 기반 fallback.
            BallStart[] starts = CurrentHoleObject.GetComponentsInChildren<BallStart>(true);

            GameObject ballStart = null;
            if(starts.Length > 0)
            {
                // 현재 플레이어용 BallStart 찾기
                foreach(var s in starts)
                {
                    if(s.PlayerIndex == CurrentPlayer)
                    {
                        ballStart = s.gameObject;
                        break;
                    }
                }

                // 2P 모드인데 내 위치 못 찾은 경우 → 에러 + 첫 번째로 fallback (게임은 이어가게)
                if(ballStart == null)
                {
                    Debug.LogError($"Hole {CurrentHole}: BallStart with playerIndex={CurrentPlayer} 없음. 2P 스폰 지점을 배치하세요. 1P 위치로 fallback.");
                    ballStart = starts[0].gameObject;
                }
                else if(PlayerCount > 1 && starts.Length < PlayerCount)
                {
                    Debug.LogError($"Hole {CurrentHole}: BallStart 가 1개만 있음 ({starts.Length}/{PlayerCount}). 2P 용 BallStart 추가 필요.");
                }
            }
            else
            {
                // 완전 레거시: BallStart 컴포넌트 없는 프리팹 → tag 로 시도
                ballStart = GameObject.FindGameObjectWithTag("BallStart");
            }

            if(ballStart == null)
            {
                Debug.LogError($"Hole {CurrentHole} has no BallStart.");
                return;
            }

            // 공 위치 먼저 세팅 (OnLoadHole → CameraController.OnLoadHole 에서 공 위치로 카메라 워프하기 때문).
            // 이렇게 하면 홀이 지하에 있어도 카메라는 처음부터 새 홀 위치를 바라봄.
            Ball.Instance.SetPosition(ballStart.transform.position);

            // 마커 전부 숨김 (쓰지 않은 다른 플레이어의 start 도 안 보이게)
            if(starts.Length > 0)
                foreach(var s in starts) s.gameObject.SetActive(false);
            else
                ballStart.SetActive(false);

            // 두트윈 인트로 연출: 홀을 지하로 밀어내리고, 공은 잠시 숨겨둠. 코루틴이 뿅 올라오는 애니메이션 담당.
            // ballStart 는 hole 의 자식 → hole 이 올라오면 ballStart 의 world 위치도 원래대로 복귀.
            if(holeIntroPopUp)
            {
                Ball.Instance.gameObject.SetActive(false);
                float originalY = CurrentHoleObject.transform.position.y;
                CurrentHoleObject.transform.position += Vector3.down * holeIntroPopDepth;
                // ballStart Transform 을 전달 → 애니메이션 종료 후 그 시점의 world 좌표로 공 재배치.
                StartCoroutine(HoleIntroAnimation(originalY, ballStart.transform));
            }
        }

        // Menu 씬의 GameStart 팝업 패턴과 동일 느낌: OutBack 이징으로 Y 복귀 → 공 활성화 → 카메라 잠시 정지.
        IEnumerator HoleIntroAnimation(float targetY, Transform ballStartTransform)
        {
            // 코스 전체가 DOMoveY 로 지하→본래 위치
            if(CurrentHoleObject != null)
                yield return CurrentHoleObject.transform
                    .DOMoveY(targetY, holeIntroPopDuration)
                    .SetEase(Ease.OutBack)
                    .WaitForCompletion();

            if(holeIntroBallSpawnDelay > 0f)
                yield return new WaitForSeconds(holeIntroBallSpawnDelay);

            // 공 활성화 → SetPosition 으로 Rigidbody/Transform 재동기화 (비활성 상태에서 설정한 position 이
            // 재활성 시 어긋나는 경우 대비. Menu 의 SpawnBallAt 과 동일 순서).
            // 이후 Menu 코인과 동일 패턴의 scale 팝인 + 조준 블록 (settle).
            if(Ball.Instance != null)
            {
                Ball.Instance.gameObject.SetActive(true);
                if(ballStartTransform != null)
                    Ball.Instance.SetPosition(ballStartTransform.position);
                Ball.Instance.StartSpawnSettle(holeIntroBallSettleDuration);
                Ball.Instance.PlaySpawnPopIn();
            }

            // 카메라 팔로우 잠금 → 공 pop-in 이 끝나는 타이밍에 정확히 풀리도록.
            // Ball 의 SpawnPopDuration 과 일치시켜 "공이 원래 크기 되면 바로 카메라 잡기" 를 구현.
            // Ball.Instance 없거나 pop 지속시간 0이면 인스펙터의 fallback 값 사용.
            var camCtrl = FindAnyObjectByType<CameraController>();
            if(camCtrl != null)
            {
                float freezeDuration = (Ball.Instance != null && Ball.Instance.SpawnPopDuration > 0f)
                    ? Ball.Instance.SpawnPopDuration
                    : holeIntroCameraFollowDelay;
                camCtrl.FreezeFollow(freezeDuration);
            }
        }

        public void BallSinked()
        {
            BallInHole = true;
            OnBallSunk?.Invoke();
            StartCoroutine(PostBallSink());
        }

        IEnumerator PostBallSink()
        {
            yield return new WaitForSeconds(2.0f);

            // 2P이고 아직 현재 홀을 플레이하지 않은 플레이어가 있으면 전환
            if(CurrentPlayer < PlayerCount - 1)
            {
                CurrentPlayer++;
                OnPlayerChanged?.Invoke(CurrentPlayer);
                // 같은 홀을 다음 플레이어를 위해 리셋
                ScreenFade.Instance.BeginTransition(() =>
                {
                    BallInHole = false;
                    SpawnHole();
                });
            }
            else
            {
                // 모든 플레이어가 현재 홀 완료 → 다음 홀 또는 메뉴로
                CurrentPlayer = 0;
                if(CurrentHole < CurrentCourse.Holes.Length)
                    ScreenFade.Instance.BeginTransition(() => LoadHole(CurrentHole + 1));
                else
                    ScreenFade.Instance.BeginTransition(() => UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"));
            }
        }

        void OnBallHit()
        {
            CurrentStroke++;
        }
    }
}
