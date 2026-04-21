using UnityEngine;

namespace MiniGolf
{
    // 스코어보드 제거됨. 참조 호환성 유지용 스텁.
    public class ScoreboardUI : MonoBehaviour
    {
        public static ScoreboardUI Instance;
        public bool IsOpen => false;

        void Awake() { Instance = this; }
        public void ToggleScoreboard(bool toggle) { }
    }
}
