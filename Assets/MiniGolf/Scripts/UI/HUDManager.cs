using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace MiniGolf
{
    public class HUDManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text holeText;
        [SerializeField] private TMP_Text strokeText;
        [SerializeField] private TMP_Text parText;
        [SerializeField] private TMP_Text playerText;   // "1P 차례" / "2P 차례"
        [Tooltip("코인 카운트 표시용. 비워두면 별도 UI(CoinCountUI)에서 표시.")]
        [SerializeField] private TMP_Text coinText;

        private bool goingToMenu;

        void Awake()
        {
            GameManager.Instance.OnLoadHole    += OnLoadHole;
            GameManager.Instance.OnPlayerChanged += OnPlayerChanged;
            CoinCounter.OnCountChanged += OnCoinChanged;
            OnCoinChanged(CoinCounter.Total);
        }

        void OnDestroy()
        {
            CoinCounter.OnCountChanged -= OnCoinChanged;
        }

        void OnCoinChanged(int count)
        {
            if(coinText != null)
                coinText.text = $"x {count}";
        }

        void Start()
        {
            Ball.Instance.OnHit += UpdateStrokeText;
            UpdatePlayerText();
        }

        void OnLoadHole(CourseData courseData, int hole)
        {
            holeText.text = $"Hole {hole}";
            parText.text  = $"Par {courseData.Holes[hole - 1].Par}";
            UpdateStrokeText();
            UpdatePlayerText();
        }

        void OnPlayerChanged(int player)
        {
            UpdatePlayerText();
            UpdateStrokeText();
        }

        void UpdateStrokeText()
        {
            strokeText.text = $"Stroke {GameManager.Instance.CurrentStroke}";
        }

        void UpdatePlayerText()
        {
            if(playerText == null) return;
            if(GameManager.Instance.PlayerCount < 2)
            {
                playerText.gameObject.SetActive(false);
                return;
            }
            playerText.gameObject.SetActive(true);
            playerText.text = $"{GameManager.Instance.CurrentPlayer + 1}P 차례";
        }

        public void OnResetCameraButton()
        {
            FindAnyObjectByType<CameraController>().SetPosition(Ball.Instance.transform.position);
        }

        public void OnMenuButton()
        {
            if(goingToMenu) return;
            goingToMenu = true;
            ScreenFade.Instance.BeginTransition(() => SceneManager.LoadScene("Menu"));
        }
    }
}
