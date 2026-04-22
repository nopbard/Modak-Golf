using UnityEngine;
using TMPro;
using DG.Tweening;

namespace MiniGolf
{
    // TMP_Text 에 붙이면 CoinCounter.OnCountChanged 에 맞춰 자동 갱신.
    // 프로토타입용이라 간단.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CoinCountUI : MonoBehaviour
    {
        [Tooltip("표시 포맷. {0} 자리에 숫자 들어감")]
        [SerializeField] private string format = "{0}";

        [Tooltip("증가 시 살짝 punch 애니메이션")]
        [SerializeField] private bool punchOnChange = true;

        TMP_Text text;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            Refresh(CoinCounter.Total);
        }

        void OnEnable()
        {
            CoinCounter.OnCountChanged += Refresh;
            Refresh(CoinCounter.Total);
        }

        void OnDisable()
        {
            CoinCounter.OnCountChanged -= Refresh;
        }

        void Refresh(int count)
        {
            if(text == null) text = GetComponent<TMP_Text>();
            text.text = string.Format(format, count);
            if(punchOnChange)
            {
                transform.DOKill();
                transform.localScale = Vector3.one;
                transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.8f);
            }
        }
    }
}
