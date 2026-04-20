#pragma once

TEXTURE2D_X(_MainTex);
float4 _MainTex_ST;
float4 _MainTex_TexelSize;

float4 _ShadowData;
#define SHADOW_SAMPLES _ShadowData.x
#define POSTERIZATION _ShadowData.y
#define DEPTH_REJECTION _ShadowData.z
#define SHADOW_MAX_BLUR_DEPTH _ShadowData.w
       
float4 _ShadowData2;
#define CONTACT_STRENGTH  _ShadowData2.x
#define MAX_SHADOW_SPREAD _ShadowData2.y
#define SHADOW_MAX_DEPTH  _ShadowData2.z
#define LIGHT_SIZE        _ShadowData2.w

float4 _ShadowData3;
#define BLUR_DEPTH_ATTEN_START _ShadowData3.x
#define BLUR_DEPTH_ATTEN_LENGTH _ShadowData3.y
#define BLUR_GRAZING_ATTEN_STRENGTH _ShadowData3.z
#define BLUR_EDGE_SHARPNESS _ShadowData3.w

float _BlurSpread;
#define BLUR_SPREAD _BlurSpread

float4 _ShadowData4;
#define OCCLUDERS_COUNT _ShadowData4.x
#define OCCLUDERS_SEARCH_RADIUS _ShadowData4.y
#define CONTACT_STRENGTH_KNEE _ShadowData4.z
#define MASK_SCALE _ShadowData4.w

float _BlurScale;

float4 _BlendCascadeData;
#define BLEND_CASCADE_DATA _BlendCascadeData

float2 _SourceSize;

float _ReceiverPlaneAltitude;

int _EarlyOutSamples;

#define dot2(x) dot(x,x)

struct AttributesSimple {
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct VaryingsSimple {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};


VaryingsSimple VertSimple(AttributesSimple v) {
    VaryingsSimple o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS = v.positionOS;
    o.positionCS.y *= _ProjectionParams.x;
    o.uv = v.uv;
    return o;
}


float GetLinearDepth(float2 uv) {
    float rawDepth = SampleSceneDepth(uv);
    float depthPersp = Linear01Depth(rawDepth, _ZBufferParams);
    float depthOrtho = rawDepth;
    #if UNITY_REVERSED_Z
        depthOrtho = 1.0 - depthOrtho;
    #endif
    float depth = lerp(depthPersp, depthOrtho, unity_OrthoParams.w);
    return depth;
}

float3 GetWorldPosition(float2 uv, float rawDepth) {
    #if UNITY_REVERSED_Z
         float depth = rawDepth;
    #else
         float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
    #endif

    float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
    return worldPos;
}


float3 GetNormalFromWPOS(float3 wpos) {
    float3 dx = ddx(wpos);
    float3 dy = ddy(wpos);
    float3 cr = cross(dy, dx);
    float len = length(cr);
    if (len == 0) {
        return -UNITY_MATRIX_IT_MV[2].xyz;
    }
    return cr / len;
}


float3 GetNormalFromUV(float2 uv, float depth) {
    float3 wpos = GetWorldPosition(uv, depth);
    return GetNormalFromWPOS(wpos);
}

half InvLerp(half b, half t) {
    half oneMinusT = 1 - t;
    return oneMinusT + b * t;
}

bool IsSkyBox(float rawDepth) {
    #if UNITY_REVERSED_Z
        return rawDepth <= 0;
    #else
        return rawDepth >= 1;
    #endif
}

