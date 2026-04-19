using UnityEngine;

namespace MiniGolf
{
    public enum CourseDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    [CreateAssetMenu(fileName = "Course Data", menuName = "New Course Data")]
    public class CourseData : ScriptableObject
    {
        public string DisplayName;
        public CourseDifficulty Difficulty;
        public HoleData[] Holes;
        public string GameScene = "Game_Forest";
    }
}