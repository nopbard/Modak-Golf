#ifndef UMBRA_SHADOW_FUNCTIONS_INCLUDED
#define UMBRA_SHADOW_FUNCTIONS_INCLUDED

#ifdef SHADERGRAPH_PREVIEW
    void GetMainShadow_float(float3 worldPos, out half shadowAtten){
        shadowAtten = 1;
    }
#else
    void GetMainShadow_float(float3 worldPos, out half shadowAtten){
	    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(worldPos));
	    shadowAtten = SampleScreenSpaceShadowmap(shadowCoord);
    }
#endif

#endif // UMBRA_SHADOW_FUNCTIONS_INCLUDED