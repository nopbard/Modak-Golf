Shader "MiniGolf/GroundGradient"
{
    Properties
    {
        _ColorTop       ("Top Color",                  Color)         = (0.4, 0.8, 0.2, 1)
        _ColorBottom    ("Bottom Color",               Color)         = (0.6, 0.45, 0.1, 1)
        _Power          ("Gradient Power",             Range(0.1, 5)) = 1.0
        _ShadowStrength ("Shadow Strength",            Range(0, 1))   = 0.6
        _AmbientBoost   ("Ambient / Shadow Min Light", Range(0, 1))   = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 100

        // ─────────────────────────────────────────────────────────────────
        //  Forward Lit
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // multi_compile: URP가 전역으로 켜고 끄는 키워드 → 반드시 multi_compile 사용.
            // 섀도 cascade: 3 variants (없음 / 기본 / cascade / screen-space)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // 소프트 섀도: 2 variants. LOW/MEDIUM/HIGH 품질 단계는 URP 13+ 신규 키워드인데
            // WebGL에서 변형 수를 줄이려면 단순 _SHADOWS_SOFT 만 선언하는 것이 낫다.
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                // w를 fragment에서 나눠야 퍼스펙티브 보정이 됨 — float4 그대로 보간
                float4 screenPos   : TEXCOORD0;
                // shadow coord는 fragment에서 cascade 선택 → world pos 필요
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _ColorTop;
                half4  _ColorBottom;
                float  _Power;
                float  _ShadowStrength;
                float  _AmbientBoost;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.screenPos   = ComputeScreenPos(p.positionCS);
                OUT.positionWS  = p.positionWS;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // ── 화면 공간 그라디언트 ────────────────────────────────
                // fragment에서 /w: 퍼스펙티브 보정, 플랫폼 y-flip 자동 처리
                float screenY = IN.screenPos.y / IN.screenPos.w; // 0=하단, 1=상단
                float t       = pow(saturate(screenY), _Power);
                half4 color   = lerp(_ColorBottom, _ColorTop, t);

                // ── 그림자 수신 ─────────────────────────────────────────
                // fragment에서 계산해야 cascade 경계 오차 없음
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight   = GetMainLight(shadowCoord);
                float shadowAtten = lerp(1.0, max(mainLight.shadowAttenuation, _AmbientBoost), _ShadowStrength);
                color.rgb        *= shadowAtten;

                return color;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────
        //  Shadow Caster
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                OUT.positionHCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────
        //  Depth Only
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(Varyings IN) : SV_Target { return IN.positionHCS.z; }
            ENDHLSL
        }
    }
}
