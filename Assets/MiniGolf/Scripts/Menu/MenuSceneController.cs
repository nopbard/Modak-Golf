using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace MiniGolf
{
    // 메뉴 씬의 "월드 속 UX" 컨트롤러.
    //
    // 플로우:
    //   [씬 로드] → GameStart 지하에서 뿅 등장 + "Game Start" 텍스트 팝업
    //          → DOTween 끝나는 시점에 공을 GameStart 의 BallStart 위치에 스폰
    //          → 유저가 공 홀인
    //          → GameStart 지하로 하강 → CourseSelect 지하에서 뿅 등장
    //          → DOTween 끝나는 시점에 공을 CourseSelect 의 BallStart 위치에 스폰
    //          → 유저가 코스 홀에 공 넣기 → 해당 코스 씬 로드
    //
    // Ball 은 Hierarchy 상 GameStart / CourseSelect 자식이 아니어도 됨 (두트윈 애니메이션 영향 X).
    // 단순히 OnComplete 시점에 SetPosition + SetActive(true).
    [DefaultExecutionOrder(100)]
    public sealed class MenuSceneController : MonoBehaviour
    {
        [Header("Scene Objects")]
        [SerializeField] private Transform gameStartRoot;
        [SerializeField] private Transform courseSelectRoot;
        [Tooltip("GameStart 등장 완료 시 공이 놓일 위치. GameStart 의 BallStart 자식.")]
        [SerializeField] private Transform gameStartBallSpawn;
        [Tooltip("CourseSelect 등장 완료 시 공이 놓일 위치. CourseSelect 의 BallStart 자식.")]
        [SerializeField] private Transform courseSelectBallSpawn;

        [Header("Game Start Label")]
        [Tooltip("'Game Start' 라벨들. 씬 로드 시 scale 0→원본 으로 팝업 (여러 개 가능 — 텍스트 + 스프라이트 조합 등)")]
        [SerializeField] private Transform[] gameStartLabels;

        [Header("Animation")]
        [Tooltip("지하에서 뿅 올라오는 Y 오프셋 (m)")]
        [SerializeField] private float popDepth = 3f;
        [Tooltip("등장 시간 (초)")]
        [SerializeField] private float popInDuration = 0.7f;
        [Tooltip("퇴장 시간 (초)")]
        [SerializeField] private float popOutDuration = 0.5f;
        [Tooltip("코스 로드 전 짧은 대기 시간 (연출용)")]
        [SerializeField] private float loadDelay = 0.6f;
        [Tooltip("GameStart 홀 인 이후 CourseSelect 전환까지의 지연 (Flag sink 연출 시간)")]
        [SerializeField] private float postSinkDelay = 1.0f;

        [Header("Refs")]
        [SerializeField] private CourseList courseList;

        [Header("Ball Spawn")]
        [Tooltip("GameStart/CourseSelect 두트윈 끝난 뒤, 공이 등장하기까지의 대기 시간 (초)")]
        [SerializeField] private float ballSpawnDelay = 0.2f;
        [Tooltip("공이 착지해 멈추기 전까지 인디케이터/조준을 막는 시간 (초)")]
        [SerializeField] private float ballSettleDuration = 1.0f;
        [Tooltip("첫 스폰 시 카메라를 부드럽게 만들 시간 (초). 공이 착지할 때까지의 덜커덕 방지")]
        [SerializeField] private float firstSpawnSmoothDuration = 2.0f;
        [Tooltip("첫 스폰 시 카메라 damping 값. 높을수록 느리고 부드럽게 추적")]
        [SerializeField] private float firstSpawnSmoothDamping = 3.0f;

        enum Phase { Intro, ShowingGameStart, Transitioning, ShowingCourseSelect, Loading }
        Phase phase = Phase.Intro;

        Vector3 gameStartTargetPos;
        Vector3 courseSelectTargetPos;
        Vector3[] gameStartLabelTargetScales;
        bool isFirstSpawn = true;

        void Awake()
        {
            // GameStart 지하로, CourseSelect 지하+비활성. 공은 Intro 끝날 때 까지 숨김.
            if(gameStartRoot != null)
            {
                gameStartTargetPos = gameStartRoot.position;
                gameStartRoot.position = gameStartTargetPos + Vector3.down * popDepth;
            }
            if(courseSelectRoot != null)
            {
                courseSelectTargetPos = courseSelectRoot.position;
                courseSelectRoot.position = courseSelectTargetPos + Vector3.down * popDepth;
                courseSelectRoot.gameObject.SetActive(false);
            }
            if(gameStartLabels != null && gameStartLabels.Length > 0)
            {
                gameStartLabelTargetScales = new Vector3[gameStartLabels.Length];
                for(int i = 0; i < gameStartLabels.Length; i++)
                {
                    if(gameStartLabels[i] == null) continue;
                    gameStartLabelTargetScales[i] = gameStartLabels[i].localScale;
                    gameStartLabels[i].localScale = Vector3.zero;
                }
            }

            // Ball.Instance 는 Ball.Awake 에서 세팅됨. DefaultExecutionOrder 덕에 우리가 나중에 실행돼서 접근 가능.
            if(Ball.Instance != null)
                Ball.Instance.gameObject.SetActive(false);
        }

        IEnumerator Start()
        {
            yield return null;
            PlayGameStartIntro();
        }

        // ── Public Entry Points (UnityEvent 연결용) ──────────────────────────
        public void OnGameStartHoleSunk()
        {
            if(phase != Phase.ShowingGameStart) return;
            phase = Phase.Transitioning;            // 추가 클릭/입력 방지
            StartCoroutine(DelayedTransition());
        }

        IEnumerator DelayedTransition()
        {
            if(postSinkDelay > 0f)
                yield return new WaitForSeconds(postSinkDelay);
            yield return TransitionToCourseSelect();
        }

        public void LoadCourse(int courseIndex, int playerCount)
        {
            if(phase == Phase.Loading) return;
            phase = Phase.Loading;

            playerCount = Mathf.Clamp(playerCount, 1, 2);
            PlayerPrefs.SetInt("PlayerCount", playerCount);
            PlayerPrefs.SetInt("CourseToPlay", courseIndex);

            string sceneName = (courseList != null && courseIndex >= 0 && courseIndex < courseList.Courses.Length)
                ? courseList.Courses[courseIndex].GameScene
                : null;

            if(string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"MenuSceneController: courseIndex {courseIndex} 에 대응하는 GameScene 을 못 찾았습니다.");
                phase = Phase.ShowingCourseSelect;
                return;
            }

            StartCoroutine(LoadSceneAfterDelay(sceneName));
        }

        IEnumerator LoadSceneAfterDelay(string sceneName)
        {
            yield return new WaitForSeconds(loadDelay);
            if(ScreenFade.Instance != null)
                ScreenFade.Instance.BeginTransition(() => SceneManager.LoadScene(sceneName));
            else
                SceneManager.LoadScene(sceneName);
        }

        // ── Animations ─────────────────────────────────────────────────────────
        void PlayGameStartIntro()
        {
            phase = Phase.Intro;

            if(gameStartRoot != null)
            {
                gameStartRoot.DOMoveY(gameStartTargetPos.y, popInDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        phase = Phase.ShowingGameStart;
                        StartCoroutine(DelayedSpawn(gameStartBallSpawn));
                    });
            }
            else
            {
                phase = Phase.ShowingGameStart;
                StartCoroutine(DelayedSpawn(gameStartBallSpawn));
            }

            if(gameStartLabels != null)
            {
                for(int i = 0; i < gameStartLabels.Length; i++)
                {
                    var lbl = gameStartLabels[i];
                    if(lbl == null) continue;
                    Vector3 targetScale = gameStartLabelTargetScales != null && i < gameStartLabelTargetScales.Length
                        ? gameStartLabelTargetScales[i]
                        : Vector3.one;
                    lbl.localScale = Vector3.zero;
                    lbl.DOScale(targetScale, popInDuration)
                        .SetEase(Ease.OutBack)
                        .SetDelay(popInDuration * 0.3f);
                }
            }
        }

        IEnumerator TransitionToCourseSelect()
        {
            phase = Phase.Transitioning;

            // 공 숨김
            if(Ball.Instance != null) Ball.Instance.gameObject.SetActive(false);

            if(gameStartLabels != null)
            {
                foreach(var lbl in gameStartLabels)
                {
                    if(lbl != null)
                        lbl.DOScale(Vector3.zero, popOutDuration * 0.6f).SetEase(Ease.InBack);
                }
            }

            if(gameStartRoot != null)
                gameStartRoot.DOMoveY(gameStartTargetPos.y - popDepth, popOutDuration).SetEase(Ease.InBack);

            yield return new WaitForSeconds(popOutDuration + 0.1f);

            if(gameStartRoot != null) gameStartRoot.gameObject.SetActive(false);

            if(courseSelectRoot != null)
            {
                courseSelectRoot.gameObject.SetActive(true);
                courseSelectRoot.position = courseSelectTargetPos + Vector3.down * popDepth;
                courseSelectRoot.DOMoveY(courseSelectTargetPos.y, popInDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        phase = Phase.ShowingCourseSelect;
                        StartCoroutine(DelayedSpawn(courseSelectBallSpawn));
                    });
            }
            else
            {
                phase = Phase.ShowingCourseSelect;
                StartCoroutine(DelayedSpawn(courseSelectBallSpawn));
            }
        }

        IEnumerator DelayedSpawn(Transform spawn)
        {
            if(ballSpawnDelay > 0f)
                yield return new WaitForSeconds(ballSpawnDelay);
            SpawnBallAt(spawn);
        }

        // 공을 지정 위치로 옮기고 활성화. 연출 없음.
        void SpawnBallAt(Transform spawn)
        {
            if(Ball.Instance == null) return;
            Ball.Instance.gameObject.SetActive(true);

            var camCtrl = FindAnyObjectByType<CameraController>();

            if(spawn != null)
            {
                Ball.Instance.SetPosition(spawn.position);

                if(camCtrl != null)
                {
                    if(isFirstSpawn)
                    {
                        // 첫 스폰: 카메라 워프 없이 부드러운 damping 으로 따라오게.
                        // 공이 지면에 정착하는 동안 덜커덕 없이 스르르 추적.
                        camCtrl.SetSmoothDamping(firstSpawnSmoothDamping, firstSpawnSmoothDuration);
                    }
                    else
                    {
                        // 이후 스폰(CourseSelect 전환): 즉시 워프 → 순간이동 덜커덕 방지
                        camCtrl.SetPosition(spawn.position);
                    }
                }
            }

            isFirstSpawn = false;

            // 스폰 직후 착지 전까지 인디케이터/조준 숨김
            Ball.Instance.StartSpawnSettle(ballSettleDuration);
        }
    }
}
