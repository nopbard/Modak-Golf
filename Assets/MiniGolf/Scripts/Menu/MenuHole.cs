using UnityEngine;
using UnityEngine.Events;

namespace MiniGolf
{
    // Menu 씬용 홀 트리거. GameManager 의존 없이 공 감지만 함.
    // Trigger Collider 와 함께 사용하며, 공이 들어오면 SFX + 파티클 + onBallSunk UnityEvent 호출 (1회).
    [RequireComponent(typeof(Collider))]
    public sealed class MenuHole : MonoBehaviour
    {
        [Header("Feedback")]
        [SerializeField] private AudioClip sinkSFX;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
        [Tooltip("공이 들어왔을 때 Play() 시킬 파티클")]
        [SerializeField] private ParticleSystem sinkParticle;
        [Tooltip("공이 들어왔을 때 Sink() 호출해 뿅 사라지게 할 Flag")]
        [SerializeField] private Flag flag;

        [Header("Event")]
        [SerializeField] private UnityEvent onBallSunk;

        private bool triggered;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if(col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if(triggered) return;
            if(!other.CompareTag("Ball")) return;
            triggered = true;

            if(sinkSFX != null)
                AudioSource.PlayClipAtPoint(sinkSFX, transform.position, sfxVolume);
            if(sinkParticle != null)
                sinkParticle.Play(true);
            if(flag != null)
                flag.Sink();

            // 공 즉시 숨김 → 그림자 데칼 / WaitingIndicator 같은 자식도 함께 꺼져 바닥에 잔상 안 남음
            if(Ball.Instance != null)
                Ball.Instance.gameObject.SetActive(false);

            onBallSunk?.Invoke();
        }

        public void ResetTrigger() => triggered = false;
    }
}
