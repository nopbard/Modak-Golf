using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MiniGolf
{
    public class ScoreboardUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject scoreboard;

        [SerializeField]
        private ScoreboardHoleSlotUI[] holeSlots;

        public bool IsOpen => scoreboard.activeInHierarchy;

        public static ScoreboardUI Instance;

        void Awake()
        {
            Instance = this;
        }

        public void ToggleScoreboard(bool toggle)
        {
            scoreboard.SetActive(toggle);

            if(toggle)
                LoadScoreboard();
        }

        void LoadScoreboard()
        {
            CourseData course = GameManager.Instance.CurrentCourse;

            for(int i = 0; i < holeSlots.Length; i++)
            {
                if(i >= course.Holes.Length)
                {
                    holeSlots[i].gameObject.SetActive(false);
                    continue;
                }

                holeSlots[i].gameObject.SetActive(true);

                holeSlots[i].SetUI(i + 1, course.Holes[i].Par, GameManager.Instance.Strokes[i]);
            }
        }
    }
}