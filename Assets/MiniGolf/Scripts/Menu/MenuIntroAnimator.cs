using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace MiniGolf
{
    public class MenuIntroAnimator : MonoBehaviour
    {
        [Tooltip("팝업 애니메이션을 적용할 오브젝트들 (순서대로 순차 실행)")]
        [SerializeField] private Transform[] targets;

        [Header("Animation")]
        [SerializeField] private float duration = 0.55f;
        [SerializeField] private float stagger  = 0.07f;
        [SerializeField] private Ease  ease     = Ease.OutBack;

        void Update()
        {
            if(Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Start()
        {
            for(int i = 0; i < targets.Length; i++)
            {
                if(targets[i] == null) continue;

                Transform t = targets[i];
                Vector3 originalScale = t.localScale;
                t.localScale = Vector3.zero;

                // 자식 포함 모든 Rigidbody를 kinematic으로 고정
                Rigidbody[] rbs = t.GetComponentsInChildren<Rigidbody>();
                bool[] wasKinematic = new bool[rbs.Length];
                for(int r = 0; r < rbs.Length; r++)
                {
                    wasKinematic[r] = rbs[r].isKinematic;
                    rbs[r].isKinematic = true;
                }

                t.DOScale(originalScale, duration)
                 .SetEase(ease)
                 .SetDelay(i * stagger)
                 .OnComplete(() =>
                 {
                     for(int r = 0; r < rbs.Length; r++)
                         rbs[r].isKinematic = wasKinematic[r];
                 });
            }
        }
    }
}
