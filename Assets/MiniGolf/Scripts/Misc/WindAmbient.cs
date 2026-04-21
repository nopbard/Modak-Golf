using UnityEngine;
using UnityEngine.Rendering;

namespace MiniGolf
{
    // 공기중에 떠다니는 바람 파티클 (ambient wind streaks).
    //
    // WebGL-safe:
    //   - built-in ParticleSystem(CPU) + Trails 모듈만 사용
    //   - VFX Graph / Compute Shader 사용 안 함
    //   - URP Particles Unlit 머티리얼 (Transparent) 전제
    //
    // 참고: https://anchitsh.github.io/wind.html (Trails 로 선을 그리는 기본 패턴)
    //
    // 사용:
    //   1) 씬에 빈 GameObject 생성 ("WindAmbient" 등)
    //   2) 이 스크립트 Add → ParticleSystem / Renderer 자동 추가
    //   3) trailMaterial 에 Assets/MiniGolf/Materials/WindTrail.mat 할당
    //   4) followTarget 에 Main Camera(혹은 Player) 드래그 → 카메라가 움직여도 항상 주위에 파티클
    //   5) windDirection / windSpeed 를 씬 분위기에 맞게 조정
    //
    // 설계 포인트:
    //   - 본체 파티클은 알파 0 + 사이즈 0.001 → 트레일만 보임
    //   - 방출은 rateOverTime (박스 안 랜덤 위치에서 계속 발생)
    //   - VelocityOverLifetime = windDirection * windSpeed + 랜덤 지터
    //   - Noise 모듈로 살짝 휘감는 듯한 흐름
    // ParticleSystemRenderer 는 ParticleSystem 추가 시 Unity 가 자동으로 붙여줌.
    // RequireComponent 로 중복 추가하면 NaN bounds 가진 두 번째 renderer 가 생겨
    // IsFinite(distanceForSort) / IsFinite(distanceAlongView) assertion 유발함. 그래서 뺐음.
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class WindAmbient : MonoBehaviour
    {
        [Header("Follow (optional)")]
        [Tooltip("이 Transform 을 따라다니며 방출 박스를 재배치. 보통 Main Camera.")]
        [SerializeField] private Transform followTarget;
        [Tooltip("followTarget 기준 오프셋")]
        [SerializeField] private Vector3 followOffset = Vector3.zero;

        [Header("Emission Volume")]
        [Tooltip("방출되는 박스 크기(월드 m). 카메라 주변을 덮을 정도로.")]
        [SerializeField] private Vector3 boxSize = new Vector3(20f, 5f, 20f);
        [Tooltip("초당 방출 파티클 수")]
        [SerializeField] private float emissionRate = 25f;
        [Tooltip("같은 시점에 존재 가능한 최대 파티클 수")]
        [SerializeField] private int maxParticles = 300;

        [Header("Wind")]
        [Tooltip("바람 방향(월드 기준). 정규화 불필요.")]
        [SerializeField] private Vector3 windDirection = new Vector3(1f, 0f, 0.3f);
        [Tooltip("바람 속도(m/s)")]
        [SerializeField] private float windSpeed = 4f;
        [Tooltip("각 파티클이 제멋대로 튀는 범위(m/s)")]
        [SerializeField] private float windJitter = 0.5f;
        [Tooltip("Noise 모듈 강도. 바람이 휘감기는 느낌. 0 이면 noise off. (0 권장 — 일부 Unity 버전에서 NaN bounds 원인)")]
        [Range(0f, 2f)] [SerializeField] private float noiseStrength = 0f;
        [Tooltip("Noise 모듈 주파수(낮을수록 완만한 소용돌이)")]
        [Range(0.05f, 3f)] [SerializeField] private float noiseFrequency = 0.4f;

        [Header("Particle Life")]
        [Tooltip("파티클 수명(초). 트레일 길이는 lifetime × windSpeed 정도가 됨.")]
        [SerializeField] private float particleLifetime = 2.5f;

        [Header("Trail Look")]
        [SerializeField] private Material trailMaterial;
        [Tooltip("트레일이 살아있는 시간(초)")]
        [SerializeField] private float trailLifetime = 1.5f;
        [Tooltip("트레일 최대 폭(m)")]
        [SerializeField] private float trailWidth = 0.05f;
        [Tooltip("트레일 색 + 최대 알파")]
        [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.35f);

        ParticleSystem ps;
        ParticleSystemRenderer psr;

        void Reset()
        {
            Configure();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            // 플레이 중 Inspector 수정 시 모든 모듈 재설정하면 내부 상태가 꼬여
            // IsFinite(distanceForSort) assertion 을 유발할 수 있음. 에디트 모드에서만 재구성.
            if (Application.isPlaying) return;
#if UNITY_EDITOR
            // GUI 이벤트 처리 중에 모듈 속성 건드리면 GetTransformInfoExpectUpToDate 경고 뜸.
            // delayCall 로 한 프레임 미뤄서 GUI 루프 밖에서 실행.
            UnityEditor.EditorApplication.delayCall += DelayedConfigure;
#else
            Configure();
#endif
        }

#if UNITY_EDITOR
        void DelayedConfigure()
        {
            if (this == null) return;           // 컴포넌트 파괴된 뒤에도 delayCall 큐에 남아있을 수 있음
            if (!isActiveAndEnabled) return;
            Configure();
        }
#endif

        void Awake()
        {
            Configure();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (followTarget == null) return;
            transform.position = followTarget.position + followOffset;
        }

        void Configure()
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            if (psr == null) psr = GetComponent<ParticleSystemRenderer>();
            if (ps == null || psr == null) return;

            var main = ps.main;
            main.loop = true;
            // duration 은 한 번만, 재생중에 변경하면 Unity 경고 + GetTransformInfoExpectUpToDate 유발.
            // 에디터 프리뷰 / 런타임 모두 커버. 이미 10이면 스킵.
            if (Mathf.Abs(main.duration - 10f) > 0.001f && !Application.isPlaying && !ps.isPlaying)
                main.duration = 10f;
            main.startLifetime = Mathf.Max(0.1f, particleLifetime);
            main.startSpeed = 0f;                                     // 속도는 velocityOverLifetime 로 부여
            main.startSize = 0.01f;                                   // 0 근처면 sort distance NaN 위험 → 최소 0.01
            main.startColor = new Color(1f, 1f, 1f, 0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.gravityModifier = 0f;
            main.playOnAwake = true;
            main.prewarm = false;                                     // prewarm + world + noise 조합이 NaN 원인이 될 수 있음

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = emissionRate;
            em.rateOverDistance = 0f;

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            // degenerate (0) scale 은 방출 위치 NaN 의 원인. 최소값 보장.
            sh.scale = new Vector3(
                Mathf.Max(0.01f, boxSize.x),
                Mathf.Max(0.01f, boxSize.y),
                Mathf.Max(0.01f, boxSize.z));
            sh.position = Vector3.zero;
            sh.rotation = Vector3.zero;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            Vector3 v = windDirection.sqrMagnitude > 0f
                ? windDirection.normalized * windSpeed
                : Vector3.zero;
            vol.x = new ParticleSystem.MinMaxCurve(v.x - windJitter, v.x + windJitter);
            vol.y = new ParticleSystem.MinMaxCurve(v.y - windJitter * 0.3f, v.y + windJitter * 0.3f);
            vol.z = new ParticleSystem.MinMaxCurve(v.z - windJitter, v.z + windJitter);

            var noise = ps.noise;
            noise.enabled = noiseStrength > 0.001f;
            noise.strength = noiseStrength;
            noise.frequency = noiseFrequency;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;           // WebGL 부하 ↓

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(trailColor.a, 0.2f),
                    new GradientAlphaKey(trailColor.a, 0.7f),
                    new GradientAlphaKey(0f, 1f),
                });

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = grad;

            var tr = ps.trails;
            tr.enabled = true;
            tr.mode = ParticleSystemTrailMode.PerParticle;
            tr.ratio = 1f;
            tr.lifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(0.1f, trailLifetime));
            tr.minVertexDistance = 0.05f;
            tr.worldSpace = true;                                     // 월드에 고정 → 뒤에 잔류하는 바람 스트리크 느낌
            tr.dieWithParticles = false;                              // 파티클 사라진 뒤에도 페이드아웃 (부드러움)
            tr.inheritParticleColor = false;
            tr.sizeAffectsWidth = false;
            tr.sizeAffectsLifetime = false;
            tr.widthOverTrail = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.001f, trailWidth),
                new AnimationCurve(
                    new Keyframe(0f, 0.1f, 0f, 0f),       // 끝점 tangent 0 → NaN 회피
                    new Keyframe(0.5f, 1f, 0f, 0f),
                    new Keyframe(1f, 0.1f, 0f, 0f)));
            tr.colorOverLifetime = new ParticleSystem.MinMaxGradient(grad);

            psr.enabled = true;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = trailMaterial;
            psr.trailMaterial = trailMaterial;
            psr.sortMode = ParticleSystemSortMode.None;                // distance 정렬 비활성 → NaN assertion 방지
            psr.sortingFudge = 0f;
            psr.receiveShadows = false;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.lightProbeUsage = LightProbeUsage.Off;
            psr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            psr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            psr.alignment = ParticleSystemRenderSpace.View;
        }

        public void SetMaterial(Material m)
        {
            trailMaterial = m;
            Configure();
        }

        public void SetWind(Vector3 dir, float speed)
        {
            windDirection = dir;
            windSpeed = speed;
            Configure();
        }

        // 씬에 serialize 된 이전 설정(prewarm=true, trails.worldSpace=true 등)이 남아있는 경우
        // 수동으로 현재 코드 기준 재구성 + ps 초기화.
        [ContextMenu("Force Reconfigure + Clear")]
        void ForceReconfigure()
        {
            ps = GetComponent<ParticleSystem>();
            psr = GetComponent<ParticleSystemRenderer>();
            Configure();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!isActiveAndEnabled) return;
            // Unity 6 에서 Gizmo 안에서 transform.position/rotation 직접 접근하면
            // GetTransformInfoExpectUpToDate 경고가 뜰 수 있음 → localToWorldMatrix 사용
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 1f);
            // windDirection 은 로컬 좌표가 아니지만 gizmo 는 방향 감 잡는 용도이므로 OK
            Vector3 dir = windDirection.sqrMagnitude > 0f ? windDirection.normalized : Vector3.right;
            Gizmos.DrawRay(Vector3.zero, dir * Mathf.Max(1f, windSpeed));
        }
#endif
    }
}
