using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MiniGolf
{
    // Manages an individual course in the course list screen on the menu
    public class CourseSlotUI : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text courseNameText;

        [SerializeField]
        private TMP_Text holeCountText;

        [SerializeField]
        private Image difficultyImage;

        [SerializeField]
        private Sprite[] difficultyIcons;

        [SerializeField]
        private Color[] difficultyIconColors;

        [SerializeField]
        private Color[] difficultyBackgroundColor;

        private Image image;

        void Awake()
        {
            image = GetComponent<Image>();
        }

        public void Initialize(CourseData course)
        {
            courseNameText.text = course.DisplayName;
            holeCountText.text = $"{course.Holes.Length} Holes";

            switch(course.Difficulty)
            {
                case CourseDifficulty.Easy:
                    difficultyImage.sprite = difficultyIcons[0];
                    difficultyImage.color = difficultyIconColors[0];
                    image.color = difficultyBackgroundColor[0];
                    break;
                case CourseDifficulty.Medium:
                    difficultyImage.sprite = difficultyIcons[1];
                    difficultyImage.color = difficultyIconColors[1];
                    image.color = difficultyBackgroundColor[1];
                    break;
                case CourseDifficulty.Hard:
                    difficultyImage.sprite = difficultyIcons[2];
                    difficultyImage.color = difficultyIconColors[2];
                    image.color = difficultyBackgroundColor[2];
                    break;
            }
        }
    }
}