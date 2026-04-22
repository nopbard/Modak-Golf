using UnityEngine;

namespace MiniGolf
{
    public class Hole : MonoBehaviour
    {
        [Tooltip("공이 들어갔을 때 Sink() 호출해 뿅 사라지게 할 Flag. 비워두면 같은 홀 프리팹에서 자동 검색.")]
        [SerializeField] private Flag flag;

        void Awake()
        {
            // 인스펙터에서 지정 안 한 경우 홀 프리팹 안에서 Flag 자동 탐색.
            if(flag == null) flag = GetComponentInParent<Flag>();
            if(flag == null) flag = GetComponentInChildren<Flag>();
            if(flag == null && transform.root != null)
                flag = transform.root.GetComponentInChildren<Flag>(true);
        }

        void OnTriggerEnter(Collider other)
        {
            if(!other.CompareTag("Ball")) return;
            // 멀티볼(2P) 지원: 이 공이 이미 Sunk 면 무시. GameManager 가 플레이어별로 sunk 추적.
            Ball ball = other.GetComponent<Ball>();
            if(ball == null || ball.CurState == Ball.State.Sunk) return;

            if(GameManager.Instance != null)
                GameManager.Instance.BallSinked(ball);

            // Flag 뿅 애니메이션 (첫 공 홀인 시에만 트리거되도록 Flag.Sink 자체는 멱등성 가정).
            if(flag != null) flag.Sink();
        }
    }
}