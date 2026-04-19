using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MiniGolf
{
    // Our scoreboard has a number of fields for each hole
    // Each field has three properties: the hole, par, and strokes
    // This script controls the data for an individual field, aka a scoreboard hole slot
    public class ScoreboardHoleSlotUI : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text holeText;

        [SerializeField]
        private TMP_Text parText;

        [SerializeField]
        private TMP_Text strokesText;

        [SerializeField]
        private Image[] backgrounds;

        [SerializeField]
        private Color notPlayedColor;

        [SerializeField]
        private Color currentlyPlayingColor;

        [SerializeField]
        private Color playedColor;

        public void SetUI(int hole, int par, int strokes)
        {
            holeText.text = hole.ToString();
            parText.text = par.ToString();
            strokesText.text = strokes.ToString();

            if(hole > GameManager.Instance.CurrentHole)
                SetColor(playedColor);
            else if(hole == GameManager.Instance.CurrentHole)
                SetColor(currentlyPlayingColor);
            else
                SetColor(notPlayedColor);
        }

        void SetColor(Color color)
        {
            for(int i = 0; i < backgrounds.Length; i++)
            {
                backgrounds[i].color = color;
            }
        }
    }
}