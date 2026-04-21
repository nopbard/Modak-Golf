using System;

namespace MiniGolf
{
    // 전역 코인 카운터. 프로토타입 단계라 정적 상태 + 이벤트로 간단히.
    // 나중에 플레이어별/씬별로 분리 필요해지면 싱글톤으로 전환.
    public static class CoinCounter
    {
        public static int Total { get; private set; }
        public static event Action<int> OnCountChanged;

        public static void Add(int amount = 1)
        {
            if(amount == 0) return;
            Total += amount;
            OnCountChanged?.Invoke(Total);
        }

        public static void Reset()
        {
            Total = 0;
            OnCountChanged?.Invoke(Total);
        }
    }
}
