using UnityEngine;

namespace MiniGolf
{
    // CourseSelect 의 각 홀에 붙음. 공이 들어오면 지정 코스/플레이어 수로 씬 전환.
    // 내부적으로 MenuHole 을 사용해 공 감지.
    [RequireComponent(typeof(MenuHole))]
    public sealed class MenuCourseHole : MonoBehaviour
    {
        [Tooltip("CourseList 내 인덱스 (0부터)")]
        [SerializeField] private int courseIndex = 0;

        [Tooltip("이 홀로 진입 시 사용할 플레이어 수 (1 or 2)")]
        [Range(1, 2)] [SerializeField] private int playerCount = 1;

        [Tooltip("MenuSceneController 참조. 비우면 씬에서 FindFirstObjectByType 으로 찾음.")]
        [SerializeField] private MenuSceneController controller;

        void Reset()
        {
            if(controller == null) controller = FindAnyObjectByType<MenuSceneController>();
            var hole = GetComponent<MenuHole>();
            // 기본 바인딩은 인스펙터에서 UnityEvent 에 직접 연결하는 게 안전함.
            // Reset 시 자동 연결은 생략 (사용자가 의도적으로 설정하도록).
        }

        void Awake()
        {
            if(controller == null) controller = FindAnyObjectByType<MenuSceneController>();
        }

        // MenuHole.onBallSunk UnityEvent 에 이 메서드 연결
        public void OnBallSunk()
        {
            if(controller == null)
            {
                Debug.LogError("MenuCourseHole: MenuSceneController 참조가 없어 씬 로드 불가.", this);
                return;
            }
            controller.LoadCourse(courseIndex, playerCount);
        }
    }
}
