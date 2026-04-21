using UnityEngine;

namespace MiniGolf
{
    [RequireComponent(typeof(ParticleSystem))]
    public class BallSinkParticle : MonoBehaviour
    {
        private ParticleSystem particle;

        void Awake()
        {
            particle = GetComponent<ParticleSystem>();
        }

        void OnEnable()
        {
            // Menu 씬 같이 GameManager 가 없는 씬에서도 오류 안 나게 가드
            if(GameManager.Instance != null)
                GameManager.Instance.OnBallSunk += OnBallSunk;
        }

        void OnDisable()
        {
            if(GameManager.Instance != null)
                GameManager.Instance.OnBallSunk -= OnBallSunk;
        }

        void OnBallSunk()
        {
            particle.Play();
        }
    }
}
