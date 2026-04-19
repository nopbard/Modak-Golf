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
            GameManager.Instance.OnBallSunk += OnBallSunk;
        }

        void OnDisable()
        {
            GameManager.Instance.OnBallSunk -= OnBallSunk;
        }

        void OnBallSunk()
        {
            particle.Play();
        }
    }
}
