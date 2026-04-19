using UnityEngine;
using UnityEngine.UI;

namespace MiniGolf
{
    // Controls the line that comes out of the ball when aiming
    // Rotate based on aim angle and lengthen based on aim power
    public class BallAimingIndicator : MonoBehaviour
    {
        [SerializeField]
        private Ball ball;

        [SerializeField]
        private Image fill;

        private RectTransform fillRT;

        [SerializeField]
        private Gradient powerGradient;

        private float fullHeight;

        void Start()
        {
            transform.parent = null;
            fill.gameObject.SetActive(false);

            fillRT = fill.GetComponent<RectTransform>();

            fullHeight = fillRT.sizeDelta.y;
        }

        void LateUpdate()
        {
            fill.gameObject.SetActive(ball.IsAiming);

            if(!fill.isActiveAndEnabled)
                return;

            float yRot = Mathf.Atan2(ball.CurrentAimDirection.z, ball.CurrentAimDirection.x) * Mathf.Rad2Deg - 90;

            transform.position = ball.transform.position + (ball.CurrentAimDirection * 0.05f);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, 0, yRot);

            fillRT.sizeDelta = new Vector2(fillRT.sizeDelta.x, fullHeight * ball.CurrentPowerPercent);

            fill.color = powerGradient.Evaluate(ball.CurrentPowerPercent);
        }
    }
}