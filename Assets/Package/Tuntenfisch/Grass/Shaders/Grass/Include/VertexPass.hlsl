#ifndef TUNTENFISCH_GRASS_INCLUDE_VERTEX_PASS
#define TUNTENFISCH_GRASS_INCLUDE_VERTEX_PASS

int CalculateSeed(VertexPassInput input)
{
    return asint(dot(input.positionOS, input.positionOS));
}

GeometryPassInput VertexPass(VertexPassInput input)
{
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    GeometryPassInput output = GeometryPassInput::Create
    (
        positionInputs.positionWS,
        normalInputs.normalWS,
        normalInputs.tangentWS,
        normalInputs.bitangentWS,
        CalculateSeed(input)
    );

    // If we are rendering the foward lit pass we need to additionally calculate some lighting related properties.
    #if defined(_FORWARD_LIT_PASS)
        float3 vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
        float fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

        OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
        OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            output.fogFactorAndVertexLight = float4(fogFactor, vertexLight);
        #else
            output.fogFactor = fogFactor;
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = GetShadowCoord(positionInputs);
        #endif
    #endif

    return output;
}

#endif