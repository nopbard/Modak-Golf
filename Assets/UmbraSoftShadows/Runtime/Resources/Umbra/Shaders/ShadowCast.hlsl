TEXTURE2D(_NoiseTex);
float4 _NoiseTex_TexelSize;
float4 _UmbraCascadeRects[4];
float _UmbraCascadeScales[4];

static const float2 randomOffsets[64] = {
float2(0.000000, 0.000000),
float2(-0.737277, 0.675590),
float2(0.123257, -1.408832),
float2(1.054406, 1.374128),
float2(-1.969616, -0.347296),
float2(1.885881, -1.201438),
float2(-0.633975, 2.366025),
float2(-1.221672, -2.346810),
float2(2.657852, 0.967379),
float2(-2.771639, 1.148050),
float2(1.336436, -2.865997),
float2(0.997328, 3.163121),
float2(-3.000000, -1.732051),
float2(3.520085, -0.780384),
float2(-2.146127, 3.064986),
float2(-0.505526, -3.839849),
float2(3.064178, 2.571150),
float2(-4.119181, 0.179847),
float2(3.000000, -3.000000),
float2(-0.190133, 4.354750),
float2(-2.874634, -3.425855),
float2(4.543371, 0.598146),
float2(-3.842164, 2.690312),
float2(1.038008, -4.682151),
float2(2.449490, 4.242641),
float2(-4.768585, -1.503529),
float2(4.621281, -2.154939),
float2(-1.988481, 4.800619),
float2(-1.809800, -4.972386),
float2(4.776700, 2.486592),
float2(-5.290594, 1.417610),
float2(2.991558, -4.695805),
float2(0.982302, 5.570914),
float2(-4.557468, -3.497068),
float2(5.808763, -0.508201),
float2(-3.996846, 4.361792),
float2(-0.000000, -6.000000),
float2(4.109455, 4.484683),
float2(-6.140957, -0.537264),
float2(4.954490, -3.801714),
float2(-1.098248, 6.228471),
float2(-3.440396, -5.400340),
float2(6.259915, 1.677339),
float2(-5.816519, 3.027888),
float2(2.268705, -6.233216),
float2(2.567119, 6.197572),
float2(-6.146878, -2.866337),
float2(6.538354, -2.061535),
float2(-3.464102, 6.000000),
float2(-1.515077, -6.834072),
float2(5.792280, 4.055798),
float2(-7.080333, 0.932143),
float2(4.635207, -5.524025),
float2(0.317554, 7.273181),
float2(-5.196152, -5.196152),
float2(7.409140, 0.323490),
float2(-5.732552, 4.810182),
float2(0.985451, -7.485245),
float2(4.368228, 6.238476),
float2(-7.499072, -1.662504),
float2(6.708204, -3.872983),
float2(-2.348587, 7.448768),
float2(-3.327700, -7.136275),
float2(7.333066, 3.037456)
};

#if _LOOP_STEP_X3
    #define LOOP_STEP 3
#elif _LOOP_STEP_X2
    #define LOOP_STEP 2
#else
    #define LOOP_STEP 1
#endif

#define SHADER_API_WEBGL ((SHADER_API_GLES || SHADER_API_GLES3) && SHADER_API_DESKTOP)

#if SHADER_API_MOBILE || SHADER_API_WEBGL
    #define LOOP(index, count) UNITY_UNROLL for(int index=0;index<64;index+=LOOP_STEP) if (index < count) {
#else
    #define LOOP(index, count) for(int index=0;index<count;index+=LOOP_STEP) {
#endif
#define END_LOOP }

half ComputeCascadeIndexWithBlending(float3 positionWS, out half blendFactor) {

    float3 fromCenter0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    float3 fromCenter1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    float3 fromCenter2 = positionWS - _CascadeShadowSplitSpheres2.xyz;
    float3 fromCenter3 = positionWS - _CascadeShadowSplitSpheres3.xyz;
    
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));
    
    // Determine the closest cascade
    half4 weights = half4(distances2 < _CascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);
    half cascadeIndex = 4 - dot(weights, half4(4, 3, 2, 1));
    
    // Compute blend factor between cascades
    half4 distancesToBorders = abs(distances2 - _CascadeShadowSplitSphereRadii);
    half4 blendFactors = 1.0 - saturate(distancesToBorders / BLEND_CASCADE_DATA);
    blendFactors *= blendFactors;
    blendFactors *= 0.5;

    if (blendFactors.x > blendFactors.y) {
        blendFactor = blendFactors.x;
        return saturate(1 - cascadeIndex);
    }
    if (blendFactors.y > blendFactors.z) {
        blendFactor = blendFactors.y;
        return clamp(3 - cascadeIndex, 1, 2);
    }
    blendFactor = blendFactors.z;
    return clamp(5 - cascadeIndex, 2, 3);
}


float4 CustomTransformWorldToShadowCoord(float3 positionWS, out half cascadeIndex, out half blendFactor)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    cascadeIndex = ComputeCascadeIndexWithBlending(positionWS, blendFactor);
#else
    cascadeIndex = 0;
    blendFactor = 0;
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));

    return float4(shadowCoord.xyz, 0);
}

float4 CustomTransformWorldToShadowCoord(float3 positionWS, out half cascadeIndex)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    cascadeIndex = 0;
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));

    return float4(shadowCoord.xyz, 0);
}


inline float3 InterpolateShadowmapCoord(float3 coords, float3 dx, float3 dy, float2 offset) {
    return coords + dx * offset.x + dy * offset.y;
}

#if _MASK_TEXTURE

TEXTURE2D(_MaskTex);

half ComputeMaskWorldSpace(float3 wpos, float3 normalWS) {
    float3 maskUV = wpos * MASK_SCALE;
    half n1 = SAMPLE_TEXTURE2D(_MaskTex, sampler_LinearRepeat, maskUV.zy).r;
    half n2 = SAMPLE_TEXTURE2D(_MaskTex, sampler_LinearRepeat, maskUV.xz).r;
    half n3 = SAMPLE_TEXTURE2D(_MaskTex, sampler_LinearRepeat, maskUV.xy).r;
    float3 triW = abs(normalWS);
    float3 weights = triW / (triW.x + triW.y + triW.z);
    half mask = dot(half3(n1, n2, n3), weights);
    return mask;
}
#endif

half4 FragCast(Varyings input) : SV_Target {

            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord.xy;

            float rawDepth = SampleSceneDepth(uv);

            #if !_RECEIVER_PLANE
                if (IsSkyBox(rawDepth)) return half4(1, 0, 0, 0);
            #endif

            half cascadeIndex;

            // normal provided by normals texture
            #if _NORMALS_TEXTURE
                float3 norm = SampleSceneNormals(uv);
            #endif            

            float3 wpos = GetWorldPosition(uv, rawDepth);

            #if _RECEIVER_PLANE
            if (wpos.y < _ReceiverPlaneAltitude) {
                float3 cameraToWpos = wpos - _WorldSpaceCameraPos;
                float t = (_ReceiverPlaneAltitude - _WorldSpaceCameraPos.y) / cameraToWpos.y;
                wpos = _WorldSpaceCameraPos + t * cameraToWpos;            
                #if _NORMALS_TEXTURE
                    norm = float3(0, 1, 0);
                #endif                
            }
            #endif

            // compute normal at world position
            #if !_NORMALS_TEXTURE
                float3 norm = GetNormalFromWPOS(wpos);
            #endif

            // get cascade and shadowmap coordinate
            #if defined(_BLEND_CASCADE)
                half blendFactor;
                float4 coords = CustomTransformWorldToShadowCoord(wpos, cascadeIndex, blendFactor);
                if (blendFactor <= 0 || blendFactor >= 0.5) discard;
            #else
                float4 coords = CustomTransformWorldToShadowCoord(wpos, cascadeIndex);
            #endif

            if (BEYOND_SHADOW_FAR(coords)) return half4(1, 0, 0, 0);

            // prepare noise
            float2 pos = uv * _SourceSize;
            float2 noise = normalize(SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_PointRepeat, pos * _NoiseTex_TexelSize.xy, 0).xy - 0.5);

            // pick tangent & binormal that are orthogonal to light direction
            float3 v = _MainLightPosition.xyz;
            v.y = abs(norm.y) < 0.9;
            v = normalize(v);
            float3 tangent = normalize(cross(norm, v));
            float3 binormal = cross(norm, tangent);

            // compute shadowmap axis coordinates for later interpolation
            float3 cr0 = mul(_MainLightWorldToShadow[cascadeIndex], float4(wpos + tangent,  1.0)).xyz;
            float3 cr1 = mul(_MainLightWorldToShadow[cascadeIndex], float4(wpos + binormal, 1.0)).xyz;
            float3 dx  = cr0 - coords.xyz;
            float3 dy  = cr1 - coords.xyz;

            // clamp to cascade boundary
            #if _MAIN_LIGHT_SHADOWS_CASCADE
                float4 cascadeRect = _UmbraCascadeRects[cascadeIndex];
            #endif

       #if _CONTACT_HARDENING

            // contact hardening
            float depthAvg = 0;
            float occluders = 0;
            float3 dxSearch = dx * OCCLUDERS_SEARCH_RADIUS;
            float3 dySearch = dy * OCCLUDERS_SEARCH_RADIUS;
#if SHADER_API_MOBILE
            //UNITY_UNROLLX(32)
#endif
            LOOP(i, OCCLUDERS_COUNT)
                float2 offset = reflect(randomOffsets[i], noise);
                float3 cr2 = InterpolateShadowmapCoord(coords.xyz, dxSearch, dySearch, offset);
                #if _MAIN_LIGHT_SHADOWS_CASCADE
                    cr2.xy  = clamp(cr2.xy, cascadeRect.xy, cascadeRect.zw);
                #endif
                float d = SAMPLE_TEXTURE2D_LOD(_MainLightShadowmapTexture, sampler_LinearClamp, cr2.xy, 0).x;
                if (d > cr2.z) {
                    depthAvg += d;
                    occluders++;
                }
            END_LOOP

            if (occluders < 1) return half4(1, 0, 0, 0);
            
            depthAvg = depthAvg / occluders + 0.00001;
            float distAvg = depthAvg - coords.z;
            #if _MAIN_LIGHT_SHADOWS_CASCADE
                distAvg *= _UmbraCascadeScales[cascadeIndex];
            #endif
            float smoothness = distAvg / depthAvg;
            
            float distanceFactor = lerp(CONTACT_STRENGTH, MAX_SHADOW_SPREAD, saturate(distAvg / CONTACT_STRENGTH_KNEE) * smoothness) * LIGHT_SIZE;
      #else
            float distanceFactor = LIGHT_SIZE;
      #endif

            // compute shadow term
            dx *= distanceFactor;
            dy *= distanceFactor;
            half shadow = 0;
            half samples = 0;

#if SHADER_API_MOBILE
            //SHADOW_SAMPLES = min(SHADOW_SAMPLES, 32);
            //UNITY_UNROLLX(32)
#endif
            LOOP(j, SHADOW_SAMPLES)
                float2 offset = reflect(randomOffsets[j], noise);
                float3 cr2 = InterpolateShadowmapCoord(coords.xyz, dx, dy, offset);
                #if _MAIN_LIGHT_SHADOWS_CASCADE
                    cr2.xy  = clamp(cr2.xy, cascadeRect.xy, cascadeRect.zw);
                #endif
                shadow += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, cr2);
                samples++;
                if (samples > _EarlyOutSamples && (shadow <= 0 || shadow >= samples)) break;
            END_LOOP
            shadow /= samples;

#if _MASK_TEXTURE
            half mask = ComputeMaskWorldSpace(wpos, norm);
            shadow = saturate(shadow + mask);
#endif

            // apply shadow intensity
            half4 shadowParams = GetMainLightShadowParams();
            half shadowIntensity = shadowParams.x;
            shadow = InvLerp(shadow, shadowIntensity);

            // output with alpha if cascade blending is used
            #if defined(_BLEND_CASCADE)
                return half4(shadow, distanceFactor, 0, blendFactor);
            #else
                return half4(shadow, distanceFactor, 0, 1);
            #endif
}


        half4 FragUnityShadows(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if UNITY_REVERSED_Z
            float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r;
#else
            float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r;
            deviceDepth = deviceDepth * 2.0 - 1.0;
#endif

            //Fetch shadow coordinates for cascade.
            float3 wpos = ComputeWorldSpacePosition(input.texcoord.xy, deviceDepth, unity_MatrixInvVP);
            float4 coords = TransformWorldToShadowCoord(wpos);

            // Screenspace shadowmap is only used for directional lights which use orthogonal projection.
            half realtimeShadow = MainLightRealtimeShadow(coords);

            return realtimeShadow;
        }



