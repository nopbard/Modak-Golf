using UnityEngine;

namespace MiniGolf
{
    public class Hole : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if(other.CompareTag("Ball"))
            {
                if(GameManager.Instance.BallInHole)
                    return;

                GameManager.Instance.BallSinked();
            }
        }
    }
}