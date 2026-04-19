using UnityEngine;
using System.Collections;

namespace MiniGolf
{
    [DefaultExecutionOrder(-1)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField]
        private CourseList courseList;

        public CourseData CurrentCourse { get; private set; }

        public GameObject CurrentHoleObject { get; private set; }

        public int CurrentHole { get; private set; }
        public bool BallInHole { get; private set; }
        public int CurrentStroke
        {
            get
            {
                if(Strokes == null)
                    return -1;

                return Strokes[CurrentHole - 1];
            }
            private set
            {
                if(Strokes == null || CurrentHole > Strokes.Length)
                    return;

                Strokes[CurrentHole - 1] = value;
            }
        }
        public int CurrentPar { get; private set; }
        public int[] Strokes { get; private set; }

        public event System.Action<CourseData> OnLoadCourse;
        public event System.Action<CourseData, int> OnLoadHole;
        public event System.Action OnBallSunk;

        public static GameManager Instance;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            // We know what course to load in based on the 'CourseToPlay' player pref.
            // This is set in the Menu scene when we select a course to play.
            if(!PlayerPrefs.HasKey("CourseToPlay"))
            {
                Debug.LogError("'CourseToPlay' PlayerPref has not been found! Make sure to load the game from the Menu scene.");
                return;
            }

            LoadCourse(PlayerPrefs.GetInt("CourseToPlay"));

            Ball.Instance.OnHit += OnBallHit;
        }

        // When we load into the game scene, setup the course to play.
        // The 'courseListIndex' refers to the course in the Course List to play.
        void LoadCourse(int courseListIndex)
        {
            CurrentCourse = courseList.Courses[courseListIndex];
            Strokes = new int[CurrentCourse.Holes.Length];

            if(CurrentCourse.Holes.Length == 0)
            {
                Debug.LogError($"Course '{CurrentCourse.DisplayName}' has no holes.");
                return;
            }

            LoadHole(1);
        }

        // Called when we start the course and when we complete a hole.
        void LoadHole(int hole)
        {
            CurrentHole = hole;
            BallInHole = false;

            if(hole > CurrentCourse.Holes.Length)
            {
                Debug.LogError($"Hole {hole} doesn't exist in the course.");
                return;
            }

            if(CurrentCourse.Holes[hole - 1].HolePrefab == null)
            {
                Debug.LogError($"Hole {hole} doesn't have a prefab.");
                return;
            }

            CurrentPar = CurrentCourse.Holes[hole - 1].Par;

            if(CurrentHoleObject != null)
            {
                Destroy(CurrentHoleObject);
            }

            CurrentHoleObject = Instantiate(CurrentCourse.Holes[hole - 1].HolePrefab, Vector3.zero, Quaternion.identity);

            GameObject ballStart = GameObject.FindGameObjectWithTag("BallStart");

            if(ballStart == null)
            {
                Debug.LogError($"Hole {hole} doesn't have a ball start.");
                return;
            }

            Ball.Instance.SetPosition(ballStart.transform.position);
            ballStart.SetActive(false);

            OnLoadHole?.Invoke(CurrentCourse, hole);
        }

        // Called when the ball has been sunk in a hole.
        public void BallSinked()
        {
            BallInHole = true;
            OnBallSunk?.Invoke();

            StartCoroutine(PostBallSink());
        }

        // After sinking the ball, wait a bit before transitioning to the next hole.
        IEnumerator PostBallSink()
        {
            yield return new WaitForSeconds(2.0f);

            if(CurrentHole < CurrentCourse.Holes.Length)
                ScreenFade.Instance.BeginTransition(() => LoadHole(CurrentHole + 1));
            else
                ScoreboardUI.Instance.ToggleScoreboard(true);
        }

        // Called when the player hits the ball - increase strokes.
        void OnBallHit()
        {
            CurrentStroke++;
        }
    }
}