using UnityEngine;

namespace MiniGolf
{
    // 홀 프리팹 안에 배치되는 공 스폰 위치 마커.
    // 2P 모드일 때 플레이어별로 다른 스폰 위치를 쓰고 싶으면 BallStart 를 2개 배치하고
    // 각각 playerIndex 를 0(=1P), 1(=2P) 로 설정.
    public sealed class BallStart : MonoBehaviour
    {
        [Tooltip("0 = 1P, 1 = 2P")]
        [Range(0, 1)] [SerializeField] private int playerIndex = 0;

        public int PlayerIndex => playerIndex;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = playerIndex == 0
                ? new Color(0.3f, 0.8f, 1f, 0.9f)   // 1P: 푸른 색
                : new Color(1f, 0.4f, 0.6f, 0.9f);  // 2P: 분홍
            Gizmos.DrawWireSphere(transform.position, 0.15f);

            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.2f,
                $"{playerIndex + 1}P Start");
        }
#endif
    }
}
