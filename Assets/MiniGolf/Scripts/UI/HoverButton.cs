using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MiniGolf
{
    /// <summary>
    /// General purpose script used to add hover/click effects to a button.
    /// </summary>
    public class HoverButton : EventTrigger
    {
        [SerializeField]
        private float hoverScaleChange = 0.02f;

        private float targetScale = 1.0f;

        public static event System.Action OnEnter;
        public static event System.Action OnExit;
        public static event System.Action OnDown;
        public static event System.Action OnUp;

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 20);
        }

        public override void OnPointerEnter(PointerEventData data)
        {
            targetScale = 1.0f + hoverScaleChange;
            OnEnter?.Invoke();
        }

        public override void OnPointerExit(PointerEventData data)
        {
            targetScale = 1.0f;
            OnExit?.Invoke();
        }

        public override void OnPointerDown(PointerEventData data)
        {
            targetScale = 1.0f;
            OnDown?.Invoke();
        }

        public override void OnPointerUp(PointerEventData data)
        {
            if(!data.hovered.Contains(gameObject))
                return;

            targetScale = 1.0f + hoverScaleChange;
            OnUp?.Invoke();
        }

        void OnDisable()
        {
            targetScale = 1.0f;
            transform.localScale = Vector3.one;
        }
    }
}