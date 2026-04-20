using UnityEngine;

namespace Umbra {

    public enum ShadowSource {
        UnityShadows,
        UmbraShadows,
        OnlyContactShadows
    }

    public enum ContactShadowsInjectionPoint {
        ShadowTexture,
        AfterOpaque
    }

    public enum UmbraPreset {
        [InspectorName("")]
        None = 0,
        Hard = 10,
        Soft = 20,
        Smooth = 30,
        ExtraSmooth = 40,
        Blurred = 50,
        Fast = 60
    }

    public enum BlurType {
        [InspectorName("Gaussian (15 taps, 2 passes)")]
        Gaussian15 = 10,
        [InspectorName("Gaussian (5 taps, 2 passes)")]
        Gaussian5 = 20,
        [InspectorName("Box Blur (9 taps, 1 pass)")]
        Box = 30
    }

    public enum NormalSource {
        ReconstructFromDepth,
        NormalsPass,
        [InspectorName("GBuffer Normals")]
        GBufferNormals
    }

    public enum LoopStep {
        Default,
        x2,
        x3
    }

    public enum Style {
        Default,
        Textured
    }


    [CreateAssetMenu(fileName = "UmbraProfile", menuName = "Umbra Profile", order = 251)]
    [HelpURL("https://kronnect.com/docs/umbra/")]
    public class UmbraProfile : ScriptableObject {

        [Tooltip("Which shadow generation system should be used")]
        public ShadowSource shadowSource = ShadowSource.UmbraShadows;

        [Tooltip("Translates to the number of shadow map samples used to resolve shadows")]
        [Range(1, 64)]
        public int sampleCount = 16;

        [Tooltip("Number of samples to early out if shadow is already fully resolved")]
        [Range(1, 64)]
        public int earlyOutSamples = 32;

        [Tooltip("Size of the directional light which influences the penumbra size")]
        [Range(0f, 32)]
        public float lightSize = 6;

        [Tooltip("Makes shadows sharper nearer to the occluder")]
        public bool enableContactHardening = true;

        [Tooltip("Makes shadows sharper and stronger near the occluder")]
        [Range(0, 1)]
        public float contactStrength = 0.5f;

        [Tooltip("Usual value is 5. Modify to alter the shadow edge appearance.")]
        public float contactStrengthKnee = 0.0001f;

        [Tooltip("Makes shadows smoother when far from occluder")]
        [Range(1, 16)]
        public float distantSpread = 1f;

        [Tooltip("Number of shadow map samples to find occluders which improve the penumbra radius calculation")]
        [Range(1, 64)]
        public int occludersCount = 8;

        [Tooltip("Multiplier to the occluder search radius in texture space. A higher value creates more diffused penumbra.")]
        public float occludersSearchRadius = 8f;

        [Tooltip("Number of blur passes. Each blur pass increases shadow softness but reduces performance.")]
        [Range(0, 3)]
        public int blurIterations;

        [Tooltip("Specifies the blur method. Gaussian blur uses two passes per iteration and produces smoother results.")]
        public BlurType blurType = BlurType.Gaussian15;

        [Tooltip("Blur kerner radius multiplier. Improves softness but can introduce artifacts")]
        [Range(0.5f, 5)]
        public float blurSpread = 1f;

        [Tooltip("Increases the contrast on the shadow border to create sharper shadows when blur is used")]
        [Range(0, 1)]
        public float blurEdgeSharpness;

        [Tooltip("Strength of the edge weight when blurring")]
        public float blurEdgeTolerance = 5f;

        [Tooltip("Reduces blur on distance from camera")]
        public float blurDepthAttenStart = 50;

        [Tooltip("Blur reduction intensity")]
        public float blurDepthAttenLength = 50;

        [Tooltip("Blur reduction when viewing shadow from a grazing angle")]
        [Range(0, 1)]
        public float blurGrazingAttenuation;

        public bool blendCascades;

        [Range(1, 10)]
        public int posterization = 1;

        public float cascade1BlendingStrength = 0.1f;
        public float cascade2BlendingStrength = 0.1f;
        public float cascade3BlendingStrength = 0.1f;

        [Tooltip("Manual adjustment of shadow smoothness multiplier for this cascade")]
        public float cascade1Scale = 1f;
        [Tooltip("Manual adjustment of shadow smoothness multiplier for this cascade")]
        public float cascade2Scale = 1f;
        [Tooltip("Manual adjustment of shadow smoothness multiplier for this cascade")]
        public float cascade3Scale = 1f;
        [Tooltip("Manual adjustment of shadow smoothness multiplier for this cascade")]
        public float cascade4Scale = 1f;

        [Tooltip("Method used to obtain surface normals. Reconstruct from depth is fastest, normals pass is more accurate, and GBuffer normals (deferred only) uses existing GBuffer data.")]
        public NormalSource normalsSource = NormalSource.ReconstructFromDepth;

        /// <summary>
        /// Gets the effective normals source, falling back to ReconstructFromDepth when GBufferNormals is selected but not in deferred mode
        /// </summary>
        public NormalSource effectiveNormalsSource => 
            normalsSource == NormalSource.GBufferNormals && !UmbraSoftShadows.isDeferred 
                ? NormalSource.ReconstructFromDepth 
                : normalsSource;

        [Tooltip("Reduces the number of samples while keeping the shadow size")]
        public LoopStep loopStepOptimization = LoopStep.Default;

        [Tooltip("Resolves shadowmap every two frames, reusing the result from previous frame")]
        public bool frameSkipOptimization;

        [Tooltip("Distance threshold - if camera has moved this distance from previous frame, ignore cached shadowmap")]
        public float skipFrameMaxCameraDisplacement = 0.1f;

        [Tooltip("Rotation threshold - if camera has rotated this angle from previous frame, ignore cached shadowmap")]
        public float skipFrameMaxCameraRotation = 5f;

        [Tooltip("Resolves screen space shadows in a reduced buffer of half of screen size")]
        public bool downsample;

        [Tooltip("Forces a depth prepass so depth and normals are available even for forward-only materials (useful in Deferred when using forward only materials).")]
        public bool forceDepthPrepass;

        [Tooltip("Prevents shadow blurring on geometry edges")]
        public bool preserveEdges = true;

        [Tooltip("Stylized look for shadows")]
        public Style style = Style.Default;

        [Tooltip("Optional mask texture to create stylized shadows")]
        public Texture2D maskTexture;

        public float maskScale = 1f;

        public bool contactShadows;

        [Range(0, 1)]
        public float contactShadowsIntensityMultiplier = 0.85f;

        public ContactShadowsInjectionPoint contactShadowsInjectionPoint = ContactShadowsInjectionPoint.ShadowTexture;

        public ContactShadowsInjectionPoint actualContactShadowsInjectionPoint => shadowSource == ShadowSource.OnlyContactShadows || contactShadowsInjectionPoint == ContactShadowsInjectionPoint.AfterOpaque ? ContactShadowsInjectionPoint.AfterOpaque : contactShadowsInjectionPoint;

        [Range(1, 64)]
        public int contactShadowsSampleCount = 16;

        public float contactShadowsStepping = 0.01f;

        public float contactShadowsThicknessNear = 0.5f;

        public float contactShadowsThicknessDistanceMultiplier;

        public float contactShadowsJitter = 0.3f;

        [Range(0, 1)]
        [Tooltip("Attenuates shadow intensity with distance to occluder")]
        public float contactShadowsDistanceFade = 0.75f;

        public float contactShadowsStartDistance;
        public float contactShadowsStartDistanceFade = 0.01f;

        [Tooltip("Adds an offset to the pixel position to avoid self-occlusion")]
        [Range(0.0001f, 0.25f)]
        public float contactShadowsNormalBias = 0.1f;

        [Tooltip("Attenuates contact shadows on the edges of screen")]
        [Range(0, 0.5f)]
        public float contactShadowsVignetteSize = 0.15f;

        [Tooltip("Adds an offset to the pixel position to avoid self-occlusion")]
        [Range(0f, 1f)]
        public float contactShadowsBias = 0.001f;

        [Tooltip("Bias applied at far distances. Use only if self-shadowing occurs on certain surfaces.")]
        [Range(0, 1)]
        public float contactShadowsBiasFar = 0.4f;        

        [Tooltip("Softens the edges of contact shadows. Higher values create softer edges.")]
        [Range(0.01f, 0.5f)]
        public float contactShadowsEdgeSoftness = 0.1f;

        [Tooltip("Enables soft edges for contact shadows. When disabled, shadows have hard edges for better performance.")]
        public bool contactShadowsSoftEdges;

        [Tooltip("Makes contact shadows planar by ignoring the Y component of the light direction. Useful for ground shadows.")]
        public bool contactShadowsPlanarShadows;

        [Tooltip("Adds an extra pass after opaque with custom colored shadows")]
        public bool overlayShadows;
        public float overlayShadowsIntensity = 0.5f;
        [ColorUsage(showAlpha: false, hdr: false)]
        public Color overlayShadowsColor = Color.black;

        [Tooltip("Enables a receiver plane to cast shadows from transparent objects. Check demo scene Transparent Receiver for an example.")]
        public bool transparentReceiverPlane;
        public float receiverPlaneAltitude;


        void OnValidate() {
            blurDepthAttenLength = Mathf.Max(0.001f, blurDepthAttenLength);
            blurEdgeTolerance = Mathf.Max(0, blurEdgeTolerance);
            blurDepthAttenStart = Mathf.Max(0, blurDepthAttenStart);
            blurDepthAttenLength = Mathf.Max(0, blurDepthAttenLength);
            blurGrazingAttenuation = Mathf.Max(0, blurGrazingAttenuation);
            occludersSearchRadius = Mathf.Max(0.01f, occludersSearchRadius);
            contactStrengthKnee = Mathf.Max(0.0001f, contactStrengthKnee);
            cascade1BlendingStrength = Mathf.Max(0.01f, cascade1BlendingStrength);
            cascade2BlendingStrength = Mathf.Max(0.01f, cascade2BlendingStrength);
            cascade3BlendingStrength = Mathf.Max(0.01f, cascade3BlendingStrength);
            cascade1Scale = Mathf.Max(0, cascade1Scale);
            cascade2Scale = Mathf.Max(0, cascade2Scale);
            cascade3Scale = Mathf.Max(0, cascade3Scale);
            cascade4Scale = Mathf.Max(0, cascade4Scale);
            maskScale = Mathf.Max(maskScale, 0);
            skipFrameMaxCameraRotation = Mathf.Max(skipFrameMaxCameraRotation, 0);
            skipFrameMaxCameraDisplacement = Mathf.Max(skipFrameMaxCameraDisplacement, 0);
            contactShadowsStepping = Mathf.Max(0.0001f, contactShadowsStepping);
            contactShadowsThicknessNear = Mathf.Max(0.0f, contactShadowsThicknessNear);
            contactShadowsThicknessDistanceMultiplier = Mathf.Max(0.0f, contactShadowsThicknessDistanceMultiplier);
            contactShadowsJitter = Mathf.Max(0f, contactShadowsJitter);
            contactShadowsStartDistance = Mathf.Max(0, contactShadowsStartDistance);
            contactShadowsStartDistanceFade = Mathf.Max(0.00001f, contactShadowsStartDistanceFade);
            overlayShadowsIntensity = Mathf.Max(0, overlayShadowsIntensity);
        }


        public void ApplyPreset(UmbraPreset preset) {
            shadowSource = ShadowSource.UmbraShadows;
            switch (preset) {
                case UmbraPreset.Fast:
                    sampleCount = 12;
                    lightSize = 12;
                    occludersCount = 6;
                    occludersSearchRadius = 5f;
                    enableContactHardening = true;
                    contactStrength = 0.5f;
                    contactStrengthKnee = 0.0001f;
                    blurIterations = 0;
                    distantSpread = 1f;
                    break;
                case UmbraPreset.Hard:
                    sampleCount = 1;
                    lightSize = 0.5f;
                    enableContactHardening = false;
                    blurIterations = 1;
                    blurType = BlurType.Box;
                    blurSpread = 1f;
                    blurEdgeTolerance = 5f;
                    blurEdgeSharpness = 1f;
                    blurDepthAttenStart = 75;
                    blurDepthAttenLength = 50;
                    blurGrazingAttenuation = 0;
                    break;
                case UmbraPreset.Soft:
                    sampleCount = 16;
                    lightSize = 8;
                    enableContactHardening = true;
                    occludersCount = 8;
                    occludersSearchRadius = 8f;
                    contactStrength = 0.5f;
                    contactStrengthKnee = 0.0001f;
                    distantSpread = 1f;
                    blurIterations = 0;
                    break;
                case UmbraPreset.Smooth:
                    sampleCount = 16;
                    lightSize = 13;
                    enableContactHardening = true;
                    occludersCount = 9;
                    occludersSearchRadius = 8f;
                    contactStrength = 0.5f;
                    contactStrengthKnee = 0.0001f;
                    distantSpread = 1.2f;
                    blurIterations = 0;
                    break;
                case UmbraPreset.ExtraSmooth:
                    sampleCount = 20;
                    lightSize = 16;
                    enableContactHardening = true;
                    occludersCount = 16;
                    occludersSearchRadius = 10;
                    contactStrength = 0.8f;
                    contactStrengthKnee = 0.0001f;
                    distantSpread = 1.4f;
                    blurIterations = 0;
                    break;
                case UmbraPreset.Blurred:
                    sampleCount = 32;
                    lightSize = 16;
                    enableContactHardening = true;
                    occludersCount = 24;
                    occludersSearchRadius = 16;
                    contactStrength = 0.4f;
                    contactStrengthKnee = 0.0001f;
                    blurIterations = 0;
                    distantSpread = 2f;
                    break;
            }
        }

    }



}