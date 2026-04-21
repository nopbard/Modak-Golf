using UnityEngine;

namespace MiniGolf
{
    // 실제 물리/이동 없이 공이 '굴러가는' 것처럼 보이게 하는 페이크 롤러.
    // 메뉴 씬처럼 공이 제자리에 있지만 굴러가 보여야 하는 장식용 연출에 사용.
    public class BallFakeRoll : MonoBehaviour
    {
        [Tooltip("굴러가는 방향(월드 기준). 길이는 무시하고 정규화해서 사용")]
        [SerializeField]
        private Vector3 rollDirection = Vector3.forward;

        [Tooltip("굴러가는 선속도(유닛/초). 실제 이동은 하지 않고 회전 각속도 계산에만 사용")]
        [SerializeField]
        private float rollSpeed = 2f;

        [Tooltip("공 반지름(유닛). 각속도 = 선속도 / 반지름. 너무 작으면 0.5로 자동 보정")]
        [SerializeField]
        private float ballRadius = 0.5f;

        [Tooltip("true = 월드 축 기준 회전, false = 로컬 축 기준 회전")]
        [SerializeField]
        private bool useWorldSpace = true;

        [Tooltip("시작 시 무작위 회전으로 초기화해서 매번 다른 각도에서 굴러 보이게 함")]
        [SerializeField]
        private bool randomizeStartRotation = true;

        void Start()
        {
            if(randomizeStartRotation)
                transform.rotation = Random.rotation;
        }

        void Update()
        {
            Vector3 dir = rollDirection;
            if(dir.sqrMagnitude < 1e-6f || Mathf.Abs(rollSpeed) < 1e-6f)
                return;
            dir.Normalize();

            float radius = ballRadius > 1e-4f ? ballRadius : 0.5f;
            float angularSpeedDeg = (rollSpeed / radius) * Mathf.Rad2Deg;

            // 구르는 축 = up × 이동방향. 오른손 법칙으로 이동방향 앞쪽이 아래로 말려 들어감.
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            if(axis.sqrMagnitude < 1e-6f)
                return;
            axis.Normalize();

            transform.Rotate(axis, angularSpeedDeg * Time.deltaTime, useWorldSpace ? Space.World : Space.Self);
        }

        // 런타임에서 외부 시스템이 방향/속도를 바꿀 때 사용
        public void SetRoll(Vector3 direction, float speed)
        {
            rollDirection = direction;
            rollSpeed = speed;
        }
    }
}
