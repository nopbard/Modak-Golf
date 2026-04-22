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

        [Header("Ground Snap (BallShadow 처럼 첫 번째 표면에만 투영하고 싶을 때)")]
        [Tooltip("활성화 시 공 아래로 raycast 해서 첫 hit 표면에 데칼을 스냅. 2층 코스에서도 상단 표면에만 투영됨. 공이 공중에 있어도 지면에 그림자 표시 (높이 감각 유지).")]
        [SerializeField] private bool snapToGround = false;
        [Tooltip("raycast 최대 거리 (m)")]
        [SerializeField] private float snapMaxDistance = 30f;
        [Tooltip("raycast 대상 레이어 (Ball 본체 레이어는 자동 제외)")]
        [SerializeField] private LayerMask snapLayers = ~0;
        [Tooltip("표면 hit 지점에서 법선 방향으로 얼마나 띄울지. Z-fighting 방지용 안전 여유 (m). 너무 크면 그림자가 surface 위에서 뜨는 것처럼 보일 수 있음.")]
        [SerializeField] private float snapSurfaceClearance = 0.02f;

        private DecalProjector decal;

        // Awake 에서 unparent — BallAimingIndicator 와 동일한 이유.
        void Awake()
        {
            transform.parent = null;
            decal = GetComponent<DecalProjector>();
        }

        void Update()
        {
            if(ball == null) return;
            bool follow = ball.activeInHierarchy;

            if(decal != null && decal.enabled != follow) decal.enabled = follow;

            if(!follow) return;

            if(snapToGround)
            {
                int effectiveMask = snapLayers.value & ~(1 << ball.layer);
                // 공 중심에서 아래로 raycast. 공 위로 offset 을 주면 공이 2층 오버행 아래에 있을 때
                // ray 시작점이 2층 윗면으로 올라가서 엉뚱한 표면에 스냅됨. 공 collider 는 레이어로 제외됨.
                Ray ray = new Ray(ball.transform.position, Vector3.down);
                if(Physics.Raycast(ray, out RaycastHit hit, snapMaxDistance, effectiveMask, QueryTriggerInteraction.Ignore))
                {
                    // surface 바로 위에 살짝 띄워서 배치 (Z-fighting 방지)
                    transform.position = hit.point + hit.normal * snapSurfaceClearance;
                    // 법선 방향 기준 아래로 투영되도록 회전 (경사 지면 대응)
                    transform.rotation = Quaternion.LookRotation(-hit.normal);
                    return;
                }
                // raycast 실패: 공 아래 아무것도 없음 → 공 위치로 fallback (그림자 안 보이는 편이 나음)
            }

            transform.position = ball.transform.position + offset;
        }
    }
}
