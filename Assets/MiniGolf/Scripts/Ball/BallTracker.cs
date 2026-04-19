using UnityEngine;

namespace MiniGolf
{
    public class BallTracker : MonoBehaviour
    {
        [SerializeField]
        private GameObject ball;

        [SerializeField]
        private Vector3 offset;

        void Start()
        {
            transform.parent = null;
        }

        void Update()
        {
            transform.position = ball.transform.position + offset;
        }
    }
}