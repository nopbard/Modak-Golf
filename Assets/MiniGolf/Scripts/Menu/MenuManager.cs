using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MiniGolf
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject playerSelectScreen;
        [SerializeField] private GameObject coursesScreen;

        [SerializeField] private CourseSlotUI[] courseSlots;
        [SerializeField] private CourseList courseList;

        private bool loadingCourse;

        void Start()
        {
            SetScreen(mainMenuScreen);
        }

        // ── 화면 전환 ────────────────────────────────────────────────────────
        void SetScreen(GameObject screen)
        {
            mainMenuScreen.SetActive(false);
            playerSelectScreen.SetActive(false);
            coursesScreen.SetActive(false);

            screen.SetActive(true);

            if(screen == coursesScreen)
                UpdateCoursesScreen();
        }

        // ── 버튼 콜백 ────────────────────────────────────────────────────────

        // 메인 메뉴 "게임 시작" 버튼
        public void OnStartButton()
        {
            SetScreen(playerSelectScreen);
        }

        // 1P / 2P 선택 버튼. Inspector에서 각각 OnPlayerSelect(1), OnPlayerSelect(2) 연결
        public void OnPlayerSelect(int playerCount)
        {
            PlayerPrefs.SetInt("PlayerCount", playerCount);
            SetScreen(coursesScreen);
        }

        public void OnBackButton()
        {
            // 현재 어느 화면이냐에 따라 한 단계 뒤로
            if(coursesScreen.activeSelf)
                SetScreen(playerSelectScreen);
            else
                SetScreen(mainMenuScreen);
        }

        // ── 코스 선택 화면 ───────────────────────────────────────────────────
        void UpdateCoursesScreen()
        {
            for(int i = 0; i < courseSlots.Length; i++)
            {
                if(i >= courseList.Courses.Length)
                {
                    courseSlots[i].gameObject.SetActive(false);
                    continue;
                }

                courseSlots[i].gameObject.SetActive(true);
                courseSlots[i].Initialize(courseList.Courses[i]);

                Button courseButton = courseSlots[i].GetComponent<Button>();
                courseButton.onClick.RemoveAllListeners();
                int courseIndex = i;
                courseButton.onClick.AddListener(() => PlayCourse(courseIndex));
            }
        }

        void PlayCourse(int courseListIndex)
        {
            if(loadingCourse) return;

            PlayerPrefs.SetInt("CourseToPlay", courseListIndex);
            loadingCourse = true;

            string sceneToLoad = courseList.Courses[courseListIndex].GameScene;
            ScreenFade.Instance.BeginTransition(() => SceneManager.LoadScene(sceneToLoad));
        }
    }
}
