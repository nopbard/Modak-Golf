using UnityEngine;

namespace MiniGolf
{
    public class CollisionFXManager : MonoBehaviour
    {
        [System.Serializable]
        public class CollisionGroup
        {
            public string groupName;
            public Collider[] targets;
            public GameObject particlePrefab;
            public AudioClip sfx;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [SerializeField] private CollisionGroup[] groups;
        [SerializeField] private float particleLifetime = 3f;

        public static CollisionFXManager Instance;

        void Awake()
        {
            Instance = this;
        }

        // Ball의 OnCollisionEnter에서 호출
        public void NotifyCollision(Collider other, Vector3 contactPoint)
        {
            foreach(CollisionGroup group in groups)
            {
                foreach(Collider target in group.targets)
                {
                    if(target != other) continue;

                    if(group.particlePrefab != null)
                    {
                        GameObject fx = Instantiate(group.particlePrefab, contactPoint, Quaternion.identity);
                        Destroy(fx, particleLifetime);
                    }

                    if(group.sfx != null)
                        AudioSource.PlayClipAtPoint(group.sfx, contactPoint, group.volume);

                    return;
                }
            }
        }
    }
}
