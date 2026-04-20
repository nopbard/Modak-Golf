using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Umbra {

    [DisallowMultipleRendererFeature("Umbra Render Feature")]
    [Tooltip("Umbra Render Feature")]
    internal partial class UmbraRenderFeature : ScreenSpaceShadows {
#if UNITY_EDITOR
        [UnityEditor.ShaderKeywordFilter.SelectIf(true, keywordNames: ShaderKeywordStrings.MainLightShadowScreen)]
        private const bool k_RequiresScreenSpaceShadowsKeyword = true;
#endif

        static class ShaderParams {
            readonly public static int MainTex = Shader.PropertyToID("_MainTex");
            readonly public static int ShadowData = Shader.PropertyToID("_ShadowData");
            readonly public static int ShadowData2 = Shader.PropertyToID("_ShadowData2");
            readonly public static int ShadowData3 = Shader.PropertyToID("_ShadowData3");
            readonly public static int ShadowData4 = Shader.PropertyToID("_ShadowData4");
            readonly public static int BlurTemp = Shader.PropertyToID("_BlurTemp");
            readonly public static int BlurTemp2 = Shader.PropertyToID("_BlurTemp2");
            readonly public static int BlurScale = Shader.PropertyToID("_BlurScale");
            readonly public static int BlurSpread = Shader.PropertyToID("_BlurSpread");
            readonly public static int UmbraCascadeRects = Shader.PropertyToID("_UmbraCascadeRects");
            readonly public static int UmbraCascadeScales = Shader.PropertyToID("_UmbraCascadeScales");
            readonly public static int DownsampledDepth = Shader.PropertyToID("_DownsampledDepth");
            readonly public static int NoiseTex = Shader.PropertyToID("_NoiseTex");
            readonly public static int SourceSize = Shader.PropertyToID("_SourceSize");
            readonly public static int BlendCascadeData = Shader.PropertyToID("_BlendCascadeData");
            readonly public static int MaskTexture = Shader.PropertyToID("_MaskTex");
            readonly public static int CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
            readonly public static int CameraNormalsTexture = Shader.PropertyToID("_CameraNormalsTexture");
            readonly public static int ContactShadowsSampleCount = Shader.PropertyToID("_ContactShadowsSampleCount");
            readonly public static int ContactShadowsData1 = Shader.PropertyToID("_ContactShadowsData1");
            readonly public static int ContactShadowsData2 = Shader.PropertyToID("_ContactShadowsData2");
            readonly public static int ContactShadowsData3 = Shader.PropertyToID("_ContactShadowsData3");
            readonly public static int ContactShadowsData4 = Shader.PropertyToID("_ContactShadowsData4");
            readonly public static int ContactShadowsBlendMode = Shader.PropertyToID("_ContactShadowsBlend");
            readonly public static int ReceiverPlaneAltitude = Shader.PropertyToID("_ReceiverPlaneAltitude");
            readonly public static int OverlayShadowColor = Shader.PropertyToID("_OverlayShadowColor");
            readonly public static int EarlyOutSamples = Shader.PropertyToID("_EarlyOutSamples");
            readonly public static int PointLightPosition = Shader.PropertyToID("_PointLightPosition");

            public static Vector3 Vector3Back = Vector3.back;
            public static Vector3 Vector3Forward = Vector3.forward;

            public const string SKW_LOOP_STEP_X3 = "_LOOP_STEP_X3";
            public const string SKW_LOOP_STEP_X2 = "_LOOP_STEP_X2";
            public const string SKW_PRESERVE_EDGES = "_PRESERVE_EDGES";
            public const string SKW_BLUR_HQ = "_BLUR_HQ";
            public const string SKW_NORMALS_TEXTURE = "_NORMALS_TEXTURE";
            public const string SKW_CONTACT_HARDENING = "_CONTACT_HARDENING";
            public const string SKW_MASK_TEXTURE = "_MASK_TEXTURE";
            public const string SKW_RECEIVER_PLANE = "_RECEIVER_PLANE";
            public const string SKW_USE_POINT_LIGHT = "_USE_POINT_LIGHT";
            public const string SKW_CONTACT_SHADOWS_SOFT_EDGES = "_SOFT_EDGES";
        }

        enum Pass {
            UmbraCastShadows,
            BlurHoriz,
            BlurVert,
            BoxBlur,
            ComposeWithBlending,
            DownsampledDepth,
            CascadeBlending,
            UnityShadows,
            ComposeUnity,
            ContactShadows,
            Compose,
            DebugShadows,
            ContactShadowsAfterOpaque,
            OverlayShadows
        }

        struct CameraLocation {
            public Vector3 position;
            public Vector3 forward;
        }

        const string k_ShaderName = "Hidden/Kronnect/UmbraScreenSpaceShadows";

        [Tooltip("Specify which cameras can render Umbra Soft Shadows")]
        public LayerMask camerasLayerMask = -1;

        public static UmbraSoftShadows settings;
        public static int shadowLightIndex;

        static int cachedShadowmapTimestap;
        static bool usesCachedShadowmap;

        readonly Dictionary<Camera, CameraLocation> cameraPrevLocation = new Dictionary<Camera, CameraLocation>();
        static readonly Dictionary<Light, UmbraSoftShadows> umbraSettings = new Dictionary<Light, UmbraSoftShadows>();

        Material mat;
        UmbraScreenSpaceShadowsPass m_SSShadowsPass;
        UmbraScreenSpaceShadowsPostPass m_SSShadowsPostPass;
        UmbraDebugPass m_SSSShadowsDebugPass;
        UmbraContactShadowsAfterOpaquePass m_ContactShadowsAfterOpaquePass;
        UmbraOverlayShadows m_OverlayShadowsPass;

        public static void RegisterUmbraLight (UmbraSoftShadows settings) {
            Light light = settings.GetComponent<Light>();
            if (light != null) {
                if (light.type == LightType.Directional) {
                    umbraSettings[light] = settings;
                }
                else {
                    Debug.LogError("Umbra Soft Shadows only work on directiona light.");
                }
            }
        }

        public static void UnregisterUmbraLight (UmbraSoftShadows settings) {
            Light light = settings.GetComponent<Light>();
            if (light != null && umbraSettings.ContainsKey(light)) {
                umbraSettings.Remove(light);
            }
        }


        public override void Create () {
            if (m_SSShadowsPass == null) {
                m_SSShadowsPass = new UmbraScreenSpaceShadowsPass();
            }
            if (m_SSShadowsPostPass == null) {
                m_SSShadowsPostPass = new UmbraScreenSpaceShadowsPostPass();
            }
            if (m_ContactShadowsAfterOpaquePass == null) {
                m_ContactShadowsAfterOpaquePass = new UmbraContactShadowsAfterOpaquePass();
            }
            if (m_SSSShadowsDebugPass == null) {
                m_SSSShadowsDebugPass = new UmbraDebugPass();
            }
            if (m_OverlayShadowsPass == null) {
                m_OverlayShadowsPass = new UmbraOverlayShadows();
            }

            cachedShadowmapTimestap = -100;

            LoadMaterial();

            m_SSShadowsPass.renderPassEvent = RenderPassEvent.AfterRenderingGbuffer;
            m_SSShadowsPostPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            m_OverlayShadowsPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques + 1;
            m_ContactShadowsAfterOpaquePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques + 1;
            m_SSSShadowsDebugPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        }

        void OnDisable () {
            UmbraSoftShadows.installed = false;
        }

        protected override void Dispose (bool disposing) {
            m_SSShadowsPass?.Dispose();
            m_SSShadowsPass = null;
            CoreUtils.Destroy(mat);
        }

        static bool SetupContactShadowsMaterial (Camera cam, UmbraProfile profile, Material mat) {

            float intensityMultiplier = profile.contactShadowsIntensityMultiplier;
            // Check if we should use point lights for contact shadows
            mat.DisableKeyword(ShaderParams.SKW_USE_POINT_LIGHT);
            if (settings.contactShadowsSource == ContactShadowsSource.PointLights) {
                if (settings.pointLightsTrigger == null && Camera.main != null) {
                    settings.pointLightsTrigger = Camera.main.transform;
                }
                float fade = 0;
                if (settings.pointLightsTrigger != null) {
                    Vector3 triggerPosition = settings.pointLightsTrigger.position;

                    // Find the point light that contains the trigger position
                    foreach (var kvp in UmbraPointLightContactShadows.umbraPointLights) {
                        UmbraPointLightContactShadows pointLightComponent = kvp.Value;
                        if (pointLightComponent == null) continue;
                        fade = pointLightComponent.ComputeVolumeFade(triggerPosition);
                        if (fade > 0) {
                            // Set point light data for the shader
                            Vector3 pointLightPosition = pointLightComponent.transform.position;
                            mat.SetVector(ShaderParams.PointLightPosition, new Vector4(pointLightPosition.x, pointLightPosition.y, pointLightPosition.z, 1.0f));
                            mat.EnableKeyword(ShaderParams.SKW_USE_POINT_LIGHT);
                            break; // Use the first point light found
                        }
                    }
                }
                if (fade <= 0) return false;
                intensityMultiplier *= fade;
            }

            float farClipPlane = cam.farClipPlane;
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float shadowMaxDistance = urpAsset.shadowDistance;

            mat.SetInt(ShaderParams.ContactShadowsSampleCount, profile.contactShadowsSampleCount);
            float contactShadowsMaxDistance;
            if (settings.profile.shadowSource == ShadowSource.UnityShadows || settings.profile.actualContactShadowsInjectionPoint == ContactShadowsInjectionPoint.AfterOpaque) {
                contactShadowsMaxDistance = 1;
            }
            else {
                contactShadowsMaxDistance = shadowMaxDistance / farClipPlane;
            }
            mat.SetVector(ShaderParams.ContactShadowsData1, new Vector4(profile.contactShadowsStepping, intensityMultiplier, profile.contactShadowsJitter, profile.contactShadowsDistanceFade));
            mat.SetVector(ShaderParams.ContactShadowsData2, new Vector4(profile.contactShadowsStartDistance / farClipPlane, profile.contactShadowsStartDistanceFade / farClipPlane, contactShadowsMaxDistance, profile.contactShadowsNormalBias));
            mat.SetVector(ShaderParams.ContactShadowsData3, new Vector4(profile.contactShadowsThicknessNear / farClipPlane, profile.contactShadowsThicknessDistanceMultiplier * 0.1f, profile.contactShadowsVignetteSize, profile.contactShadowsBias));
            mat.SetVector(ShaderParams.ContactShadowsData4, new Vector4(profile.contactShadowsBiasFar + 0.0025f, profile.contactShadowsEdgeSoftness, profile.contactShadowsPlanarShadows ? 0f : 1f, 0));
            mat.SetInt(ShaderParams.ContactShadowsBlendMode, settings.debugShadows ? (int)BlendMode.SrcAlpha : (int)BlendMode.OneMinusSrcAlpha);

            // Enable/disable soft edges keyword
            if (profile.contactShadowsSoftEdges) {
                mat.EnableKeyword(ShaderParams.SKW_CONTACT_SHADOWS_SOFT_EDGES);
            }
            else {
                mat.DisableKeyword(ShaderParams.SKW_CONTACT_SHADOWS_SOFT_EDGES);
            }

            return true;
        }

        static void SetupContactShadowsAfterOpaqueOnlyMaterial (UmbraProfile profile, Material mat) {
            if ((UmbraSoftShadows.isDeferred && profile.normalsSource == NormalSource.GBufferNormals) || profile.effectiveNormalsSource == NormalSource.NormalsPass) {
                mat.EnableKeyword(ShaderParams.SKW_NORMALS_TEXTURE);
            }
            else {
                mat.DisableKeyword(ShaderParams.SKW_NORMALS_TEXTURE);
            }
            mat.DisableKeyword(ShaderParams.SKW_LOOP_STEP_X3);
            mat.DisableKeyword(ShaderParams.SKW_LOOP_STEP_X2);
            if (profile.loopStepOptimization == LoopStep.x3) {
                mat.EnableKeyword(ShaderParams.SKW_LOOP_STEP_X3);
            }
            else if (profile.loopStepOptimization == LoopStep.x2) {
                mat.EnableKeyword(ShaderParams.SKW_LOOP_STEP_X2);
            }
            if (profile.transparentReceiverPlane) {
                mat.EnableKeyword(ShaderParams.SKW_RECEIVER_PLANE);
            }
            else {
                mat.DisableKeyword(ShaderParams.SKW_RECEIVER_PLANE);
            }
        }

        static bool IsOffscreenDepthTexture (ref CameraData cameraData) => cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth;

        public override void AddRenderPasses (ScriptableRenderer renderer, ref RenderingData renderingData) {

            UmbraSoftShadows.installed = true;

            if (IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            if (!LoadMaterial()) {
                Debug.LogError("Umbra: can't load material");
                return;
            }

            // Fetch settings from current main directional light
            Light light = null;
            shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex < 0) {
                // fallback to static instance search in case that "Only Contact Shadows" option is used
                if (UmbraSoftShadows.instance != null) {
                    light = UmbraSoftShadows.instance.GetComponent<Light>();
                }
            }
            else {
                light = renderingData.lightData.visibleLights[shadowLightIndex].light;
            }
            if (light == null) return;

            if (!umbraSettings.TryGetValue(light, out settings)) return;
            if (settings == null) return;

            Camera cam = renderingData.cameraData.camera;

            usesCachedShadowmap = cachedShadowmapTimestap == Time.frameCount - 1 && settings != null && settings.profile != null && settings.profile.frameSkipOptimization && Application.isPlaying;
            if (usesCachedShadowmap) {
                // test camera rotation/movement
                Transform t = cam.transform;
                Vector3 pos = t.position;
                Vector3 fwd = t.forward;
                bool camMoved = true;
                if (cameraPrevLocation.TryGetValue(cam, out CameraLocation prevLocation)) {
                    float dx = pos.x - prevLocation.position.x;
                    float dy = pos.y - prevLocation.position.y;
                    float dz = pos.z - prevLocation.position.z;
                    if (dx < 0) dx = -dx;
                    if (dy < 0) dy = -dy;
                    if (dz < 0) dz = -dz;
                    float thresholdPosition = settings.profile.skipFrameMaxCameraDisplacement;
                    if (dx <= thresholdPosition && dy <= thresholdPosition && dz <= thresholdPosition) {
                        if (Vector3.Angle(prevLocation.forward, fwd) <= settings.profile.skipFrameMaxCameraRotation) {
                            camMoved = false;
                        }
                    }
                }
                if (camMoved) {
                    prevLocation.position = pos;
                    prevLocation.forward = fwd;
                    cameraPrevLocation[cam] = prevLocation;
                    usesCachedShadowmap = false;
                }
            }

            UmbraSoftShadows.isDeferred = renderer is UniversalRenderer && ((UniversalRenderer)renderer).renderingModeRequested == RenderingMode.Deferred;
            bool shouldEnqueue = ((camerasLayerMask & (1 << cam.gameObject.layer)) != 0) && m_SSShadowsPass.Setup(mat);

            if (shouldEnqueue) {
                bool allowMainLightShadows = renderingData.shadowData.supportsMainLightShadows && renderingData.lightData.mainLightIndex != -1 && renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex].light.shadowStrength > 0;
                bool allowScreenSpaceShadows = allowMainLightShadows && (settings.profile == null || settings.profile.shadowSource != ShadowSource.OnlyContactShadows);
                if (allowScreenSpaceShadows) {
                    m_SSShadowsPass.renderPassEvent = UmbraSoftShadows.isDeferred
                        ? RenderPassEvent.AfterRenderingGbuffer
                        : RenderPassEvent.AfterRenderingPrePasses + 1; // We add 1 to ensure this happens after depth priming depth copy pass that might be scheduled

                    renderer.EnqueuePass(m_SSShadowsPass);
                    renderer.EnqueuePass(m_SSShadowsPostPass);
                }

                if (settings.profile != null) {
                    if (!settings.debugShadows && allowScreenSpaceShadows && settings.profile.overlayShadows && settings.profile.overlayShadowsIntensity > 0) {
                        renderer.EnqueuePass(m_OverlayShadowsPass);
                    }
                    if (settings.profile.contactShadows || settings.profile.shadowSource == ShadowSource.OnlyContactShadows) {
                        if (SetupContactShadowsMaterial(cam, settings.profile, mat)) {
                            if (settings.profile.shadowSource != ShadowSource.UmbraShadows || settings.profile.actualContactShadowsInjectionPoint == ContactShadowsInjectionPoint.AfterOpaque) {
                                SetupContactShadowsAfterOpaqueOnlyMaterial(settings.profile, mat);
                                m_ContactShadowsAfterOpaquePass.Setup();
                                renderer.EnqueuePass(m_ContactShadowsAfterOpaquePass);
                            }
                        }
                    }
                }

                if (allowScreenSpaceShadows && settings.debugShadows) {
                    m_SSSShadowsDebugPass.Setup(m_SSShadowsPass);
                    renderer.EnqueuePass(m_SSSShadowsDebugPass);
                }
            }
        }

        private bool LoadMaterial () {
            if (mat != null) {
                return true;
            }

            Shader shader = Shader.Find(k_ShaderName);
            if (shader == null) {
                return false;
            }

            mat = CoreUtils.CreateEngineMaterial(shader);
            Texture2D noiseTex = Resources.Load<Texture2D>("Umbra/Textures/NoiseTex");
            mat.SetTexture(ShaderParams.NoiseTex, noiseTex);

            return mat != null;
        }

        private partial class UmbraScreenSpaceShadowsPass : ScriptableRenderPass {

            static string m_ProfilerTag = "UmbraSoftShadows";
            static ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
            public static Material mat;
            static RTHandle m_RenderTarget, m_DownscaledRenderTarget;
            static readonly Vector4[][] cascadeRects =  {
                new Vector4[] { new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1) },
                new Vector4[] { new Vector4(0, 0, 0.5f, 1f), new Vector4(0.5f, 0, 1, 1f), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1) },
                new Vector4[] { new Vector4(0, 0, 0.5f, 0.5f), new Vector4(0.5f, 0, 1, 0.5f), new Vector4(0, 0.5f, 0.5f, 1), new Vector4(0.5f, 0.5f, 1, 1) },
                new Vector4[] { new Vector4(0, 0, 0.5f, 0.5f), new Vector4(0.5f, 0, 1, 0.5f), new Vector4(0, 0.5f, 0.5f, 1), new Vector4(0.5f, 0.5f, 1, 1) }
            };
            static readonly Vector4[][] cascadeRectsWithPadding =  {
                new Vector4[] { new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1) },
                new Vector4[] { new Vector4(0, 0, 0.5f, 1f), new Vector4(0.5f, 0, 1, 1f), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1) },
                new Vector4[] { new Vector4(0, 0, 0.5f, 0.5f), new Vector4(0.5f, 0, 1, 0.5f), new Vector4(0, 0.5f, 0.5f, 1), new Vector4(0.5f, 0.5f, 1, 1) },
                new Vector4[] { new Vector4(0, 0, 0.5f, 0.5f), new Vector4(0.5f, 0, 1, 0.5f), new Vector4(0, 0.5f, 0.5f, 1), new Vector4(0.5f, 0.5f, 1, 1) }
            };
            static readonly float[] cascadeScales = { 1, 1, 1, 1 };
            static RenderTextureDescriptor desc;
            GraphicsFormat screenShadowTextureFormat;
            public readonly Dictionary<Camera, RTHandle> shadowTextures = new Dictionary<Camera, RTHandle>();
            static bool newShadowmap = true;
            static readonly float[] autoCascadeScales = new float[4];


            public void Dispose () {
                foreach (var rt in shadowTextures.Values) {
                    rt?.Release();
                }
                shadowTextures.Clear();
                m_RenderTarget?.Release();
                m_DownscaledRenderTarget?.Release();
            }

            internal bool Setup (Material material) {
                if (settings == null || !settings.enabled || settings.profile == null) return false;
                mat = material;
                UmbraProfile profile = settings.profile;

                GraphicsFormat desiredFormat = profile.shadowSource == ShadowSource.UmbraShadows && profile.blurIterations > 0 && profile.enableContactHardening ? GraphicsFormat.R8G8_UNorm : GraphicsFormat.R8_UNorm;
#if UNITY_2023_1_OR_NEWER
                screenShadowTextureFormat = SystemInfo.IsFormatSupported(desiredFormat, GraphicsFormatUsage.Linear | GraphicsFormatUsage.Render)
#else
                screenShadowTextureFormat = RenderingUtils.SupportsGraphicsFormat(desiredFormat, FormatUsage.Linear | FormatUsage.Render)
#endif
                    ? desiredFormat
                    : GraphicsFormat.B8G8R8A8_UNorm;
                if (usesCachedShadowmap) {
                    ConfigureInput(ScriptableRenderPassInput.None);
                }
                else {
                    if (profile.shadowSource == ShadowSource.UmbraShadows && (profile.effectiveNormalsSource == NormalSource.NormalsPass || profile.downsample || profile.forceDepthPrepass)) {
                        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                    }
                    else {
                        ConfigureInput(ScriptableRenderPassInput.Depth);
                    }
                }
                return mat != null;
            }

#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup (CommandBuffer cmd, ref RenderingData renderingData) {

                desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.graphicsFormat = screenShadowTextureFormat;

                if (settings == null || settings.profile == null) return;

                UmbraProfile profile = settings.profile;

                if (profile.downsample && !profile.preserveEdges) {
                    desc.width /= 2;
                    desc.height /= 2;
                }

                Camera cam = renderingData.cameraData.camera;
#if UNITY_EDITOR
                if (profile.frameSkipOptimization && Application.isPlaying) {
#else
                if (profile.frameSkipOptimization) {
#endif
                    newShadowmap = !shadowTextures.TryGetValue(cam, out m_RenderTarget);
                }

#if UNITY_6000_0_OR_NEWER
                if (RenderingUtils.ReAllocateHandleIfNeeded(ref m_RenderTarget, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceShadowmapTexture")) {
#else
                if (RenderingUtils.ReAllocateIfNeeded(ref m_RenderTarget, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceShadowmapTexture")) {
#endif
                    newShadowmap = true;
                }
                if (newShadowmap) {
                    shadowTextures[cam] = m_RenderTarget;
                }

                cmd.SetGlobalTexture(m_RenderTarget.name, m_RenderTarget.nameID);

                ConfigureTarget(m_RenderTarget);
                ConfigureClear(ClearFlag.None, Color.white);
            }
#endif


#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {

                if (mat == null) {
                    Debug.LogError("Umbra material not initialized");
                    return;
                }
                UmbraProfile profile = settings.profile;

                var cmd = renderingData.commandBuffer;
                using (new ProfilingScope(cmd, m_ProfilingSampler)) {

                    int frameCount = Time.frameCount;
#if UNITY_EDITOR
                    bool useFrame = !Application.isPlaying || !profile.frameSkipOptimization || newShadowmap || !usesCachedShadowmap;
#else
                    bool useFrame = !profile.frameSkipOptimization || newShadowmap || !usesCachedShadowmap;
#endif
                    if (useFrame) {
                        cachedShadowmapTimestap = frameCount;
                        newShadowmap = false;

                        RTHandle shadowsHandle;

                        if (profile.downsample && profile.preserveEdges) {
                            desc.width /= 2;
                            desc.height /= 2;
#if UNITY_6000_0_OR_NEWER
                            RenderingUtils.ReAllocateHandleIfNeeded(ref m_DownscaledRenderTarget, desc, FilterMode.Point, TextureWrapMode.Clamp);
#else
                            RenderingUtils.ReAllocateIfNeeded(ref m_DownscaledRenderTarget, desc, FilterMode.Point, TextureWrapMode.Clamp);
#endif
                            shadowsHandle = m_DownscaledRenderTarget;
                        }
                        else {
                            shadowsHandle = m_RenderTarget;
                        }

                        int cascadeCount = renderingData.shadowData.mainLightShadowCascadesCount;
                        float farClipPlane = renderingData.cameraData.camera.farClipPlane;
                        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                        float shadowMaxDistance = urpAsset.shadowDistance;

                        Pass castShadowsPass;

                        if (profile.shadowSource == ShadowSource.UnityShadows) {
                            castShadowsPass = Pass.UnityShadows;
                        }
                        else {
                            castShadowsPass = Pass.UmbraCastShadows;

                            if (profile.transparentReceiverPlane) {
                                mat.EnableKeyword(ShaderParams.SKW_RECEIVER_PLANE);
                                cmd.SetGlobalFloat(ShaderParams.ReceiverPlaneAltitude, profile.receiverPlaneAltitude);
                                cmd.SetGlobalVector(ShaderParams.ShadowData3, new Vector4(10f, 0.001f, 0, profile.blurEdgeSharpness));
                            }
                            else {
                                mat.DisableKeyword(ShaderParams.SKW_RECEIVER_PLANE);
                                cmd.SetGlobalVector(ShaderParams.ShadowData3, new Vector4(profile.blurDepthAttenStart / farClipPlane, profile.blurDepthAttenLength / farClipPlane, profile.blurGrazingAttenuation, profile.blurEdgeSharpness));
                            }

                            cmd.SetGlobalVector(ShaderParams.ShadowData, new Vector4(profile.sampleCount, 1024 / Mathf.Pow(2, profile.posterization), profile.blurEdgeTolerance * 1000f, (profile.blurDepthAttenStart + profile.blurDepthAttenLength) / farClipPlane));
                            float shadowMaxDepth = profile.transparentReceiverPlane ? 2 : shadowMaxDistance / farClipPlane;
                            cmd.SetGlobalVector(ShaderParams.ShadowData2, new Vector4(1f - profile.contactStrength, profile.distantSpread, shadowMaxDepth, profile.lightSize * 0.02f));
                            cmd.SetGlobalVector(ShaderParams.ShadowData4, new Vector4(profile.occludersCount, profile.occludersSearchRadius * 0.02f, profile.contactStrength > 0 ? profile.contactStrengthKnee * 0.1f : 0.00001f, profile.maskScale));
                            cmd.SetGlobalVector(ShaderParams.SourceSize, new Vector4(desc.width, desc.height, 0, 0));
                            cmd.SetGlobalInt(ShaderParams.EarlyOutSamples, profile.earlyOutSamples);

                            if (cascadeCount > 1) {
                                VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
                                float shadowNearPlane = shadowLight.light.shadowNearPlane;

                                for (int k = 0; k < cascadeCount; k++) {
                                    renderingData.cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex,
                                          k, cascadeCount, renderingData.shadowData.mainLightShadowCascadesSplit, urpAsset.mainLightShadowmapResolution, shadowNearPlane, out _, out Matrix4x4 proj,
                                          out _);
                                    Matrix4x4 invProj = proj.inverse;
                                    autoCascadeScales[k] = Mathf.Abs(invProj.MultiplyPoint(ShaderParams.Vector3Back).z - invProj.MultiplyPoint(ShaderParams.Vector3Forward).z) / 100f;
                                }

                                Vector4[] cascadeRectsTemp = cascadeRectsWithPadding[cascadeCount - 1];
                                float texel = 1f / urpAsset.mainLightShadowmapResolution;
                                float padding = texel;
                                for (int c = 0; c < 4; c++) {
                                    Vector4 defaultRect = cascadeRects[cascadeCount - 1][c];
                                    Vector4 rect = cascadeRectsTemp[c];
                                    rect.x = defaultRect.x + padding;
                                    rect.y = defaultRect.y + padding;
                                    rect.z = defaultRect.z - padding;
                                    rect.w = defaultRect.w - padding;
                                    cascadeRectsTemp[c] = rect;
                                }
                                cmd.SetGlobalVectorArray(ShaderParams.UmbraCascadeRects, cascadeRectsTemp);
                                cascadeScales[0] = profile.cascade1Scale * autoCascadeScales[0];
                                cascadeScales[1] = profile.cascade2Scale * autoCascadeScales[1];
                                cascadeScales[2] = profile.cascade3Scale * autoCascadeScales[2];
                                cascadeScales[3] = profile.cascade4Scale * autoCascadeScales[3];
                                cmd.SetGlobalFloatArray(ShaderParams.UmbraCascadeScales, cascadeScales);
                            }

                            if ((UmbraSoftShadows.isDeferred && profile.normalsSource == NormalSource.GBufferNormals) || profile.effectiveNormalsSource == NormalSource.NormalsPass || profile.downsample) {
                                mat.EnableKeyword(ShaderParams.SKW_NORMALS_TEXTURE);
                            }
                            else {
                                mat.DisableKeyword(ShaderParams.SKW_NORMALS_TEXTURE);
                            }

#if !UNITY_WEBGL
                            if (profile.enableContactHardening) {
                                mat.EnableKeyword(ShaderParams.SKW_CONTACT_HARDENING);
                            }
                            else
#endif
                            {
                                mat.DisableKeyword(ShaderParams.SKW_CONTACT_HARDENING);
                            }

                            mat.DisableKeyword(ShaderParams.SKW_LOOP_STEP_X3);
                            mat.DisableKeyword(ShaderParams.SKW_LOOP_STEP_X2);
                            if (profile.loopStepOptimization == LoopStep.x3) {
                                mat.EnableKeyword(ShaderParams.SKW_LOOP_STEP_X3);
                            }
                            else if (profile.loopStepOptimization == LoopStep.x2) {
                                mat.EnableKeyword(ShaderParams.SKW_LOOP_STEP_X2);
                            }

                            if (profile.style == Style.Textured && profile.maskTexture != null) {
                                mat.EnableKeyword(ShaderParams.SKW_MASK_TEXTURE);
                                mat.SetTexture(ShaderParams.MaskTexture, profile.maskTexture);
                            }
                            else {
                                mat.DisableKeyword(ShaderParams.SKW_MASK_TEXTURE);
                            }
                        }

                        // Resolve screen space shadows
                        Blitter.BlitCameraTexture(cmd, m_RenderTarget, shadowsHandle, mat, (int)castShadowsPass);

                        // Blend cascades 0 & 1
                        if (profile.shadowSource == ShadowSource.UmbraShadows) {
                            if (profile.blendCascades && cascadeCount > 1) {
                                cmd.SetGlobalVector(ShaderParams.BlendCascadeData, new Vector4(profile.cascade1BlendingStrength * 100f, profile.cascade2BlendingStrength * 100f, profile.cascade3BlendingStrength * 100f, 1f));
                                Blitter.BlitCameraTexture(cmd, shadowsHandle, shadowsHandle, mat, (int)Pass.CascadeBlending);
                            }
                        }

                        // Add contact shadows
                        if (profile.contactShadows) {
                            if (!settings.debugShadows && profile.actualContactShadowsInjectionPoint == ContactShadowsInjectionPoint.ShadowTexture) {
                                Blitter.BlitCameraTexture(cmd, shadowsHandle, shadowsHandle, mat, (int)Pass.ContactShadows);
                            }
                        }

                        // Downscale depth for upscaler 
                        if (profile.downsample && profile.preserveEdges) {
                            RenderTextureDescriptor downscampledDepthDesc = desc;
                            downscampledDepthDesc.colorFormat = RenderTextureFormat.RFloat;
                            cmd.GetTemporaryRT(ShaderParams.DownsampledDepth, downscampledDepthDesc);
                            FullScreenBlit(cmd, ShaderParams.DownsampledDepth, mat, (int)Pass.DownsampledDepth);
                            mat.EnableKeyword(ShaderParams.SKW_PRESERVE_EDGES);
                        }
                        else {
                            mat.DisableKeyword(ShaderParams.SKW_PRESERVE_EDGES);
                        }

                        if (profile.shadowSource == ShadowSource.UnityShadows) {
                            if (profile.downsample && profile.preserveEdges) {
                                // upscale
                                FullScreenBlit(cmd, m_DownscaledRenderTarget, m_RenderTarget, mat, (int)Pass.ComposeUnity);
                            }
                        }
                        else {
                            if (profile.style == Style.Default && profile.blurIterations > 0) {
                                cmd.SetGlobalFloat(ShaderParams.BlurSpread, profile.blurSpread);

                                // perform blur
                                cmd.GetTemporaryRT(ShaderParams.BlurTemp, desc);
                                cmd.GetTemporaryRT(ShaderParams.BlurTemp2, desc);
                                cmd.SetGlobalFloat(ShaderParams.BlurScale, 1f);
                                RenderTargetIdentifier shadowsRT = shadowsHandle;
                                RenderTargetIdentifier blurredRT = ShaderParams.BlurTemp2;
                                if (profile.blurType == BlurType.Box) {
                                    for (int k = 0; k < profile.blurIterations; k++) {
                                        blurredRT = (k % 2) == 0 ? ShaderParams.BlurTemp2 : ShaderParams.BlurTemp;
                                        FullScreenBlit(cmd, shadowsRT, blurredRT, mat, (int)Pass.BoxBlur);
                                        shadowsRT = blurredRT;
                                        cmd.SetGlobalFloat(ShaderParams.BlurScale, k + 1f);
                                    }
                                }
                                else {
                                    if (profile.blurType == BlurType.Gaussian15) {
                                        mat.EnableKeyword(ShaderParams.SKW_BLUR_HQ);
                                    }
                                    else {
                                        mat.DisableKeyword(ShaderParams.SKW_BLUR_HQ);
                                    }
                                    FullScreenBlit(cmd, shadowsRT, ShaderParams.BlurTemp, mat, (int)Pass.BlurHoriz);
                                    cmd.SetGlobalFloat(ShaderParams.BlurScale, 1f);
                                    for (int k = 0; k < profile.blurIterations - 1; k++) {
                                        FullScreenBlit(cmd, ShaderParams.BlurTemp, ShaderParams.BlurTemp2, mat, (int)Pass.BlurVert);
                                        cmd.SetGlobalFloat(ShaderParams.BlurScale, k + 2f);
                                        FullScreenBlit(cmd, ShaderParams.BlurTemp2, ShaderParams.BlurTemp, mat, (int)Pass.BlurHoriz);
                                    }
                                    FullScreenBlit(cmd, ShaderParams.BlurTemp, ShaderParams.BlurTemp2, mat, (int)Pass.BlurVert);
                                }

                                // blit blurred shadows
                                FullScreenBlit(cmd, blurredRT, m_RenderTarget, mat, (int)Pass.ComposeWithBlending);
                            }
                            else if (profile.downsample && profile.preserveEdges) {
                                // upscale
                                FullScreenBlit(cmd, m_DownscaledRenderTarget, m_RenderTarget, mat, (int)Pass.Compose);
                            }
                        }
                    }

                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, false);
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, false);
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowScreen, true);
                }
            }
#endif

            static Mesh _fullScreenMesh;

            static Mesh fullscreenMesh {
                get {
                    if (_fullScreenMesh != null) {
                        return _fullScreenMesh;
                    }
                    float num = 1f;
                    float num2 = 0f;
                    Mesh val = new Mesh();
                    _fullScreenMesh = val;
                    _fullScreenMesh.SetVertices(new List<Vector3> {
            new Vector3 (-1f, -1f, 0f),
            new Vector3 (-1f, 1f, 0f),
            new Vector3 (1f, -1f, 0f),
            new Vector3 (1f, 1f, 0f)
        });
                    _fullScreenMesh.SetUVs(0, new List<Vector2> {
            new Vector2 (0f, num2),
            new Vector2 (0f, num),
            new Vector2 (1f, num2),
            new Vector2 (1f, num)
        });
                    _fullScreenMesh.SetIndices(new int[6] { 0, 1, 2, 2, 1, 3 }, (MeshTopology)0, 0, false);
                    _fullScreenMesh.UploadMeshData(true);
                    return _fullScreenMesh;
                }
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier destination, Material material, int passIndex) {
                destination = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetRenderTarget(destination);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex) {
                destination = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetRenderTarget(destination);
                cmd.SetGlobalTexture(ShaderParams.MainTex, source);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
            }

        }

        private partial class UmbraScreenSpaceShadowsPostPass : ScriptableRenderPass {

            // Profiling tag
            private static string m_ProfilerTag = "Umbra Screen Space Shadows Post Pass";
            private static ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
            private static readonly RTHandle k_CurrentActive = RTHandles.Alloc(BuiltinRenderTextureType.CurrentActive);

#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                ConfigureTarget(k_CurrentActive);
            }

#endif
#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                var cmd = renderingData.commandBuffer;
                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    ShadowData shadowData = renderingData.shadowData;
                    int cascadesCount = shadowData.mainLightShadowCascadesCount;
                    bool mainLightShadows = renderingData.shadowData.supportsMainLightShadows;
                    bool receiveShadowsNoCascade = mainLightShadows && cascadesCount == 1;
                    bool receiveShadowsCascades = mainLightShadows && cascadesCount > 1;

                    // Before transparent object pass, force to disable screen space shadow of main light
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowScreen, false);

                    // then enable main light shadows with or without cascades
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, receiveShadowsNoCascade);
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, receiveShadowsCascades);
                }
            }
#endif
        }


        private partial class UmbraDebugPass : ScriptableRenderPass {

            // Profiling tag
            private static string m_ProfilerTag = "Umbra Debug Pass";
            private static ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);

            RTHandle source;
            static UmbraScreenSpaceShadowsPass shadowPass;

            public void Setup (UmbraScreenSpaceShadowsPass shadowPass) {
                UmbraDebugPass.shadowPass = shadowPass;
                if (settings.debugShadows) {
                    ConfigureInput(ScriptableRenderPassInput.Depth);
                }
            }

#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup (CommandBuffer cmd, ref RenderingData renderingData) {
                source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            }

#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                Material mat = UmbraScreenSpaceShadowsPass.mat;
                if (mat == null) return;

                RTHandle shadows = null;
                Camera cam = renderingData.cameraData.camera;
                Dictionary<Camera, RTHandle> shadowTextures = shadowPass.shadowTextures;
                if (shadowTextures != null) {
                    shadows = shadowTextures[cam];
                }

                if (shadows == null) return;

                var cmd = renderingData.commandBuffer;
                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    Blitter.BlitCameraTexture(cmd, shadows, source, mat, (int)Pass.DebugShadows);
                    if (settings.debugShadows && settings.profile != null && settings.profile.contactShadows) {
                        Blitter.BlitCameraTexture(cmd, source, source, mat, (int)Pass.ContactShadowsAfterOpaque);
                    }
                }
            }
#endif
        }

        private partial class UmbraContactShadowsAfterOpaquePass : ScriptableRenderPass {

            // Profiling tag
            private static string m_ProfilerTag = "Umbra Contact Shadows After Opaque Pass";
            private static ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);

            RTHandle source;

            public void Setup () {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup (CommandBuffer cmd, ref RenderingData renderingData) {
                source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                ConfigureTarget(source);
            }

#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                Material mat = UmbraScreenSpaceShadowsPass.mat;
                if (mat == null || source.rt == null) return;
                var cmd = renderingData.commandBuffer;
                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    UmbraProfile profile = settings.profile;
                    if (profile.transparentReceiverPlane) {
                        cmd.SetGlobalFloat(ShaderParams.ReceiverPlaneAltitude, profile.receiverPlaneAltitude);
                    }
                    cmd.SetGlobalVector(ShaderParams.SourceSize, new Vector4(source.rt.width, source.rt.height, 0, 0));
                    Blitter.BlitCameraTexture(cmd, source, source, mat, (int)Pass.ContactShadowsAfterOpaque);
                }
            }
#endif
        }


        private partial class UmbraOverlayShadows : ScriptableRenderPass {

            // Profiling tag
            private static string m_ProfilerTag = "Umbra Overlay Shadows";
            private static ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);

            RTHandle source;

#if !UNITY_6000_3_OR_NEWER
#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void OnCameraSetup (CommandBuffer cmd, ref RenderingData renderingData) {
                source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                ConfigureTarget(source);
            }

#if UNITY_2023_3_OR_NEWER
            [Obsolete]
#endif
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                Material mat = UmbraScreenSpaceShadowsPass.mat;
                if (mat == null || source == null) return;
                var cmd = renderingData.commandBuffer;
                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    Color shadowColor = settings.profile.overlayShadowsColor;
                    shadowColor.a = settings.profile.overlayShadowsIntensity;
                    mat.SetColor(ShaderParams.OverlayShadowColor, shadowColor);
                    Blitter.BlitCameraTexture(cmd, source, source, mat, (int)Pass.OverlayShadows);
                }
            }
#endif            
        }

    }
}
