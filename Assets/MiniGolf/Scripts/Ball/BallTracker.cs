using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MiniGolf
{
    // Ball 위치를 따라다니는 데칼(그림자) / 보조 오브젝트용.
    // Start 에서 parent=null 로 독립되기 때문에 Ball 이 SetActive(false) 돼도 자동으로 안 꺼짐.
    // → 공의 activeInHierarchy 를 폴링해 DecalProjector 만 on/off (Renderer 는 건드리지 않음 —
    //   이 컴포넌트가 BallWaitingIndicator 같은 자체 가시성 제어가 있는 것에도 붙어있기 때문).
    public class BallTracker : MonoBehaviour
    {
        [SerializeField] private GameObject ball;
        [SerializeField] private Vector3 offset;

        private DecalProjector decal;

        void Start()
        {
            transform.parent = null;
            decal = GetComponent<DecalProjector>();
        }

        void Update()
        {
            if(ball == null) return;
            bool follow = ball.activeInHierarchy;

            if(decal != null && decal.enabled != follow) decal.enabled = follow;

            if(follow)
                transform.position = ball.transform.position + offset;
        }
    }
}
