// Lightweight URP Toon Shader — WebGL2 compatible, single file
// Based on ColinLeung-NiloCat/UnityURPToonLitShaderExample (MIT)
// - 텍스처(_BaseMap) 그대로 유지, 위에 셀 셰이딩만 얹음
// - Screen-space shadows + PCF all quality tiers → WebGL 블록 현상 방지
// - ShadowCaster / DepthOnly 패스 포함
Shader "MiniGolf/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Base Texture", 2D)    = "white" {}
        [MainColor]   _BaseColor ("Base Color",   Color) = (1,1,1,1)

        [Header(Cel Shading)]
        _ShadowMidpoint ("Shadow Midpoint", Range(0,1)) = 0.4
        _ShadowSoftness ("Shadow Softness", Range(0,1)) = 0.05
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.5
        _AmbientMin     ("Ambient Minimum",  Range(0,1)) = 0.2
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

        // ── Forward Lit ───────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Screen-space shadows 포함 — WebGL PCF 블록 현상 해소
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // URP 품질 단계별 PCF 커널 (HIGH = 가장 부드러운 탭)
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float  _ShadowMidpoint;
                float  _ShadowSoftness;
                float  _ShadowStrength;
                float  _AmbientMin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                // Screen-space shadow lookup용 — _w 그대로 보간, fragment에서 나눔
                float4 screenPos   : TEXCOORD3;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                // ComputeScreenPos: /w 전 전체 float4 — GetMainLight 내부에서 나눔
                OUT.screenPos   = ComputeScreenPos(p.positionCS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float3 normalWS = normalize(IN.normalWS);

                // Screen-space shadow 모드면 screenPos, 아니면 world→shadow map 변환
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = IN.screenPos;
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);

                // ── 셀 셰이딩 (NdotL 기반 자기 그림자) ──────────────────
                float NdotL   = dot(normalWS, normalize(mainLight.direction));
                float celMask = smoothstep(
                    _ShadowMidpoint - _ShadowSoftness,
                    _ShadowMidpoint + _ShadowSoftness,
                    NdotL);
                float celLight = lerp(1.0 - _ShadowStrength, 1.0, celMask);

                // ── 투영 그림자 (shadow map / screen-space PCF) ──────────
                // cast shadow를 셀보다 약간 부드럽게 블렌딩해 블록 느낌 감소
                float castShadow = lerp(1.0 - _ShadowStrength * 0.75, 1.0,
                                        mainLight.shadowAttenuation);

                // 두 항목 중 어두운 쪽을 채택 → ambient 최소값 보정
                float light = min(celLight, castShadow);
                light = max(light, _AmbientMin);

                return half4(albedo.rgb * light, albedo.a);
            }
            ENDHLSL
        }

        // ── Shadow Caster ─────────────────────────────────────────────────
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float  _ShadowMidpoint;
                float  _ShadowSoftness;
                float  _ShadowStrength;
                float  _AmbientMin;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
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

        // ── Depth Only ────────────────────────────────────────────────────
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float  _ShadowMidpoint;
                float  _ShadowSoftness;
                float  _ShadowStrength;
                float  _AmbientMin;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(Varyings IN) : SV_Target { return IN.positionHCS.z; }
            ENDHLSL
        }
    }
}
