#if UNITY_2023_3_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

namespace Umbra {

    internal partial class UmbraRenderFeature : ScreenSpaceShadows {

        private partial class UmbraScreenSpaceShadowsPass : ScriptableRenderPass {

            class PassData {
                public UmbraScreenSpaceShadowsPass pass;
                public UniversalShadowData shadowData;
                public UniversalLightData lightData;
                public UniversalCameraData cameraData;
                public UniversalRenderingData renderingData;
                public TextureHandle rtCameraDepthTexture;
                public TextureHandle rtCameraNormalsTexture;
            }

            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {

                if (mat == null) {
                    Debug.LogError("Umbra material not initialized");
                    return;
                }

                using (var builder = renderGraph.AddUnsafePass<PassData>("Umbra Soft Shadows", out var passData, m_ProfilingSampler)) {

                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    desc = cameraData.cameraTargetDescriptor;
                    desc.depthBufferBits = 0;
                    desc.msaaSamples = 1;
                    desc.graphicsFormat = screenShadowTextureFormat;

                    UmbraProfile profile = settings.profile;

                    if (profile.downsample && !profile.preserveEdges) {
                        desc.width /= 2;
                        desc.height /= 2;
                    }

                    Camera cam = cameraData.camera;
#if UNITY_EDITOR
                    if (profile.frameSkipOptimization && Application.isPlaying) {
#else
                    if (profile.frameSkipOptimization) {
#endif
                        newShadowmap = !shadowTextures.TryGetValue(cam, out m_RenderTarget);
                    }

                    if (RenderingUtils.ReAllocateHandleIfNeeded(ref m_RenderTarget, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceShadowmapTexture")) {
                        newShadowmap = true;
                    }
                    if (newShadowmap) {
                        shadowTextures[cam] = m_RenderTarget;
                    }

                    builder.AllowGlobalStateModification(true);

#if UNITY_6000_0_OR_NEWER
                    TextureHandle shadowmap = renderGraph.ImportTexture(m_RenderTarget, new RenderTargetInfo {
                        width = desc.width,
                        height = desc.height,
                        volumeDepth = 1,
                        msaaSamples = 1,
                        format = desc.graphicsFormat
                    });
#else
                    TextureHandle shadowmap = renderGraph.ImportTexture(m_RenderTarget);
#endif
                    builder.UseTexture(shadowmap, AccessFlags.ReadWrite);

                    if (UmbraSoftShadows.isDeferred && resourceData.gBuffer[2].IsValid()) {
                        passData.rtCameraNormalsTexture = resourceData.gBuffer[2];
                        builder.UseTexture(passData.rtCameraNormalsTexture, AccessFlags.Read);
                    }
                    passData.rtCameraDepthTexture = resourceData.cameraDepthTexture;
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    if ((input & ScriptableRenderPassInput.Normal) != 0) {
                        builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                    }
                    passData.pass = this;
                    passData.cameraData = cameraData;
                    passData.lightData = frameData.Get<UniversalLightData>();
                    passData.shadowData = frameData.Get<UniversalShadowData>();
                    passData.renderingData = frameData.Get<UniversalRenderingData>();
                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext rgContext) => {
                        if (data.rtCameraDepthTexture.IsValid()) {
                            mat.SetTexture(ShaderParams.CameraDepthTexture, data.rtCameraDepthTexture);
                        }
                        if (data.rtCameraNormalsTexture.IsValid()) {
                            mat.SetTexture(ShaderParams.CameraNormalsTexture, data.rtCameraNormalsTexture);
                        }
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(rgContext.cmd);
                        cmd.SetGlobalTexture(m_RenderTarget.name, m_RenderTarget.nameID);
                        ExecutePass(cmd, data);
                    });
                }
            }


            static void ExecutePass (CommandBuffer cmd, PassData passData) {

                UmbraProfile profile = settings.profile;
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

                    float farClipPlane = passData.cameraData.camera.farClipPlane;
                    int cascadeCount = passData.shadowData.mainLightShadowCascadesCount;
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
                        }
                        else {
                            mat.DisableKeyword(ShaderParams.SKW_RECEIVER_PLANE);
                        }

                        cmd.SetGlobalVector(ShaderParams.ShadowData, new Vector4(profile.sampleCount, 1024 / Mathf.Pow(2, profile.posterization), profile.blurEdgeTolerance * 1000f, (profile.blurDepthAttenStart + profile.blurDepthAttenLength) / farClipPlane));
                        float shadowMaxDepth = profile.transparentReceiverPlane ? 2 : shadowMaxDistance / farClipPlane;
                        cmd.SetGlobalVector(ShaderParams.ShadowData2, new Vector4(1f - profile.contactStrength, profile.distantSpread, shadowMaxDepth, profile.lightSize * 0.02f));
                        cmd.SetGlobalVector(ShaderParams.ShadowData3, new Vector4(profile.blurDepthAttenStart / farClipPlane, profile.blurDepthAttenLength / farClipPlane, profile.blurGrazingAttenuation, profile.blurEdgeSharpness));
                        cmd.SetGlobalVector(ShaderParams.ShadowData4, new Vector4(profile.occludersCount, profile.occludersSearchRadius * 0.02f, profile.contactStrength > 0 ? profile.contactStrengthKnee * 0.1f : 0.00001f, profile.maskScale));
                        cmd.SetGlobalVector(ShaderParams.SourceSize, new Vector4(desc.width, desc.height, 0, 0));
                        cmd.SetGlobalInt(ShaderParams.EarlyOutSamples, profile.earlyOutSamples);

                        if (cascadeCount > 1) {
                            VisibleLight shadowLight = passData.lightData.visibleLights[shadowLightIndex];
                            float shadowNearPlane = shadowLight.light.shadowNearPlane;

                            for (int k = 0; k < cascadeCount; k++) {
                                passData.renderingData.cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex,
                                      k, cascadeCount, passData.shadowData.mainLightShadowCascadesSplit, urpAsset.mainLightShadowmapResolution, shadowNearPlane, out _, out Matrix4x4 proj,
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
                    if (profile.contactShadows && SetupContactShadowsMaterial(passData.cameraData.camera, profile, mat)) {
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

        private partial class UmbraScreenSpaceShadowsPostPass : ScriptableRenderPass {

            internal class PassData {
                internal UniversalShadowData shadowData;
            }

            private static void ExecutePass (RasterCommandBuffer cmd, UniversalShadowData shadowData) {
                int cascadesCount = shadowData.mainLightShadowCascadesCount;
                bool mainLightShadows = shadowData.supportsMainLightShadows;
                bool receiveShadowsNoCascade = mainLightShadows && cascadesCount == 1;
                bool receiveShadowsCascades = mainLightShadows && cascadesCount > 1;

                // Before transparent object pass, force to disable screen space shadow of main light
                cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowScreen, false);

                // then enable main light shadows with or without cascades
                cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadows, receiveShadowsNoCascade);
                cmd.SetKeyword(ShaderGlobalKeywords.MainLightShadowCascades, receiveShadowsCascades);
            }

            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Umbra Screen Space Shadow Post Pass", out var passData, m_ProfilingSampler)) {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                    TextureHandle color = resourceData.activeColorTexture;
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    passData.shadowData = frameData.Get<UniversalShadowData>();

                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext rgContext) => {
                        ExecutePass(rgContext.cmd, data.shadowData);
                    });
                }
            }
        }

        private partial class UmbraDebugPass : ScriptableRenderPass {

            internal class PassData {
                internal Camera cam;
            }

            private static void ExecutePass (RasterCommandBuffer cmd, Camera cam) {
                Material mat = UmbraScreenSpaceShadowsPass.mat;
                if (mat == null) return;

                RTHandle shadows = null;
                Dictionary<Camera, RTHandle> shadowTextures = shadowPass.shadowTextures;
                if (shadowTextures != null) {
                    shadows = shadowTextures[cam];
                }

                if (shadows == null) return;

                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    Blitter.BlitTexture(cmd, shadows, new Vector4(1, 1, 0, 0), mat, (int)Pass.DebugShadows);
                    if (settings.debugShadows && settings.profile != null && settings.profile.contactShadows) {
                        Blitter.BlitTexture(cmd, shadows, new Vector4(1, 1, 0, 0), mat, (int)Pass.ContactShadowsAfterOpaque);
                    }

                }
            }

            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Umbra Debug Pass", out var passData, m_ProfilingSampler)) {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                    TextureHandle color = resourceData.activeColorTexture;
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    passData.cam = frameData.Get<UniversalCameraData>().camera;

                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => {
                        ExecutePass(rgContext.cmd, data.cam);
                    });
                }
            }
        }


        private partial class UmbraContactShadowsAfterOpaquePass : ScriptableRenderPass {

            internal class PassData {
                internal TextureHandle colorTexture;
                internal TextureHandle depthTexture;
            }

            private static void ExecutePass (RasterCommandBuffer cmd, RTHandle source, RTHandle depth) {
                Material mat = UmbraScreenSpaceShadowsPass.mat;
                if (mat == null) return;

                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    mat.SetTexture(ShaderParams.CameraDepthTexture, depth);
                    UmbraProfile profile = settings.profile;
                    if (profile.transparentReceiverPlane) {
                        cmd.SetGlobalFloat(ShaderParams.ReceiverPlaneAltitude, profile.receiverPlaneAltitude);
                    }
                    cmd.SetGlobalVector(ShaderParams.SourceSize, new Vector4(source.rt.width, source.rt.height, 0, 0));
                    Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), mat, (int)Pass.ContactShadowsAfterOpaque);
                }
            }

            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Umbra Contact Shadows After Opaque Pass", out var passData, m_ProfilingSampler)) {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                    TextureHandle color = resourceData.activeColorTexture;
                    if (UmbraSoftShadows.isDeferred && resourceData.gBuffer[4].IsValid()) {
                        passData.depthTexture = resourceData.gBuffer[4];
                        builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                    }
                    else {
                        passData.depthTexture = resourceData.cameraDepthTexture;
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    passData.colorTexture = color;

                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => {
                        ExecutePass(rgContext.cmd, data.colorTexture, data.depthTexture);
                    });
                }
            }
        }

        private partial class UmbraOverlayShadows : ScriptableRenderPass {

            internal class PassData {
                internal TextureHandle colorTexture;
            }

            private static void ExecutePass (RasterCommandBuffer cmd, RTHandle source) {
                Material mat = UmbraScreenSpaceShadowsPass.mat;
                if (mat == null) return;

                using (new ProfilingScope(cmd, m_ProfilingSampler)) {
                    Color shadowColor = settings.profile.overlayShadowsColor;
                    shadowColor.a = settings.profile.overlayShadowsIntensity;
                    mat.SetColor(ShaderParams.OverlayShadowColor, shadowColor);
                    Blitter.BlitTexture(cmd, source, new Vector4(1, 1, 0, 0), mat, (int)Pass.OverlayShadows);
                }
            }

            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Umbra Overlay Shadows", out var passData, m_ProfilingSampler)) {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                    TextureHandle color = resourceData.activeColorTexture;
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    passData.colorTexture = color;

                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => {
                        ExecutePass(rgContext.cmd, data.colorTexture);
                    });
                }
            }
        }
    }
}
#endif