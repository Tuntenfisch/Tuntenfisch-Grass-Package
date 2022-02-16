#ifndef TUNTENFISCH_GRASS_INCLUDE_PASS_INPUTS
#define TUNTENFISCH_GRASS_INCLUDE_PASS_INPUTS

struct VertexPassInput
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;

    #if defined(_FORWARD_LIT_PASS)
        float2 lightmapUV : TEXCOORD1;
    #endif

    static VertexPassInput Create(float4 positionOS, float3 normalOS, float4 tangentOS)
    {
        VertexPassInput input = (VertexPassInput)0;
        input.positionOS = positionOS;
        input.normalOS = normalOS;
        input.tangentOS = tangentOS;

        return input;
    }
};

struct GeometryPassInput
{
    float3 positionWS : TEXCOORD0;
    float3 normalWS : NORMAL;
    float3 tangentWS : TANGENT;
    float3 bitangentWS : TEXCOORD1;
    int seed : TEXCOORD2;

    #if defined(_FORWARD_LIT_PASS)
        DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            float4 fogFactorAndVertexLight : TEXCOORD4;
        #else
            float fogFactor : TEXCOORD4;
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            float4 shadowCoord : TEXCOORD5;
        #endif
    #endif

    static GeometryPassInput Create(float3 positionWS, float3 normalWS, float3 tangentWS, float3 bitangentWS, int seed)
    {
        GeometryPassInput input = (GeometryPassInput)0;
        input.positionWS = positionWS;
        input.normalWS = normalWS;
        input.tangentWS = tangentWS;
        input.bitangentWS = bitangentWS;
        input.seed = seed;

        return input;
    }
};

struct FragmentPassInput
{
    float4 positionCS : SV_POSITION;

    #if defined(_FORWARD_LIT_PASS)
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float2 uv : TEXCOORD2;
        DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            float4 fogFactorAndVertexLight : TEXCOORD4;
        #else
            float fogFactor : TEXCOORD4;
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            float4 shadowCoord : TEXCOORD5;
        #endif
    #endif

    static FragmentPassInput Create(float4 positionCS, float3 positionWS, float3 normalWS, float2 uv)
    {
        FragmentPassInput input = (FragmentPassInput)0;
        input.positionCS = positionCS;

        #if defined(_FORWARD_LIT_PASS)
            input.positionWS = positionWS;
            input.normalWS = normalWS;
            input.uv = uv;
        #endif

        return input;
    }
};

#endif