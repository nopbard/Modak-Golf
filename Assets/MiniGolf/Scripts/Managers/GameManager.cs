using UnityEngine;
using System.Collections;

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

            GameObject ballStart = GameObject.FindGameObjectWithTag("BallStart");
            if(ballStart == null)
            {
                Debug.LogError($"Hole {CurrentHole} has no BallStart.");
                return;
            }

            Ball.Instance.SetPosition(ballStart.transform.position);
            ballStart.SetActive(false);
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
