#ifndef TUNTENFISCH_GRASS_INCLUDE_GRASS_GEOMETRY_PASS
#define TUNTENFISCH_GRASS_INCLUDE_GRASS_GEOMETRY_PASS

// All our inputs to pow function calls are positive but the shader compiler doesn't
// know that so we disable the corresponding warning (WAR_POW_NOT_KNOWN_TO_BE_POSITIVE)
#pragma warning(disable : 3571)

#define BLADE_COUNT 4
#define SEGMENT_COUNT 4

#if defined(_SHADOW_CASTER_PASS)
    float3 _LightDirection;
    float3 _LightPosition;
#endif

TEXTURE2D(_BladeShapeTexture);
SAMPLER(sampler_BladeShapeTexture);

#if defined(_GRASS_INTERACTION_ENABLED)
    TEXTURE2D(_InteractionTexture);
    TEXTURE2D(_InteractionDepthTexture);
    SAMPLER(linear_clamp_sampler);
#endif

TEXTURE2D(_WindIntensityTexture);
SAMPLER(sampler_WindIntensityTexture);

struct GrassBladeProperties
{
    #if defined(_FORWARD_LIT_PASS)
        TWO_PACKED_PROPERTIES(uint, BaseColor, uint, TipColor)
    #endif

    ARRAY_PROPERTY(float4, Shape, 2)
    FOUR_PACKED_PROPERTIES(float, Width, float, Height, float, Pitch, float, Bend)
    PROPERTY(float, BendExponent)

    float GetBladeShape(float percentage)
    {
        uint indexA = floor(percentage * 7.0f);
        uint indexB = min(indexA + 1, 7);
        float sampleA = GetShapeElement(indexA / 4)[indexA % 4];
        float sampleB = GetShapeElement(indexB / 4)[indexB % 4];
        float factor = 7.0f * percentage - indexA;
        
        return lerp(sampleA, sampleB, factor);
    }

    static GrassBladeProperties Create(float4 shape[2], float width, float height, float pitch, float bend, float bendExponent)
    {
        GrassBladeProperties bladeProperties;
        bladeProperties.SetShape(shape);
        bladeProperties.SetWidth(width);
        bladeProperties.SetHeight(height);
        bladeProperties.SetPitch(radians(pitch));
        bladeProperties.SetBend(bend);
        bladeProperties.SetBendExponent(bendExponent);

        return bladeProperties;
    }
};

struct GrassInteractionProperties
{
    PROPERTY(float4x4, PitchTransform)

    static GrassInteractionProperties Create(float4x4 pitchTransform)
    {
        GrassInteractionProperties interactionProperties;
        interactionProperties.SetPitchTransform(pitchTransform);

        return interactionProperties;
    }
};

struct GrassWindProperties
{
    PROPERTY(float3, VelocityTS)
    PROPERTY(float4x4, PitchTransform)

    static GrassWindProperties Create(float3 velocityTS, float4x4 pitchTransform)
    {
        GrassWindProperties windProperties;
        windProperties.SetVelocityTS(velocityTS);
        windProperties.SetPitchTransform(pitchTransform);

        return windProperties;
    }
};

struct GrassGeometry
{
    PROPERTY_WITH_SEMANTIC(float3, PositionWS, POSITION1)
    PROPERTY_WITH_SEMANTIC(float3, NormalWS, NORMAL)
    PROPERTY_WITH_SEMANTIC(float3, TangentWS, TANGENT)
    PROPERTY_WITH_SEMANTIC(float3, BitangentWS, BINORMAL)
    
    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES) && defined(_FORWARD_LIT_PASS)
        PROPERTY_WITH_SEMANTIC(float3, BaseColor, COLOR)
            PROPERTY_WITH_SEMANTIC(float3, TipColor, TEXCOORD0)
    #endif

    PROPERTY_WITH_SEMANTIC(int, Seed, TEXCOORD1)

    #if defined(_FORWARD_LIT_PASS)
        DECLARE_LIGHTMAP_OR_SH(LightmapUV, VertexSH, 2);

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            PROPERTY_WITH_SEMANTIC(float4, FogFactorAndVertexLight, TEXCOORD3)
        #else
            PROPERTY_WITH_SEMANTIC(float, FogFactor, TEXCOORD3)
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            PROPERTY_WITH_SEMANTIC(float4, ShadowCoord, TEXCOORD4)
        #endif
    #endif

    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES)
        FOUR_PACKED_PROPERTIES_WITH_SEMANTIC(uint, ShapeIndex, float, Width, float, Height, float, Pitch, TEXCOORD5)
        THREE_PACKED_PROPERTIES_WITH_SEMANTIC(float, Bend, float, BendExponent, float, Spread, TEXCOORD6)
    #endif

    static GrassGeometry Create(float3 positionWS, float3 normalWS, float3 tangentWS, float3 bitangentWS, int seed)
    {
        GrassGeometry grassGeometry = (GrassGeometry)0;
        grassGeometry.SetPositionWS(positionWS);
        grassGeometry.SetNormalWS(normalWS);
        grassGeometry.SetTangentWS(tangentWS);
        grassGeometry.SetBitangentWS(bitangentWS);
        grassGeometry.SetSeed(seed);

        return grassGeometry;
    }
};

GrassFragment GetGrassFragment(GrassGeometry grassGeometry, GrassBladeProperties bladeProperties, float3 positionWS, float3 normalWS, float2 uv)
{
    #if defined(_FORWARD_LIT_PASS)
        GrassFragment grassFragment = GrassFragment::Create(TransformWorldToHClip(positionWS));
        grassFragment.SetPositionWS(positionWS);
        grassFragment.SetNormalWS(normalWS);
        grassFragment.SetBaseColor(bladeProperties.GetBaseColor());
        grassFragment.SetTipColor(bladeProperties.GetTipColor());
        grassFragment.SetUV(uv);

        #if defined(LIGHTMAP_ON)
            grassFragment.LightmapUV = grassGeometry.LightmapUV;
        #else
            grassFragment.VertexSH = grassGeometry.VertexSH;
        #endif

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            grassFragment.SetFogFactorAndVertexLight(grassGeometry.GetFogFactorAndVertexLight());
        #else
            grassFragment.SetFogFactor(grassGeometry.GetFogFactor());
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            grassFragment.SetShadowCoord(grassGeometry.GetShadowCoord());
        #endif
    #elif defined(_SHADOW_CASTER_PASS)
        GrassFragment grassFragment = GrassFragment::Create(TransformWorldToShadowHClip(positionWS, normalWS, _LightPosition, _LightDirection));
    #endif

    return grassFragment;
}

void ApplyWind(GrassWindProperties windProperties, GrassBladeProperties bladeProperties, float bladePercentage, inout float3 positionTS, inout float3 normalTS)
{
    // We do two things to make the grass look like it is affected by wind:
    // 1. We pitch the blade in direction of the wind.
    positionTS = bladePercentage > 0.0f ? TransformPosition(windProperties.GetPitchTransform(), positionTS) : positionTS;
    normalTS = bladePercentage > 0.0f ? TransformDirection(windProperties.GetPitchTransform(), normalTS) : normalTS;
    // 2. We bend the blade in direction of the wind.
    positionTS.xz += windProperties.GetVelocityTS().xz * pow(positionTS.y, bladeProperties.GetBendExponent());
    float windDerivative = bladeProperties.GetBendExponent() * pow(bladePercentage, bladeProperties.GetBendExponent() - 1.0f) * pow(bladeProperties.GetHeight(), bladeProperties.GetBendExponent());
    normalTS.z -= windProperties.GetVelocityTS().z * windDerivative;
}

void ApplyInteraction(GrassInteractionProperties interactionProperties, float bladePercentage, inout float3 positionTS, inout float3 normalTS)
{
    #if _GRASS_INTERACTION_ENABLED
        positionTS = bladePercentage > 0.0f ? TransformPosition(interactionProperties.GetPitchTransform(), positionTS) : positionTS;
        normalTS = bladePercentage > 0.0f ? TransformDirection(interactionProperties.GetPitchTransform(), normalTS) : normalTS;
    #endif
}

void Output
(
    GrassGeometry grassGeometry,
    GrassBladeProperties bladeProperties,
    GrassInteractionProperties interactionProperties,
    GrassWindProperties windProperties,
    float bladePercentage,
    float4x4 tangentToWorldTransform,
    float3 positionTS,
    float3 normalTS,
    float2 uv,
    inout TriangleStream<GrassFragment> outputStream
)
{
    ApplyWind(windProperties, bladeProperties, bladePercentage, positionTS, normalTS);
    ApplyInteraction(interactionProperties, bladePercentage, positionTS, normalTS);
    float3 positionWS = TransformPosition(tangentToWorldTransform, positionTS);
    float3 normalWS = TransformDirection(tangentToWorldTransform, normalize(normalTS));
    GrassFragment grassFragment = GetGrassFragment(grassGeometry, bladeProperties, positionWS, normalWS, uv);
    outputStream.Append(grassFragment);
}

float GetBladePercentage(int segment)
{
    return segment / (float)SEGMENT_COUNT;
}

float3 GetSegmentNormalTS(float bladePercentage, GrassBladeProperties bladeProperties)
{
    // The normal of the grass blade segment depends on the slope of the bend, i.e. the derivative of the z-axis.
    float bendDerivative = bladeProperties.GetBend() * bladeProperties.GetBendExponent() * pow(bladePercentage, bladeProperties.GetBendExponent() - 1.0f);
    // The normal is orthogonal to the slope.
    return float3(0.0f, 1.0f, -bendDerivative);
}

float3 GetSegmentPositionTS(float bladePercentage, GrassBladeProperties bladeProperties)
{
    float x = 0.5f * bladeProperties.GetWidth() * bladeProperties.GetBladeShape(bladePercentage);
    float y = bladeProperties.GetHeight() * bladePercentage;
    // The closer the vertex is to the tip of the blade the more it gets affected by the blade bend.
    // This will effectively create a forwards arch from bottom to top.
    float z = bladeProperties.GetBend() * pow(bladePercentage, bladeProperties.GetBendExponent());

    return float3(x, y, z);
}

GrassWindProperties GetWindPropeties(float3 bladeRootPositionWS, float4x4 worldToTangentTransform)
{
    float4x4 worldToWindTransform = AxesToRotationTransform(_WindRightDirection, _WindUpDirection, _WindForwardDirection);
    float2 uv = _WindFrequency * (TransformPosition(worldToWindTransform, bladeRootPositionWS).xz - float2(0.0f, _Time.y));
    float windIntensity = SAMPLE_TEXTURE2D_LOD(_WindIntensityTexture, sampler_WindIntensityTexture, uv, 0).r;
    float windSpeed = windIntensity * _WindSpeed;
    float3 windDirectionTS = TransformDirection(worldToTangentTransform, _WindForwardDirection);
    float3 windCrossDirectionTS = TransformDirection(worldToTangentTransform, _WindRightDirection);

    return GrassWindProperties::Create
    (
        windSpeed * _WindDisplacement * windDirectionTS,
        AngleAxis4x4(windSpeed * radians(_WindTilt), windCrossDirectionTS)
    );
}

GrassInteractionProperties GetInteractionProperties(GrassBladeProperties bladeProperties, float3 bladeRootPositionWS, float4x4 worldToTangentTransform)
{
    #if _GRASS_INTERACTION_ENABLED
        float4x4 interactionToTangentTransform = mul(worldToTangentTransform, _InteractionToWorldTransform);

        // To sample the interaction textures we calculate the blade root position in interaction space.
        float3 bladeRootPositionIS = TransformPosition(_WorldToInteractionTransform, bladeRootPositionWS);
        float2 uv = saturate(bladeRootPositionIS.xy * _InteractionAreaSize.y + 0.5f);

        float4 interaction = SAMPLE_TEXTURE2D_LOD(_InteractionTexture, linear_clamp_sampler, uv, 0);
        // The interaction texture stores the direction of interaction, i.e. the direction the blade should be tilted in,
        // in the rgb channels.
        float3 interactionDirectionIS = UnpackNormalRGBNoScale(interaction);
        // We need the vector orthogonal to the interaction direction to tilt it.
        // The direction is only defined along the xy-axes in interaction space.
        float3 interactionCrossDirectionIS = float3(interactionDirectionIS.y, -interactionDirectionIS.x, 0.0f);
        // The blue channel stores the instensity of the interaction, i.e. how strong the blade should tilt in the interaction direction.
        // Additionally, the interaction intensity should smoothly transition to 0 near the edge of the interaction texture.
        float interactionIntensity = interaction.b * GetSmallestComponent(smoothstep(0.0f, 0.05f, float4(uv, 1.0f - uv)));
        float interactionDepth = SAMPLE_TEXTURE2D_LOD(_InteractionDepthTexture, linear_clamp_sampler, uv, 0).r;

        // Account for reversed depth on certain platforms (https://forum.unity.com/threads/reversed-z.1197439/).
        #if defined(UNITY_REVERSED_Z)
            interactionDepth = 1.0f - interactionDepth;
        #endif

        // Unity's LinearEyeDepth() can be used to calculate the distance from the camera but only works with perspective cameras.
        // For an orthographic camera we need to calculate the distance ourselves. But that's straightforward since it's encoded linearly.
        float distanceFromCamera = lerp(_InteractionCameraClippingPlanes.x, _InteractionCameraClippingPlanes.y, interactionDepth);
        float3 interactionPositionIS = float3(bladeRootPositionIS.xy, distanceFromCamera);

        float3 interactionPositionTS = TransformPosition(interactionToTangentTransform, interactionPositionIS);
        float3 interactionCrossDirectionTS = TransformDirection(interactionToTangentTransform, interactionCrossDirectionIS);

        // Take interaction depth into account.
        float normalizedInteractionHeight = interactionPositionTS.y / bladeProperties.GetHeight();
        interactionIntensity *= GetSmallestComponent(smoothstep(float2(-0.05f, -0.25f), 0.0f, float2(normalizedInteractionHeight, 1.0f - normalizedInteractionHeight)));

        return GrassInteractionProperties::Create(AngleAxis4x4(interactionIntensity * radians(_InteractionTilt), interactionCrossDirectionTS));
    #else
        return GrassInteractionProperties::Create(0.0f);
    #endif
}

float AddNoise(float value, float variance, inout PRNG prng, float min = 0.0f)
{
    return max(value + prng.NextFloat(-variance, variance), min);
}

GrassBladeProperties GetBladeProperties(GrassGeometry grassGeometry, inout PRNG prng)
{
    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES)
        float bladeShapeU = grassGeometry.GetShapeIndex() * _BladeShapeTexture_TexelSize.x;
    #else
        float bladeShapeU = prng.NextFloat();
    #endif

    float4 bladeShape[2];
    bladeShape[0] = SAMPLE_TEXTURE2D_LOD(_BladeShapeTexture, sampler_BladeShapeTexture, float2(bladeShapeU, 0.0f), 0);
    bladeShape[1] = SAMPLE_TEXTURE2D_LOD(_BladeShapeTexture, sampler_BladeShapeTexture, float2(bladeShapeU, 1.0f), 0);

    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES)
        GrassBladeProperties bladeProperties = GrassBladeProperties::Create
        (
            bladeShape,
            AddNoise(grassGeometry.GetWidth(), _BladeWidthVariance, prng),
            AddNoise(grassGeometry.GetHeight(), _BladeHeightVariance, prng),
            AddNoise(grassGeometry.GetPitch(), _BladePitchVariance, prng),
            AddNoise(grassGeometry.GetBend(), _BladeBendVariance, prng),
            AddNoise(grassGeometry.GetBendExponent(), _BladeBendExponentVariance, prng, 1.0f)
        );

        #if defined(_FORWARD_LIT_PASS)
            float3 baseColor = grassGeometry.GetBaseColor();
            float3 tipColor = grassGeometry.GetTipColor();
        #endif
    #else
        // Get a random width, height, pitch, bend and bend exponent within the range specified in the inspector.
        // This will determine the general look of the grass blade.
        GrassBladeProperties bladeProperties = GrassBladeProperties::Create
        (
            bladeShape,
            prng.NextFloat(_BladeWidthRange),
            prng.NextFloat(_BladeHeightRange),
            prng.NextFloat(_BladePitchRange),
            prng.NextFloat(_BladeBendRange),
            prng.NextFloat(_BladeBendExponentRange)
        );

        #if defined(_FORWARD_LIT_PASS)
            float3 baseColor = _BladeBaseColor;
            float3 tipColor = _BladeTipColor;
        #endif
    #endif

    #if defined(_FORWARD_LIT_PASS)
        bladeProperties.SetBaseColor(PackR8G8B8(baseColor + _BladeBaseColorVariance * float3(prng.NextFloat(-1.0f, 1.0f), prng.NextFloat(-1.0f, 1.0f), prng.NextFloat(-1.0f, 1.0f))));
        bladeProperties.SetTipColor(PackR8G8B8(tipColor + _BladeTipColorVariance * float3(prng.NextFloat(-1.0f, 1.0f), prng.NextFloat(-1.0f, 1.0f), prng.NextFloat(-1.0f, 1.0f))));
    #endif

    return bladeProperties;
}

float3 GetPositionOnDisc(float3 center, float distance, float angle)
{
    float x, z;
    sincos(angle, z, x);
    float3 direction = float3(x, 0.0f, z);

    return center + distance * direction;
}

float4x4 GetTangentToWorldTransform(GrassGeometry grassGeometry, inout PRNG prng)
{
    #if defined(_MESH_CONTAINS_BLADE_PROPERTIES)
        float3 bladePosition = GetPositionOnDisc(grassGeometry.GetPositionWS(), grassGeometry.GetSpread() + _BladeSpreadVariance * prng.NextFloat(-1.0f, 1.0f), prng.NextFloat(0.0f, 2.0f * PI));
    #else
        // The blade is positioned randomly on a disc. The disc is centered on the grassGeometry vertex position in world space and spans the xz-axes in tangent space.
        float3 bladePosition = GetPositionOnDisc(grassGeometry.GetPositionWS(), prng.NextFloat(_BladeSpreadRange.x, _BladeSpreadRange.y), prng.NextFloat(0.0f, 2.0f * PI));
    #endif
    // Matrices can be initialized with vectors wherein each vector specifies a row of the matrix. We want each vector specifying a column instead tho,
    // so we return the transpose.
    float4x4 tangentToWorldTransform = SetTransformTranslation(AxesToRotationTransform(grassGeometry.GetTangentWS(), grassGeometry.GetNormalWS(), grassGeometry.GetBitangentWS()), bladePosition);
    // The base of the blade is oriented randomly alond the z-axis.
    float4x4 yawTransform = AngleAxis4x4(prng.NextFloat(0.0f, 2.0f * PI), float3(0.0f, 1.0f, 0.0f));

    return mul(tangentToWorldTransform, yawTransform);
}

void CreateBlade(GrassGeometry grassGeometry, inout TriangleStream<GrassFragment> outputStream, inout PRNG prng)
{
    // The entire blade position and normal calculations are done in tangent space and later on transformed into world space.
    float4x4 tangentToWorldTransform = GetTangentToWorldTransform(grassGeometry, prng);
    float4x4 worldToTangentTransform = InvertTranslationRotationTransform(tangentToWorldTransform);

    float3 bladeRootPositionWS = GetTransformTranslation(tangentToWorldTransform);

    GrassBladeProperties bladeProperties = GetBladeProperties(grassGeometry, prng);
    GrassInteractionProperties interactionProperties = GetInteractionProperties(bladeProperties, bladeRootPositionWS, worldToTangentTransform);
    GrassWindProperties windProperties = GetWindPropeties(bladeRootPositionWS, worldToTangentTransform);

    // The blade itself is pitched forwards by a random angle in accordance with the blade pitch range specified in the inspector.
    // We apply this only after calculating the wind/interaction properties because we don't want the pitch to affect these properties.
    float4x4 pitchTransform = AngleAxis4x4(bladeProperties.GetPitch(), float3(1.0f, 0.0f, 0.0f));
    tangentToWorldTransform = mul(tangentToWorldTransform, pitchTransform);

    for (int segment = 0; segment <= SEGMENT_COUNT; segment++)
    {
        float bladePercentage = GetBladePercentage(segment);
        float3 segmentPositionTS = GetSegmentPositionTS(bladePercentage, bladeProperties);
        float3 segmentNormalTS = GetSegmentNormalTS(bladePercentage, bladeProperties);

        Output(grassGeometry, bladeProperties, interactionProperties, windProperties, bladePercentage, tangentToWorldTransform, segmentPositionTS, segmentNormalTS, float2(1.0f, bladePercentage), outputStream);
        Output(grassGeometry, bladeProperties, interactionProperties, windProperties, bladePercentage, tangentToWorldTransform, float3(-segmentPositionTS.x, segmentPositionTS.yz), segmentNormalTS, float2(0.0f, bladePercentage), outputStream);
    }
    outputStream.RestartStrip();
}

[maxvertexcount(BLADE_COUNT * 2 * (SEGMENT_COUNT + 1))]
void GrassGeometryPass(point GrassGeometry inputs[1], inout TriangleStream<GrassFragment> outputStream)
{
    // Use some arbitrary value depending on the object position of the vertex as the seed.
    PRNG prng = PRNG::Create(inputs[0].GetSeed());

    // For each vertex of the original mesh, we create BLADE_COUNT grass blades.
    // Each blade will be positioned randomly on a disc centered on the given grassGeometry vertex and spanning the xz-axes in mesh tangent space.
    for (int blade = 0; blade < BLADE_COUNT; blade++)
    {
        CreateBlade(inputs[0], outputStream, prng);
    }
}

#endif