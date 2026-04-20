TEXTURE2D_X_FLOAT(_DownsampledDepth);
float4 _DownsampledDepth_TexelSize;

half4 _OverlayShadowColor;

	struct VaryingsCross {
	    float4 positionCS : SV_POSITION;
	    float2 uv: TEXCOORD0;
        float2 dir : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
	};

	VaryingsCross VertBlur(AttributesSimple v) {
    	VaryingsCross o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		o.positionCS = v.positionOS;
		o.positionCS.y *= _ProjectionParams.x;
    	o.uv = v.uv;
        #if defined(BLUR_VERT)
            o.dir = float2(0, _MainTex_TexelSize.y * _BlurScale);
        #else
            o.dir = float2(_MainTex_TexelSize.x * _BlurScale, 0);
        #endif
    	return o;
	}

#if _BLUR_HQ
    static const float weights[] = { 0.14446445, 0.13543542, 0.11153505, 0.08055309, 0.05087564, 0.02798160, 0.01332457, 0.00545096 };
    #define SAMPLE_COUNT 7
#else
    static const float weights[] = { 0.2270270270, 0.3162162162, 0.0702702703 };
    #define SAMPLE_COUNT 2
#endif


half2 BlurShadowGaussian(VaryingsCross i, float depth) {

    float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

    half2 shadow = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uv, 0).xy;
    if (depth > SHADOW_MAX_BLUR_DEPTH) return shadow;

    float referenceDepth = 10.0 / _ProjectionParams.z;
    float spread = BLUR_SPREAD * clamp(referenceDepth / depth, 1.0, 10.0);
#if _CONTACT_HARDENING
    spread *= lerp(1, shadow.y, CONTACT_STRENGTH);
#endif
    spread = clamp(spread, 0.5, 10);

    half2 shadowBlur = 0;
    half sum = 0.0000001;

    UNITY_UNROLL
    for (int index = -SAMPLE_COUNT; index <= SAMPLE_COUNT; index++) {
        float2 uv = i.uv + i.dir * (index * spread);
        float2 shadowN = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv), 0).xy;
        float depthN = GetLinearDepth(uv);
        float depthDiff = abs(depthN - depth);
        float r2 = depthDiff * DEPTH_REJECTION;
        float g = exp(-r2 * r2);
        float w = g * weights[abs(index)];
        shadowBlur += w * shadowN;
        sum += w;
    }

    shadowBlur /= sum;

    return shadowBlur;
}

half2 BlurShadowBox(VaryingsSimple i, float depth) {

    float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

    half2 shadow = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uv, 0).xy;
    if (depth > SHADOW_MAX_BLUR_DEPTH) return shadow;

    float referenceDepth = 10.0 / _ProjectionParams.z;
    float spread = BLUR_SPREAD * clamp(referenceDepth / depth, 1.0, 10.0);
#if _CONTACT_HARDENING
    spread *= lerp(1, shadow.y, CONTACT_STRENGTH);
#endif
    spread = clamp(spread, 1, 10);
    
    half2 shadowBlur = 0;
    half sum = 0.0000001;

    float2 blurSize = _MainTex_TexelSize.xy * spread;
    UNITY_UNROLL
    for(int y=-1;y<=1;y++) {
        UNITY_UNROLL
        for (int x = -1; x <= 1; x++) {
            float2 uv = i.uv + float2(x,y) * blurSize;
            float2 shadowN = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv), 0).xy;
            float depthN = GetLinearDepth(uv);
            float depthDiff = abs(depthN - depth);
            float r2 = depthDiff * DEPTH_REJECTION;
            float g = exp(-r2 * r2);
            float w = g;
            shadowBlur += w * shadowN;
            sum += w;
        }
    }

    shadowBlur /= sum;

    return shadowBlur;
}

	half4 FragBlurGaussian (VaryingsCross i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
        float depth = GetLinearDepth(i.uv);
        if (depth >= SHADOW_MAX_DEPTH) return 1;

        half2 shadow = BlurShadowGaussian(i, depth);
   		return half4(shadow, 0, 0);
	}

    half4 FragBlurBox (VaryingsSimple i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
        float depth = GetLinearDepth(i.uv);
        if (depth >= SHADOW_MAX_DEPTH) return 1;

        half2 shadow = BlurShadowBox(i, depth);
   		return half4(shadow, 0, 0);
	}


	half4 FragCompose (VaryingsSimple i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
        float rawDepth = SampleSceneDepth(i.uv);
        float depthPersp = Linear01Depth(rawDepth, _ZBufferParams);
        float depthOrtho = rawDepth;
        #if UNITY_REVERSED_Z
            depthOrtho = 1.0 - depthOrtho;
        #endif
        float depth = lerp(depthPersp, depthOrtho, unity_OrthoParams.w);
        if (depth >= SHADOW_MAX_DEPTH) return 1;

        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

#if _PRESERVE_EDGES
        const float threshold = 0.00005;
        const float t = 0.5;
        float2 uv00 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(-t, -t));
        float2 uv10 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(t, -t));
        float2 uv01 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(-t, t));
        float2 uv11 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(t, t));
        float4 depths;
        depths.x = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv00).r;
        depths.y = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv10).r;
        depths.z = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv01).r;
        depths.w = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv11).r;
        float4 diffs = abs(depth.xxxx - depths);

        float2 minUV = UnityStereoTransformScreenSpaceTex(uv);
        if (any(diffs > threshold)) {
            // Check 10 vs 00
            float minDiff  = lerp(diffs.x, diffs.y, diffs.y < diffs.x);
            minUV    = lerp(uv00, uv10, diffs.y < diffs.x);
            // Check against 01
            minUV    = lerp(minUV, uv01, diffs.z < minDiff);
            minDiff  = lerp(minDiff, diffs.z, diffs.z < minDiff);
            // Check against 11
            minUV    = lerp(minUV, uv11, diffs.w < minDiff);
        }

        half2 shadow = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, minUV).xy;
#else
        half2 shadow = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).xy;
#endif

        // improve shadow edge
#if _CONTACT_HARDENING
        half penumbra = lerp(1.0 - BLUR_EDGE_SHARPNESS, 1.0, shadow.y);
#else
        half penumbra = 1.0 - BLUR_EDGE_SHARPNESS;
#endif
        half u0 = 0.5 - penumbra * 0.5;
        half u1 = 0.5 + penumbra * 0.5;
        // lerp between fine and smooth edge based on distance to occluder
        half edged = smoothstep(u0, u1, shadow.x);
        shadow.x = lerp(shadow.x, edged, BLUR_EDGE_SHARPNESS);

        shadow = round(shadow * POSTERIZATION) / POSTERIZATION;

        half blendingFactor = 1.0;

        #if defined(COMPOSE_WITH_BLENDING)
            // reduce blur with distance
            blendingFactor -= saturate((depth - BLUR_DEPTH_ATTEN_START) / BLUR_DEPTH_ATTEN_LENGTH);

            // under skewed view, reduce blur
            if (BLUR_GRAZING_ATTEN_STRENGTH > 0) {
                float3 wpos = GetWorldPosition(uv, rawDepth);
                float3 viewDir = normalize(wpos - GetCameraPositionWS());
                #if _NORMALS_TEXTURE
                    float3 norm = SampleSceneNormals(uv);
                #else
                    float3 norm = GetNormalFromWPOS(wpos);
                #endif
                blendingFactor *= lerp(1, abs(dot(norm, viewDir)), BLUR_GRAZING_ATTEN_STRENGTH);
            }
        #endif

   		return half4(shadow.x, 0, 0, blendingFactor);
	}	

	float4 FragDownsampleDepth (VaryingsSimple i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float depth = GetLinearDepth(i.uv);
        if (depth >= SHADOW_MAX_DEPTH) return 1;
        return depth;
    }


	half4 FragComposeUnity (VaryingsSimple i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
        float rawDepth = SampleSceneDepth(i.uv);
        #if !_RECEIVER_PLANE
            if (IsSkyBox(rawDepth)) return 1;
        #endif

        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

#if _PRESERVE_EDGES
        float depth = Linear01Depth(rawDepth, _ZBufferParams);
        const float threshold = 0.00005;
        const float t = 0.5;
        float2 uv00 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(-t, -t));
        float2 uv10 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(t, -t));
        float2 uv01 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(-t, t));
        float2 uv11 = UnityStereoTransformScreenSpaceTex(i.uv + _DownsampledDepth_TexelSize.xy * float2(t, t));
        float4 depths;
        depths.x = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv00).r;
        depths.y = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv10).r;
        depths.z = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv01).r;
        depths.w = SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_PointClamp, uv11).r;
        float4 diffs = abs(depth.xxxx - depths);

        float2 minUV = UnityStereoTransformScreenSpaceTex(uv);
        if (any(diffs > threshold)) {
            // Check 10 vs 00
            float minDiff  = lerp(diffs.x, diffs.y, diffs.y < diffs.x);
            minUV    = lerp(uv00, uv10, diffs.y < diffs.x);
            // Check against 01
            minUV    = lerp(minUV, uv01, diffs.z < minDiff);
            minDiff  = lerp(minDiff, diffs.z, diffs.z < minDiff);
            // Check against 11
            minUV    = lerp(minUV, uv11, diffs.w < minDiff);
        }

        half shadow = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, minUV).x;
#else
        half shadow = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).x;
#endif

   		return half4(shadow, 0, 0, 0);
	}


    half4 FragDebugShadows(Varyings input) : SV_Target {

            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord.xy);

            half shadow = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).x;
            return shadow.xxxx;
    }



    half4 FragOverlayShadows(Varyings input) : SV_Target {

        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord.xy);

        half shadow = SAMPLE_TEXTURE2D_X(_ScreenSpaceShadowmapTexture, sampler_LinearClamp, uv).x;
        return half4(1, 1, 1, 1 - shadow) * _OverlayShadowColor;
}
