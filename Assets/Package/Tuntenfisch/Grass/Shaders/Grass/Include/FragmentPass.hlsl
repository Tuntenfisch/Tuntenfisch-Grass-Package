#ifndef TUNTENFISCH_GRASS_INCLUDE_FRAGMENT_PASS
#define TUNTENFISCH_GRASS_INCLUDE_FRAGMENT_PASS

#if defined(_FORWARD_LIT_PASS)
    TEXTURE2D(_BladeTexture);
    SAMPLER(sampler_BladeTexture);
#endif

#if defined(_FORWARD_LIT_PASS)
    // The PBR lighting setup is more or less taken from https://github.com/Cyanilux/URP_ShaderCodeTemplates/blob/main/URP_PBRLitTemplate.shader.
    // See https://www.cyanilux.com/tutorials/urp-shader-code/ for more info.
    SurfaceData CreateSurfaceData(FragmentPassInput input)
    {
        SurfaceData surfaceData = (SurfaceData)0;
        surfaceData.albedo = SAMPLE_TEXTURE2D(_BladeTexture, sampler_BladeTexture, input.uv).xyz * lerp(_BladeBaseColor, _BladeTipColor, input.uv.y).xyz;
        surfaceData.occlusion = 1.0f;
        return surfaceData;
    }

    InputData CreateInputData(FragmentPassInput input)
    {
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
        inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(inputData.positionWS));

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            inputData.shadowCoord = input.shadowCoord;
        #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
            inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
        #else
            inputData.shadowCoord = 0.0f;
        #endif

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            inputData.fogCoord = input.fogFactorAndVertexLight.x;
            inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
        #else
            inputData.fogCoord = input.fogFactor;
            inputData.vertexLighting = 0.0f;
        #endif

        inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
        inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
        return inputData;
    }
#endif

float4 FragmentPass(FragmentPassInput input) : SV_Target
{
    // We only need to apply lighting when we render the forward lit pass.
    #if defined(_FORWARD_LIT_PASS)
        SurfaceData surfaceData = CreateSurfaceData(input);
        InputData inputData = CreateInputData(input);
        float4 color = UniversalFragmentPBR(inputData, surfaceData);
        color.rgb = MixFog(color.rgb, inputData.fogCoord);
        return color;
    #elif defined(_SHADOW_CASTER_PASS)
        return 0.0f;
    #endif
}

#endif