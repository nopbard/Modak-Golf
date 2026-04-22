using UnityEngine;
using UnityEngine.UI;

namespace MiniGolf
{
    // 공 조준 시 공 앞쪽(발사 방향)에 화살표를 보여준다.
    //  - 머리는 공에서 먼 쪽(발사 방향 끝)을 향함, 꼬리 쪽(공 쪽)은 흰색 + 반투명
    //  - 파워가 강해질수록 머리 색이 흰 → 주황 → 빨강으로 변함
    //  - 세기에 따라 꼬리(직사각형)만 길어지고 머리는 그대로 (sprite 9-slice)
    public class BallAimingIndicator : MonoBehaviour
    {
        [SerializeField]
        private Ball ball;

        [Header("Arrow")]
        [SerializeField]
        private Sprite arrowSprite;

        [Tooltip("파워 0→1에 따른 머리 색 (0=흰, 중간=주황, 1=빨강 권장)")]
        [SerializeField]
        private Gradient tipColorGradient;

        [Tooltip("꼬리(공쪽) 색상. 알파를 낮출수록 공쪽에 가까워질수록 투명해짐")]
        [SerializeField]
        private Color tailColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("화살표 폭(UI px)")]
        [SerializeField]
        private float arrowWidth = 60f;

        [Tooltip("파워 0%에서의 화살표 길이(UI px). 머리만 겨우 보일 정도 권장")]
        [SerializeField]
        private float arrowMinLength = 100f;

        [Tooltip("파워 100%에서의 화살표 길이(UI px). 길수록 꼬리가 길어짐")]
        [SerializeField]
        private float arrowMaxLength = 240f;

        [Tooltip("공 중심에서 화살표 꼬리 끝까지 떨어진 거리(UI px)")]
        [SerializeField]
        private float arrowOffsetFromBall = 0f;

        private RectTransform container;
        private Image powerArrow;
        private RectTransform powerArrowRT;
        private UIVerticalGradient arrowGradient;

        // Awake 에서 unparent — Ball.SetActive(false) / scale 트윈 등 부모 측 생명주기 영향 차단.
        // Start 에서 하면 GameManager 가 Ball 을 비활성화한 뒤 Start 실행이 지연돼,
        // Ball 의 스케일 트윈 중간값으로 unparent 되어 world scale 이 고정되는 버그가 생김.
        void Awake()
        {
            transform.parent = null;
            container = (RectTransform)transform;
            BuildPowerArrow();
        }

        void BuildPowerArrow()
        {
            if(arrowSprite == null) return;

            GameObject go = new GameObject("PowerArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(container, false);
            powerArrow = go.GetComponent<Image>();
            powerArrow.sprite = arrowSprite;
            powerArrow.type = Image.Type.Sliced;
            powerArrow.fillCenter = true;
            powerArrow.color = Color.white;

            arrowGradient = go.AddComponent<UIVerticalGradient>();

            powerArrowRT = (RectTransform)go.transform;
            // 공 앞쪽(발사 방향 = 컨테이너 +Y)으로 꼬리→머리 배치:
            //  pivot bottom, anchored y = +offset → bottom이 공 바로 앞에 위치하고 rect는 +Y로 뻗음.
            //  sprite 머리(top)는 rect의 top = 멀리 끝 → 공 반대쪽을 향함 (= 발사 방향).
            //  버텍스 그라디언트: top=머리=파워색, bottom=꼬리=투명 흰색.
            powerArrowRT.anchorMin = new Vector2(0.5f, 0f);
            powerArrowRT.anchorMax = new Vector2(0.5f, 0f);
            powerArrowRT.pivot = new Vector2(0.5f, 0f);
            powerArrowRT.localRotation = Quaternion.identity;

            powerArrow.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            bool show = ball.IsAiming && ball.CurrentPowerPercent > 0.001f;
            if(!show)
            {
                if(powerArrow != null)
                    powerArrow.gameObject.SetActive(false);
                return;
            }

            float yRot = Mathf.Atan2(ball.CurrentAimDirection.z, ball.CurrentAimDirection.x) * Mathf.Rad2Deg - 90;
            transform.position = ball.transform.position + (ball.CurrentAimDirection * 0.05f);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 0, yRot);

            if(powerArrow == null) return;

            float power = ball.CurrentPowerPercent;
            powerArrow.gameObject.SetActive(true);

            float length = Mathf.Lerp(arrowMinLength, arrowMaxLength, power);
            powerArrowRT.anchoredPosition = new Vector2(0f, arrowOffsetFromBall);
            powerArrowRT.sizeDelta = new Vector2(arrowWidth, length);

            if(arrowGradient != null)
            {
                arrowGradient.topColor = tipColorGradient != null ? tipColorGradient.Evaluate(power) : Color.white;
                arrowGradient.bottomColor = tailColor;
                powerArrow.SetVerticesDirty();
            }
        }
    }
}
