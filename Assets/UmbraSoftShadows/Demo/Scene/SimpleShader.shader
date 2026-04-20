Shader "Kronnect/UmbraScreenSpaceShadows/SimpleShader"
{
	Properties
	{
		[MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
		[MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
		LOD 100

		Pass
		{
			Name "UniversalForward"
			Tags { "LightMode"="UniversalForward" }
			Cull Back
			ZWrite On
			ZTest LEqual
			Blend One Zero

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 2.0
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);
			
			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				float4 _BaseColor;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float4 shadowCoord : TEXCOORD0;
				float2 uv : TEXCOORD1;
			};

			Varyings Vertex(Attributes input)
			{
				Varyings output;
				VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionHCS = posInputs.positionCS;
				output.shadowCoord = GetShadowCoord(posInputs);
				output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
				return output;
			}

			half4 Fragment(Varyings input) : SV_Target
			{
				half shadowAtten = MainLightRealtimeShadow(input.shadowCoord);
				half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
				return _BaseColor * baseMap * shadowAtten;
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }
			Cull Back
			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
			#pragma target 2.0
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormals" }
			Cull Back
			ZWrite On

			HLSLPROGRAM
			#pragma target 2.0
			#pragma vertex DepthNormalsVertex
			#pragma fragment DepthNormalsFragment
			
			#include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitDepthNormalsPass.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			Cull Back
			ZWrite On

			HLSLPROGRAM
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment
			#pragma target 2.0
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
			ENDHLSL
		}
	}

	Fallback "Hidden/Universal Render Pipeline/FallbackError"
}



