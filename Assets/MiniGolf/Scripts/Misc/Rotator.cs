using UnityEngine;

namespace MiniGolf
{
    public class Rotator : MonoBehaviour
    {
        [SerializeField]
        private Vector3 axis;

        [SerializeField]
        private float speed;

        [SerializeField]
        private Space space;

        void Update()
        {
            transform.Rotate(axis * speed * Time.deltaTime, space);
        }
    }
}