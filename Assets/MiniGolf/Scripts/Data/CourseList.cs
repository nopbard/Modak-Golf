using UnityEngine;

namespace MiniGolf
{
    [CreateAssetMenu(fileName = "Course List", menuName = "New Course List")]
    public class CourseList : ScriptableObject
    {
        public CourseData[] Courses;
    }
}