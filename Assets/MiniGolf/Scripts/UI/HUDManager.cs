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
            // 2P 멀티볼 대응: 특정 인스턴스의 OnHit 대신 정적 OnAnyHit 구독. 어느 공이든 치면 업데이트.
            Ball.OnAnyHit += OnAnyBallHit;
            UpdatePlayerText();
        }

        void OnAnyBallHit(Ball ball)
        {
            UpdateStrokeText();
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
            var gm = GameManager.Instance;
            if(gm == null || strokeText == null) return;

            // 2P 모드: 줄바꿈으로 1P/2P 스트로크 각각 표시.
            if(gm.PlayerCount >= 2 && gm.Strokes != null && gm.Strokes.Length >= 2 && gm.CurrentHole >= 1)
            {
                int h = gm.CurrentHole - 1;
                int s1 = (gm.Strokes[0] != null && h < gm.Strokes[0].Length) ? gm.Strokes[0][h] : 0;
                int s2 = (gm.Strokes[1] != null && h < gm.Strokes[1].Length) ? gm.Strokes[1][h] : 0;
                strokeText.text = $"Stroke 1P {s1}\nStroke 2P {s2}";
            }
            else
            {
                strokeText.text = $"Stroke {gm.CurrentStroke}";
            }
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
