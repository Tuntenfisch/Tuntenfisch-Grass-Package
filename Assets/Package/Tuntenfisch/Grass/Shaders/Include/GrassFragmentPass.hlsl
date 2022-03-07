#ifndef TUNTENFISCH_GRASS_INCLUDE_GRASS_FRAGMENT_PASS
#define TUNTENFISCH_GRASS_INCLUDE_GRASS_FRAGMENT_PASS

#if defined(_FORWARD_LIT_PASS)
    TEXTURE2D(_BladeTexture);
    SAMPLER(sampler_BladeTexture);
#endif

struct GrassFragment
{
    PROPERTY_WITH_SEMANTIC(float4, PositionCS, SV_POSITION)
    
    #if defined(_FORWARD_LIT_PASS)
        PROPERTY_WITH_SEMANTIC(float3, PositionWS, POSITION1)
        PROPERTY_WITH_SEMANTIC(float3, NormalWS, NORMAL)
        nointerpolation TWO_PACKED_PROPERTIES_WITH_SEMANTIC(uint, BaseColor, uint, TipColor, COLOR)
        PROPERTY_WITH_SEMANTIC(float2, UV, TEXCOORD0)
        DECLARE_LIGHTMAP_OR_SH(LightmapUV, VertexSH, 1);

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            PROPERTY_WITH_SEMANTIC(float4, FogFactorAndVertexLight, TEXCOORD2)
        #else
            PROPERTY_WITH_SEMANTIC(float, FogFactor, TEXCOORD2)
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            PROPERTY_WITH_SEMANTIC(float4, ShadowCoord, TEXCOORD3)
        #endif
    #endif

    static GrassFragment Create(float4 positionCS)
    {
        GrassFragment grassFragment = (GrassFragment)0;
        grassFragment.SetPositionCS(positionCS);

        return grassFragment;
    }
};

#if defined(_FORWARD_LIT_PASS)
    // The PBR lighting setup is more or less taken from https://github.com/Cyanilux/URP_ShaderCodeTemplates/blob/main/URP_PBRLitTemplate.shader.
    // See https://www.cyanilux.com/tutorials/urp-shader-code/ for more info.
    SurfaceData CreateSurfaceData(GrassFragment grassFragment)
    {
        float3 baseColor = UnpackR8G8B8(grassFragment.GetBaseColor());
        float3 tipColor = UnpackR8G8B8(grassFragment.GetTipColor());
        float2 uv = grassFragment.GetUV();

        SurfaceData surfaceData = (SurfaceData)0;
        surfaceData.albedo = SAMPLE_TEXTURE2D(_BladeTexture, sampler_BladeTexture, uv).xyz * lerp(baseColor, tipColor, uv.y);
        surfaceData.occlusion = 1.0f;

        return surfaceData;
    }

    InputData CreateInputData(GrassFragment grassFragment)
    {
        InputData inputData = (InputData)0;
        inputData.positionWS = grassFragment.GetPositionWS();
        inputData.normalWS = NormalizeNormalPerPixel(grassFragment.GetNormalWS());
        inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(inputData.positionWS));

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            inputData.shadowCoord = grassFragment.GetShadowCoord();
        #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
            inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
        #else
            inputData.shadowCoord = 0.0f;
        #endif

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            inputData.fogCoord = grassFragment.GetFogFactorAndVertexLight().x;
            inputData.vertexLighting = grassFragment.GetFogFactorAndVertexLight().yzw;
        #else
            inputData.fogCoord = grassFragment.GetFogFactor();
            inputData.vertexLighting = 0.0f;
        #endif

        inputData.bakedGI = SAMPLE_GI(grassFragment.LightmapUV, grassFragment.VertexSH, inputData.normalWS);
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(grassFragment.GetPositionCS());
        inputData.shadowMask = SAMPLE_SHADOWMASK(grassFragment.LightmapUV);

        return inputData;
    }

    float4 CalculateColor(GrassFragment grassFragment)
    {
        SurfaceData surfaceData = CreateSurfaceData(grassFragment);
        InputData inputData = CreateInputData(grassFragment);
        float4 color = UniversalFragmentPBR(inputData, surfaceData);
        color.rgb = MixFog(color.rgb, inputData.fogCoord);

        return color;
    }
#endif

float4 GrassFragmentPass(GrassFragment grassFragment) : SV_Target
{
    #if defined(_FORWARD_LIT_PASS)
        return CalculateColor(grassFragment);
    #elif defined(_SHADOW_CASTER_PASS)
        return 0.0f;
    #endif
}

#endif