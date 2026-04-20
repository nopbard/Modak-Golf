Shader "Hidden/Kronnect/UmbraScreenSpaceShadows"
{
    Properties {
        // _MainTex ("Main Tex", 2D) = "white" {}
        // _Color ("Color", Color) = (1, 1, 1, 1)
        [NoScaleoffset] _NoiseTex("Noise Tex", 2D) = "white" {}
        _ContactShadowsBlend ("Contact Shadows Blend", Int) = 10 // OneMinusSrcAlpha
    }
    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #pragma target 3.0

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        #include "Common.hlsl"

        ENDHLSL

        Pass
        {
            Name "Umbra Shadows"
            HLSLPROGRAM
            #pragma multi_compile _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_local_fragment _ _NORMALS_TEXTURE
            #pragma multi_compile_local_fragment _ _CONTACT_HARDENING
            #pragma multi_compile_local_fragment _ _LOOP_STEP_X2 _LOOP_STEP_X3
            #pragma multi_compile_local_fragment _ _MASK_TEXTURE
            #pragma multi_compile_local_fragment _ _RECEIVER_PLANE
            #pragma vertex Vert
            #pragma fragment FragCast
            #include "ShadowCast.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Blur Horiz"
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _BLUR_HQ
            #pragma multi_compile_local_fragment _ _CONTACT_HARDENING
            #pragma vertex VertBlur
            #pragma fragment FragBlurGaussian
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Blur Vert"
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _BLUR_HQ
            #pragma multi_compile_local_fragment _ _CONTACT_HARDENING
            #pragma vertex VertBlur
            #pragma fragment FragBlurGaussian
            #define BLUR_VERT
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Box Blur"
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _CONTACT_HARDENING
            #pragma vertex VertSimple
            #pragma fragment FragBlurBox
            #define BOX_BLUR
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Compose with Blending"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _PRESERVE_EDGES
            #pragma multi_compile_local_fragment _ _NORMALS_TEXTURE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma vertex VertSimple
            #pragma fragment FragCompose
            #define COMPOSE_WITH_BLENDING
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Downsample Depth"
            HLSLPROGRAM
            #pragma vertex VertSimple
            #pragma fragment FragDownsampleDepth
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Umbra Cascade Blending"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma multi_compile _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_local_fragment _ _NORMALS_TEXTURE
            #pragma multi_compile_local_fragment _ _CONTACT_HARDENING
            #pragma multi_compile_local_fragment _ _LOOP_STEP_X2 _LOOP_STEP_X3
            #pragma multi_compile_local_fragment _ _MASK_TEXTURE
            #define _BLEND_CASCADE
            #pragma vertex Vert
            #pragma fragment FragCast
            #include "ShadowCast.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ScreenSpaceShadows"
            HLSLPROGRAM
            #pragma multi_compile _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma vertex   Vert
            #pragma fragment FragUnityShadows
            #include "ShadowCast.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Compose Unity Shadows"
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _PRESERVE_EDGES
            #pragma vertex VertSimple
            #pragma fragment FragComposeUnity
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Contact Shadows"
            Blend One One
            BlendOp Min
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragContactShadows
            #pragma multi_compile_local_fragment _ _LOOP_STEP_X2 _LOOP_STEP_X3
            #pragma multi_compile_local_fragment _ _NORMALS_TEXTURE
            #pragma multi_compile_local_fragment _ _RECEIVER_PLANE
            #pragma multi_compile_local_fragment _ _USE_POINT_LIGHT
            #pragma multi_compile_local_fragment _ _SOFT_EDGES
            #include "ContactShadows.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Compose"
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _PRESERVE_EDGES
            #pragma vertex VertSimple
            #pragma fragment FragCompose
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Debug Shadows"
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _PRESERVE_EDGES
            #pragma vertex Vert
            #pragma fragment FragDebugShadows
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Contact Shadows After Opaque"
            Blend SrcAlpha [_ContactShadowsBlend]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragContactShadows
            #pragma multi_compile_local_fragment _ _LOOP_STEP_X2 _LOOP_STEP_X3
            #pragma multi_compile_local_fragment _ _NORMALS_TEXTURE
            #pragma multi_compile_local_fragment _ _RECEIVER_PLANE
            #pragma multi_compile_local_fragment _ _USE_POINT_LIGHT
            #pragma multi_compile_local_fragment _ _SOFT_EDGES
            #define CONTACT_SHADOWS_AFTER_OPAQUE
            #include "ContactShadows.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Overlay Shadows"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOverlayShadows
            #include "ShadowBlur.hlsl"
            ENDHLSL
        }        

    
    }
}
