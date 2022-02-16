#ifndef TUNTENFISCH_GRASS_INCLUDE_GEOMETRY_PASS
#define TUNTENFISCH_GRASS_INCLUDE_GEOMETRY_PASS

// All our inputs to pow function calls are positive but the shader compiler doesn't
// know that so we disable the corresponding warning (WAR_POW_NOT_KNOWN_TO_BE_POSITIVE)
#pragma warning(disable : 3571)

#include "Packages/com.tuntenfisch.commons/Shaders/Include/Math.hlsl"
#include "Packages/com.tuntenfisch.commons/Shaders/Include/Random.hlsl"

#define BLADE_COUNT 4
#define SEGMENT_COUNT 5

#if defined(_SHADOW_CASTER_PASS)
    float3 _LightDirection;
    float3 _LightPosition;
#endif

TEXTURE2D(_BladeShapeTexture);
SAMPLER(sampler_BladeShapeTexture);

TEXTURE2D(_InteractionTexture);
TEXTURE2D(_InteractionDepthTexture);
SAMPLER(linear_clamp_sampler);

TEXTURE2D(_WindIntensityTexture);
SAMPLER(sampler_WindIntensityTexture);

struct BladeProperties
{
    float4 shape[2];
    float width;
    float height;
    float pitch;
    float bend;
    float bendExponent;

    static BladeProperties Create(float4 shape[2], float width, float height, float pitch, float bend, float bendExponent)
    {
        BladeProperties bladeProperties;
        bladeProperties.shape = shape;
        bladeProperties.width = width;
        bladeProperties.height = height;
        bladeProperties.pitch = pitch;
        bladeProperties.bend = bend;
        bladeProperties.bendExponent = bendExponent;

        return bladeProperties;
    }

    float GetBladeShape(float percentage)
    {
        uint indexA = percentage * 7.0f;
        uint indexB = min(indexA + 1, 7);
        float sampleA = shape[indexA / 4][indexA % 4];
        float sampleB = shape[indexB / 4][indexB % 4];
        float factor = 7.0f * percentage - indexA;
        
        return lerp(sampleA, sampleB, factor);
    }
};

struct InteractionProperties
{
    float4x4 pitchTransform;

    static InteractionProperties Create(float4x4 pitchTransform)
    {
        InteractionProperties interactionProperties;
        interactionProperties.pitchTransform = pitchTransform;

        return interactionProperties;
    }
};

struct WindProperties
{
    float3 velocityTS;
    float4x4 pitchTransform;

    static WindProperties Create(float3 velocityTS, float4x4 pitchTransform)
    {
        WindProperties windProperties;
        windProperties.velocityTS = velocityTS;
        windProperties.pitchTransform = pitchTransform;

        return windProperties;
    }
};

#if defined(_SHADOW_CASTER_PASS)
    // Basically same as Unity's implementation found here https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl.
    float4 TransformWorldToShadowHClip(float3 positionWS, float3 normalWS)
    {
        #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
        #else
            float3 lightDirectionWS = _LightDirection;
        #endif

        float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

        #if defined(UNITY_REVERSED_Z)
            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif

        return positionCS;
    }
#endif

FragmentPassInput GetFragmentPassInput(GeometryPassInput input, float3 positionWS, float3 normalWS, float2 uv)
{
    float4 positionCS = 0.0f;

    #if defined(_FORWARD_LIT_PASS)
        // If we are rendering the forward lit pass, we can use Unity's existing TransformWorldToHClip function.
        positionCS = TransformWorldToHClip(positionWS);
    #elif defined(_SHADOW_CASTER_PASS)
        // If we are rendering the shadow caster pass, we roll our "own" solution that applies things like the shadow bias
        // before transforming the world space position into clip space.
        positionCS = TransformWorldToShadowHClip(positionWS, normalWS);
    #endif

    FragmentPassInput output = FragmentPassInput::Create(positionCS, positionWS, normalWS, TRANSFORM_TEX(uv, _BladeTexture));

    #if defined(_FORWARD_LIT_PASS)
        #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
        #else
            output.vertexSH = input.vertexSH;
        #endif

        #if defined(_ADDITIONAL_LIGHTS_VERTEX)
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight;
        #else
            output.fogFactor = input.fogFactor;
        #endif

        #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.shadowCoord;
        #endif
    #endif

    return output;
}

void ApplyWind(WindProperties windProperties, BladeProperties bladeProperties, float bladePercentage, inout float3 positionTS, inout float3 normalTS)
{
    // We do two things to make the grass look like it is affected by wind:
    // 1. We pitch the blade in direction of the wind.
    positionTS = bladePercentage > 0.0f ? TransformPosition(windProperties.pitchTransform, positionTS) : positionTS;
    normalTS = bladePercentage > 0.0f ? TransformDirection(windProperties.pitchTransform, normalTS) : normalTS;
    // 2. We bend the blade in direction of the wind.
    positionTS.xz += windProperties.velocityTS.xz * pow(positionTS.y, bladeProperties.bendExponent);
    float windDerivative = bladeProperties.bendExponent * pow(bladePercentage, bladeProperties.bendExponent - 1.0f) * pow(bladeProperties.height, bladeProperties.bendExponent);
    normalTS.z -= windProperties.velocityTS.z * windDerivative;
}

void ApplyInteraction(InteractionProperties interactionProperties, float bladePercentage, inout float3 positionTS, inout float3 normalTS)
{
    #if _GRASS_INTERACTION_ENABLED
        positionTS = bladePercentage > 0.0f ? TransformPosition(interactionProperties.pitchTransform, positionTS) : positionTS;
        normalTS = bladePercentage > 0.0f ? TransformDirection(interactionProperties.pitchTransform, normalTS) : normalTS;
    #endif
}

void Output
(
    GeometryPassInput input,
    WindProperties windProperties,
    InteractionProperties interactionProperties,
    BladeProperties bladeProperties,
    float bladePercentage,
    float4x4 tangentToWorldTransform,
    float3 positionTS,
    float3 normalTS,
    float2 uv,
    inout TriangleStream<FragmentPassInput> outputStream
)
{
    ApplyWind(windProperties, bladeProperties, bladePercentage, positionTS, normalTS);
    ApplyInteraction(interactionProperties, bladePercentage, positionTS, normalTS);
    float3 positionWS = TransformPosition(tangentToWorldTransform, positionTS);
    float3 normalWS = TransformDirection(tangentToWorldTransform, normalize(normalTS));
    FragmentPassInput output = GetFragmentPassInput(input, positionWS, normalWS, uv);
    outputStream.Append(output);
}

float GetBladePercentage(int segment)
{
    return segment / (float)SEGMENT_COUNT;
}

float3 GetSegmentNormalTS(float bladePercentage, BladeProperties bladeProperties)
{
    // The normal of the grass blade segment depends on the slope of the bend, i.e. the derivative of the z-axis.
    float bendDerivative = bladeProperties.bend * bladeProperties.bendExponent * pow(bladePercentage, bladeProperties.bendExponent - 1.0f);
    // The normal is orthogonal to the slope.
    return float3(0.0f, 1.0f, -bendDerivative);
}

float3 GetSegmentPositionTS(float bladePercentage, BladeProperties bladeProperties)
{
    float x = 0.5f * bladeProperties.width * bladeProperties.GetBladeShape(bladePercentage);
    float y = bladeProperties.height * bladePercentage;
    // The closer the vertex is to the tip of the blade the more it gets affected by the blade bend.
    // This will effectively create a forwards arch from bottom to top.
    float z = bladeProperties.bend * pow(bladePercentage, bladeProperties.bendExponent);

    return float3(x, y, z);
}

WindProperties GetWindPropeties(float3 bladeRootPositionWS, float4x4 worldToTangentTransform)
{
    float4x4 worldToWindTransform = AxesToRotationTransform(_WindRightDirection, _WindUpDirection, _WindForwardDirection);
    float2 uv = _WindFrequency * (TransformPosition(worldToWindTransform, bladeRootPositionWS).xz - float2(0.0f, _Time.y));
    float windIntensity = SAMPLE_TEXTURE2D_LOD(_WindIntensityTexture, sampler_WindIntensityTexture, uv, 0).r;
    float windSpeed = windIntensity * _WindSpeed;
    float3 windDirectionTS = TransformDirection(worldToTangentTransform, _WindForwardDirection);
    float3 windCrossDirectionTS = TransformDirection(worldToTangentTransform, _WindRightDirection);

    return WindProperties::Create
    (
        windSpeed * _WindDisplacement * windDirectionTS,
        AngleAxis4x4(windSpeed * radians(_WindTilt), windCrossDirectionTS)
    );
}

InteractionProperties GetInteractionProperties(BladeProperties bladeProperties, float3 bladeRootPositionWS, float4x4 worldToTangentTransform)
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
        float normalizedInteractionHeight = interactionPositionTS.y / bladeProperties.height;
        interactionIntensity *= GetSmallestComponent(smoothstep(float2(-0.05f, -0.25f), 0.0f, float2(normalizedInteractionHeight, 1.0f - normalizedInteractionHeight)));

        return InteractionProperties::Create(AngleAxis4x4(interactionIntensity * radians(_InteractionTilt), interactionCrossDirectionTS));
    #else
        return InteractionProperties::Create(0.0f);
    #endif
}

BladeProperties GetBladeProperties(inout PRNG prng)
{
    float4 bladeShape[2];
    float bladeShapeIndex = prng.NextFloat();
    bladeShape[0] = SAMPLE_TEXTURE2D_LOD(_BladeShapeTexture, sampler_BladeShapeTexture, float2(bladeShapeIndex, 0.0f), 0);
    bladeShape[1] = SAMPLE_TEXTURE2D_LOD(_BladeShapeTexture, sampler_BladeShapeTexture, float2(bladeShapeIndex, 1.0f), 0);

    // Get a random width, height, pitch, bend and bend exponent within the range specified in the inspector.
    // This will determine the general look of the grass blade.
    return BladeProperties::Create
    (
        bladeShape,
        prng.NextFloat(_BladeWidthRange),
        prng.NextFloat(_BladeHeightRange),
        prng.NextFloat(radians(_BladePitchRange)),
        prng.NextFloat(_BladeBendRange),
        prng.NextFloat(_BladeBendExponentRange)
    );
}

float3 GetRandomPositionOnDisc(float3 discCenter, float minDistanceFromDiscCenter, float maxDistanceFromDiscCenter, inout PRNG prng)
{
    float angle = prng.NextFloat(0.0f, 2.0f * PI);
    float x, z;
    sincos(angle, z, x);
    float3 xzDirection = float3(x, 0.0f, z);
    float distance = prng.NextFloat(minDistanceFromDiscCenter, maxDistanceFromDiscCenter);

    return discCenter + distance * xzDirection;
}

float4x4 GetTangentToWorldTransform(GeometryPassInput input, inout PRNG prng)
{
    // The blade is positioned randomly on a disc. The disc is centered on the input vertex position in world space and spans the xz-axes in tangent space.
    float3 bladePosition = GetRandomPositionOnDisc(input.positionWS, _BladeSpreadRange.x, _BladeSpreadRange.y, prng);
    // Matrices can be initialized with vectors wherein each vector specifies a row of the matrix. We want each vector specifying a column instead tho,
    // so we return the transpose.
    float4x4 tangentToWorldTransform = SetTransformTranslation(AxesToRotationTransform(input.tangentWS, input.normalWS, input.bitangentWS), bladePosition);
    // The base of the blade is oriented randomly alond the z-axis.
    float4x4 yawTransform = AngleAxis4x4(prng.NextFloat(0.0f, 2.0f * PI), float3(0.0f, 1.0f, 0.0f));

    return mul(tangentToWorldTransform, yawTransform);
}

void CreateBlade(GeometryPassInput input, inout TriangleStream<FragmentPassInput> outputStream, inout PRNG prng)
{
    // The entire blade position and normal calculations are done in tangent space and later on transformed into world space.
    float4x4 tangentToWorldTransform = GetTangentToWorldTransform(input, prng);
    float4x4 worldToTangentTransform = InvertTranslationRotationTransform(tangentToWorldTransform);

    float3 bladeRootPositionWS = GetTransformTranslation(tangentToWorldTransform);

    BladeProperties bladeProperties = GetBladeProperties(prng);
    InteractionProperties interactionProperties = GetInteractionProperties(bladeProperties, bladeRootPositionWS, worldToTangentTransform);
    WindProperties windProperties = GetWindPropeties(bladeRootPositionWS, worldToTangentTransform);

    // The blade itself is pitched forwards by a random angle in accordance with the blade pitch range specified in the inspector.
    // We apply this only after calculating the wind/interaction properties because we don't want the pitch to affect these properties.
    float4x4 pitchTransform = AngleAxis4x4(bladeProperties.pitch, float3(1.0f, 0.0f, 0.0f));
    tangentToWorldTransform = mul(tangentToWorldTransform, pitchTransform);

    for (int segment = 0; segment <= SEGMENT_COUNT; segment++)
    {
        float bladePercentage = GetBladePercentage(segment);
        float3 segmentPositionTS = GetSegmentPositionTS(bladePercentage, bladeProperties);
        float3 segmentNormalTS = GetSegmentNormalTS(bladePercentage, bladeProperties);

        Output(input, windProperties, interactionProperties, bladeProperties, bladePercentage, tangentToWorldTransform, segmentPositionTS, segmentNormalTS, float2(1.0f, bladePercentage), outputStream);
        Output(input, windProperties, interactionProperties, bladeProperties, bladePercentage, tangentToWorldTransform, float3(-segmentPositionTS.x, segmentPositionTS.yz), segmentNormalTS, float2(0.0f, bladePercentage), outputStream);
    }
    outputStream.RestartStrip();
}

[maxvertexcount(BLADE_COUNT * 2 * (SEGMENT_COUNT + 1))]
void GeometryPass(point GeometryPassInput inputs[1], inout TriangleStream<FragmentPassInput> outputStream)
{
    GeometryPassInput input = inputs[0];
    // Use some arbitrary value depending on the object position of the vertex as the seed.
    PRNG prng = PRNG::Create(input.seed);

    // For each vertex of the original mesh, we create BLADE_COUNT grass blades.
    // Each blade will be positioned randomly on a disc centered on the given input vertex and spanning the xz-axes in mesh tangent space.
    for (int blade = 0; blade < BLADE_COUNT; blade++)
    {
        CreateBlade(input, outputStream, prng);
    }
}

#endif