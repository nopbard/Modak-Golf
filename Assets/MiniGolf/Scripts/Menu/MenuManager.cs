using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MiniGolf
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject mainMenuScreen;

        [SerializeField]
        private GameObject coursesScreen;

        [SerializeField]
        private CourseSlotUI[] courseSlots;

        [SerializeField]
        private CourseList courseList;

        private bool loadingCourse = false;

        void Start()
        {
            SetScreen(mainMenuScreen);
        }

        void SetScreen(GameObject screen)
        {
            mainMenuScreen.SetActive(false);
            coursesScreen.SetActive(false);

            screen.SetActive(true);

            if(screen == coursesScreen)
                UpdateCoursesScreen();
        }

        public void OnCoursesButton()
        {
            SetScreen(coursesScreen);
        }

        public void OnBackButton()
        {
            SetScreen(mainMenuScreen);
        }

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

        // When we select a course to play, save it's index number (relative to the array on the Course List file)
        // We save it to PlayerPrefs as we can easily access it in a new scene
        // This could be done by having a DontDestroyOnLoad object and keeping the data there, but this is simplist method
        void PlayCourse(int courseListIndex)
        {
            if(loadingCourse)
                return;

            PlayerPrefs.SetInt("CourseToPlay", courseListIndex);
            loadingCourse = true;

            string sceneToLoad = courseList.Courses[courseListIndex].GameScene;
            ScreenFade.Instance.BeginTransition(() => SceneManager.LoadScene(sceneToLoad));
        }
    }
}