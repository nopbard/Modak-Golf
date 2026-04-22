using UnityEngine;

namespace MiniGolf
{
    [RequireComponent(typeof(MeshRenderer))]
    public class BallWaitingIndicator : MonoBehaviour
    {
        [SerializeField] private Ball ball;
        [SerializeField] private float scaleLerpSpeed;
        [SerializeField] private float scaleLerpSize;

        private float startScale;
        private MeshRenderer mr;

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
            mr.enabled = false;                        // 기본 숨김 — Update 가 상태 보고 켬
            // startScale 캐시도 Awake 에서. Start 에서 하면 Ball.SetActive(false)/PlaySpawnPopIn 타이밍 꼬임으로
            // Ball 의 scale 0 이 inherited 된 상태에서 캡처되어 인디케이터가 보이지 않게 됨.
            startScale = transform.localScale.x;
        }

        void Update()
        {
            // 표시 조건: 공이 물리적으로 정지 + 사용자가 공 안 건드림 + 스폰 쿨다운/팝인 끝남
            // + 공이 홀에 들어간 상태(Sunk / BallInHole) 가 아닐 것 → 다음 홀로 전환될 때까지 숨김
            bool inHole = (ball != null && ball.CurState == Ball.State.Sunk)
                          || (GameManager.Instance != null && GameManager.Instance.BallInHole);

            // BallTracker 가 unparent 해놓은 상태라 Ball 이 SetActive(false) 되어도 본 컴포넌트는 계속 Update 실행됨.
            // Ball 이 비활성인 동안에는 BallTracker 가 위치 갱신을 멈춰 이전 위치에 고정되므로 반드시 숨겨야 함.
            bool ballActive = ball != null && ball.gameObject.activeInHierarchy;

            // 2P 멀티볼: 자기 턴인 (활성) 공에서만 대기 인디케이터 표시.
            // Ball.Instance 는 현재 턴 플레이어의 공. 상대 공은 kinematic 으로 정지해 있지만 인디케이터는 안 띄움.
            bool isActiveBall = (ball != null && Ball.Instance == ball);
            // 턴 전환 대기 중(공 멈추고 다음 플레이어로 넘기는 0.6s 시점)엔 인디케이터도 숨김.
            bool turnSwitching = GameManager.Instance != null && GameManager.Instance.IsTurnSwitching;
            // CurState == Waiting 일 때만 표시. PreWait (공 멈추고 0.2s 버퍼) 나 Moving 에서는 숨김.
            // 이전에는 IsMoving (velocity 기반) 만 체크해서 PreWait 에서도 인디케이터가 깜빡임.
            bool stateIsWaiting = ball != null && ball.CurState == Ball.State.Waiting;

            bool shouldShow = ball != null
                              && ballActive             // 공 비활성이면 숨김 (홀 전환/코스 로드 중)
                              && isActiveBall           // 내 턴이 아닌 공은 숨김
                              && stateIsWaiting         // Waiting 상태에서만 표시 (PreWait 숨김)
                              && !turnSwitching          // 턴 전환 대기 중 숨김
                              && !inHole
                              && !ball.IsSpawning       // 팝인 중이면 숨김 (홀 전환 직후 깜빡임 방지)
                              && !ball.IsAwaitingRespawn // OOB 리스폰 대기 중이면 숨김
                              && !ball.IsSettling
                              && InputController.Instance != null
                              && !InputController.Instance.IsInteractingWithBall;

            mr.enabled = shouldShow;
            if(!shouldShow) return;

            float s = Mathf.Sin(Time.time * scaleLerpSpeed) * scaleLerpSize;
            transform.localScale = Vector3.one * (s + startScale);
        }
    }
}
