using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace MiniGolf
{
    public class HUDManager : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text holeText;

        [SerializeField]
        private TMP_Text strokeText;

        [SerializeField]
        private TMP_Text parText;

        [SerializeField]
        private ScoreboardUI scoreboard;

        private bool goingToMenu;

        void Awake()
        {
            GameManager.Instance.OnLoadHole += OnLoadHole;
        }

        void Start()
        {
            Ball.Instance.OnHit += UpdateStrokeText;

            scoreboard.ToggleScoreboard(false);
        }

        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Tab))
                scoreboard.ToggleScoreboard(true);
            else if(Input.GetKeyUp(KeyCode.Tab))
                scoreboard.ToggleScoreboard(false);
        }

        void OnLoadHole(CourseData courseData, int hole)
        {
            holeText.text = $"Hole {hole}";
            parText.text = $"Par {courseData.Holes[hole - 1].Par}";
            UpdateStrokeText();
        }

        void UpdateStrokeText()
        {
            strokeText.text = $"Stroke {GameManager.Instance.CurrentStroke}";
        }

        public void OnResetCameraButton()
        {
            FindAnyObjectByType<CameraController>().SetPosition(Ball.Instance.transform.position);
        }

        public void OnScoreboardButton()
        {
            if(scoreboard.IsOpen)
                scoreboard.ToggleScoreboard(false);
            else
                scoreboard.ToggleScoreboard(true);
        }

        public void OnMenuButton()
        {
            if(goingToMenu)
                return;

            goingToMenu = true;
            ScreenFade.Instance.BeginTransition(() => SceneManager.LoadScene("Menu"));
        }
    }
}