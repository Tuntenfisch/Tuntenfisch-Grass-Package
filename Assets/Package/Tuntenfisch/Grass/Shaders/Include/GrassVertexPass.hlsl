#ifndef TUNTENFISCH_GRASS_INCLUDE_GRASS_VERTEX_PASS
#define TUNTENFISCH_GRASS_INCLUDE_GRASS_VERTEX_PASS

struct GrassVertex
{
    PROPERTY_WITH_SEMANTIC(float3, PositionOS, POSITION)
    PROPERTY_WITH_SEMANTIC(float3, NormalOS, NORMAL)
    PROPERTY_WITH_SEMANTIC(float4, TangentOS, TANGENT)

    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES) && defined(_FORWARD_LIT_PASS)
        PROPERTY_WITH_SEMANTIC(float3, BaseColor, COLOR)
        PROPERTY_WITH_SEMANTIC(float3, TipColor, TEXCOORD0)
    #endif

    #if defined(_FORWARD_LIT_PASS)
        PROPERTY_WITH_SEMANTIC(float2, LightmapUV, TEXCOORD1)
    #endif

    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES)
        FOUR_PACKED_PROPERTIES_WITH_SEMANTIC(uint, ShapeIndex, float, Width, float, Height, float, Pitch, TEXCOORD2)
        THREE_PACKED_PROPERTIES_WITH_SEMANTIC(float, Bend, float, BendExponent, float, Spread, TEXCOORD3)
    #endif

    static GrassVertex Create(float3 positionOS, float3 normalOS, float4 tangentOS)
    {
        GrassVertex grassVertex = (GrassVertex)0;
        grassVertex.SetPositionOS(positionOS);
        grassVertex.SetNormalOS(normalOS);
        grassVertex.SetTangentOS(tangentOS);

        return grassVertex;
    }
};

int CalculateSeed(GrassVertex grassVertex)
{
    return asint(dot(grassVertex.GetPositionOS(), grassVertex.GetPositionOS()));
}

GrassGeometry GrassVertexPass(GrassVertex grassVertex)
{
    VertexPositionInputs positionInputs = GetVertexPositionInputs(grassVertex.GetPositionOS());
    VertexNormalInputs normalInputs = GetVertexNormalInputs(grassVertex.GetNormalOS(), grassVertex.GetTangentOS());

    GrassGeometry grassGeometry = GrassGeometry::Create
    (
        positionInputs.positionWS,
        normalInputs.normalWS,
        normalInputs.tangentWS,
        normalInputs.bitangentWS,
        CalculateSeed(grassVertex)
    );

    #if defined(_FORWARD_LIT_PASS)
        float3 vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
        float fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

        OUTPUT_LIGHTMAP_UV(grassVertex.LightmapUV, unity_LightmapST, grassGeometry.LightmapUV);
        OUTPUT_SH(normalInputs.normalWS.xyz, grassGeometry.VertexSH);

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            grassGeometry.SetFogFactorAndVertexLight(float4(fogFactor, vertexLight));
        #else
            grassGeometry.SetFogFactor(fogFactor);
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            grassGeometry.SetShadowCoord(GetShadowCoord(positionInputs));
        #endif
    #endif

    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES) && defined(_FORWARD_LIT_PASS)
        grassGeometry.SetBaseColor(grassVertex.GetBaseColor());
        grassGeometry.SetTipColor(grassVertex.GetTipColor());
    #endif

    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES)
        grassGeometry.SetShapeIndex(grassVertex.GetShapeIndex());
        grassGeometry.SetWidth(grassVertex.GetWidth());
        grassGeometry.SetHeight(grassVertex.GetHeight());
        grassGeometry.SetPitch(grassVertex.GetPitch());
        grassGeometry.SetBend(grassVertex.GetBend());
        grassGeometry.SetBendExponent(grassVertex.GetBendExponent());
        grassGeometry.SetSpread(grassVertex.GetSpread());
    #endif

    return grassGeometry;
}

#endif