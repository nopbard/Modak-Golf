using UnityEngine;

namespace MiniGolf
{
    public class Hole : MonoBehaviour
    {
        [Tooltip("공이 들어갔을 때 Sink() 호출해 뿅 사라지게 할 Flag. 비워두면 같은 홀 프리팹에서 자동 검색.")]
        [SerializeField] private Flag flag;

        // 한 홀에서 여러 번 Sink 호출 방지 (2P 모드에서 같은 프리팹이 재사용될 경우 대비)
        private bool sunkTriggered;

        void Awake()
        {
            // 인스펙터에서 지정 안 한 경우 홀 프리팹 안에서 Flag 자동 탐색.
            // 1) 부모 체인 → 2) 자식 체인 → 3) 프리팹 루트 하위 전체
            if(flag == null) flag = GetComponentInParent<Flag>();
            if(flag == null) flag = GetComponentInChildren<Flag>();
            if(flag == null && transform.root != null)
                flag = transform.root.GetComponentInChildren<Flag>(true);
        }

        void OnTriggerEnter(Collider other)
        {
            if(!other.CompareTag("Ball")) return;
            if(GameManager.Instance != null && GameManager.Instance.BallInHole) return;
            if(sunkTriggered) return;
            sunkTriggered = true;

            if(GameManager.Instance != null)
                GameManager.Instance.BallSinked();

            // Flag 뿅 애니메이션 (Menu 씬의 MenuHole 과 동일한 연출)
            if(flag != null) flag.Sink();
        }
    }
}