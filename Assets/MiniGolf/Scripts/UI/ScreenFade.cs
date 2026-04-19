using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

namespace MiniGolf
{
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField]
        private Image fadeImage;

        private Coroutine fadeCoroutine;

        public static ScreenFade Instance;

        void Awake()
        {
            if(Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // We make it DontDestroyOnLoad so this object exists outside of our scene allowing for smooth scene transitions
            DontDestroyOnLoad(gameObject);
        }

        public void BeginTransition(UnityAction onFadeOut, float fadeTime = 0.5f)
        {
            StartCoroutine(Fade());

            IEnumerator Fade()
            {
                fadeImage.gameObject.SetActive(true);

                float a = 0.0f;

                // Fade in the black screen
                while(a != 1.0f)
                {
                    a = Mathf.MoveTowards(a, 1.0f, (1 / fadeTime) * Time.deltaTime);
                    fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, a);

                    yield return null;
                }

                // Invoke the callback and wait a fraction of a second
                onFadeOut?.Invoke();
                yield return new WaitForSeconds(0.1f);

                // Fade back to clear
                while(a != 0.0f)
                {
                    a = Mathf.MoveTowards(a, 0.0f, (1 / fadeTime) * Time.deltaTime);
                    fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, a);

                    yield return null;
                }

                fadeImage.gameObject.SetActive(false);
            }
        }
    }
}