Shader "Tuntenfisch/Grass/Grass"
{
    Properties
    {
        // Keywords
        [Toggle(_MESH_CONTAINS_BLADE_PROPERTIES)]_MeshContainsGrassBladeProperties ("Mesh Contains Blade Properties", Int) = 0

        // Blade Properties
        [MainTexture]_BladeTexture ("Blade Texture", 2D) = "white" { }
        [NoScaleOffset]_BladeShapeTexture ("Blade Shape Texture", 2D) = "white" { }

        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)]_BladeBaseColor ("Blade Base Color", Color) = (0.0, 0.3921568, 0.0, 1.0)
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)]_BladeTipColor ("Blade Tip Color", Color) = (0.2509804, 0.6588235, 0.2509804, 1.0)

        _BladeBaseColorVariance ("Blade Base Color Variance", Range(0.0, 0.5)) = 0.0
        _BladeTipColorVariance ("Blade Tip Color Variance", Range(0.0, 0.5)) = 0.025

        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)][MinMaxRange(0.0, 2.0)]_BladeWidthRange ("Blade Width Range", Vector) = (0.05, 0.1, 0.0, 0.0)
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)][MinMaxRange(0.0, 2.0)]_BladeHeightRange ("Blade Height Range", Vector) = (0.5, 1.0, 0.0, 0.0)
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)][MinMaxRange(0.0, 90.0)]_BladePropertiesPitchRange ("Blade Pitch Range", Vector) = (0.0, 15.0, 0.0, 0.0)
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)][MinMaxRange(0.0, 2.0)]_BladeBendRange ("Blade Bend Range", Vector) = (0.0, 0.25, 0.0, 0.0)
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)][MinMaxRange(1.0, 8.0)]_BladeBendExponentRange ("Blade Bend Exponent Range", Vector) = (1.5, 2.5, 0.0, 0.0)
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, false)][MinMaxRange(0.0, 2.0)]_BladeSpreadRange ("Blade Spread Range", Vector) = (0.25, 0.5, 0.0, 0.0)

        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, true)]_BladeWidthVariance ("Blade Width Variance", Range(0.0, 1.0)) = 0.025
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, true)]_BladeHeightVariance ("Blade Height Variance", Range(0.0, 1.0)) = 0.25
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, true)]_BladePitchVariance ("Blade Pitch Variance", Range(0.0, 45.0)) = 7.5
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, true)]_BladeBendVariance ("Blade Bend Variance", Range(0.0, 2.0)) = 0.125
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, true)]_BladeBendExponentVariance ("Blade Bend Exponent Variance", Range(0.0, 3.5)) = 0.5
        [ShowIf(_MESH_CONTAINS_BLADE_PROPERTIES, true)]_BladeSpreadVariance ("Blade Spread Variance", Range(0.0, 1.0)) = 0.125

        // Interaction Properties
        _InteractionTilt ("Interaction Tilt", Range(0.0, 90.0)) = 30.0

        // Wind Properties
        [EulerAnglesToAxes(_WindRightDirection, _WindUpDirection, _WindForwardDirection)]_WindRotation ("Wind Rotation", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector]_WindRightDirection ("Wind Right Direction", Vector) = (1.0, 0.0, 0.0, 0.0)
        [HideInInspector]_WindUpDirection ("Wind Up Direction", Vector) = (0.0, 1.0, 0.0, 0.0)
        [HideInInspector]_WindForwardDirection ("Wind Forward Direction", Vector) = (0.0, 0.0, 1.0, 0.0)
        _WindSpeed ("Wind Speed", Range(0.0, 10.0)) = 1.0
        _WindFrequency ("Wind Frequency", Range(0.0, 1.0)) = 0.1
        [NoScaleOffset]_WindIntensityTexture ("Wind Intensity Texture", 2D) = "clear" { }
        _WindTilt ("Wind Tilt", Range(0.0, 90.0)) = 30.0
        _WindDisplacement ("Wind Displacement", Range(0.0, 1.0)) = 0.4
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #include "Packages/com.tuntenfisch.commons/Shaders/Include/PropertyMacros.hlsl"
        #include "Packages/com.tuntenfisch.commons/Shaders/Include/Math.hlsl"
        #include "Packages/com.tuntenfisch.commons/Shaders/Include/Packing.hlsl"
        #include "Packages/com.tuntenfisch.commons/Shaders/Include/Random.hlsl"
        #include "Packages/com.tuntenfisch.commons/Shaders/Include/Unity.hlsl"

        CBUFFER_START(UnityPerMaterial)
        // Blade Properties
        float4 _BladeTexture_ST;
        float4 _BladeShapeTexture_TexelSize;

        #if !defined(_MESH_CONTAINS_BLADE_PROPERTIES)
            float3 _BladeBaseColor;
            float3 _BladeTipColor;
        #endif

        float _BladeBaseColorVariance;
        float _BladeTipColorVariance;

        #if !defined(_MESH_CONTAINS_BLADE_PROPERTIES)
            float2 _BladeWidthRange;
            float2 _BladeHeightRange;
            float2 _BladePitchRange;
            float2 _BladeBendRange;
            float2 _BladeBendExponentRange;
            float2 _BladeSpreadRange;
        #else
            float _BladeWidthVariance;
            float _BladeHeightVariance;
            float _BladePitchVariance;
            float _BladeBendVariance;
            float _BladeBendExponentVariance;
            float _BladeSpreadVariance;
        #endif

        // Interaction Properties
        #if defined(_GRASS_INTERACTION_ENABLED)
            float2 _InteractionAreaSize;                // (interaction area size, inverse interaction area size)
            float2 _InteractionCameraClippingPlanes;    // (distance to near clip plane, distance to far clip plane)
            float _InteractionTilt;
            float4x4 _InteractionToWorldTransform;
            float4x4 _WorldToInteractionTransform;
        #endif

        // Distance Culling Properties
        #if defined(_DISTANCE_CULLING_ENABLED)
            float3 _CullingCameraPosition;
            float2 _SmoothDistanceCullingRange;
        #endif

        // Wind Properties
        float3 _WindRightDirection;
        float3 _WindUpDirection;
        float3 _WindForwardDirection;
        float _WindSpeed;
        float _WindFrequency;
        float _WindTilt;
        float _WindDisplacement;
        CBUFFER_END

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // We want front and back faces to be rendered for the grass so turn of face culling.
            Cull Off

            HLSLPROGRAM

            #pragma require geometry

            // Material Keywords
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            // Unity Keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            // Custom Keywords
            #pragma multi_compile _ _GRASS_INTERACTION_ENABLED
            #pragma multi_compile _ _DISTANCE_CULLING_ENABLED
            #pragma multi_compile _ _MESH_CONTAINS_BLADE_PROPERTIES

            #pragma vertex GrassVertexPass
            #pragma geometry GrassGeometryPass
            #pragma fragment GrassFragmentPass

            #define _FORWARD_LIT_PASS
            
            #include "Include/GrassFragmentPass.hlsl"
            #include "Include/GrassGeometryPass.hlsl"
            #include "Include/GrassVertexPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            // Do not specify the culling mode for the shadow caster mode because it
            // can be set in the mesh renderer inspector anyways.
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            // Custom Keywords
            #pragma multi_compile _ _GRASS_INTERACTION_ENABLED
            #pragma multi_compile _ _DISTANCE_CULLING_ENABLED
            #pragma multi_compile _ _MESH_CONTAINS_BLADE_PROPERTIES

            #pragma vertex GrassVertexPass
            #pragma geometry GrassGeometryPass
            #pragma fragment GrassFragmentPass

            #define _SHADOW_CASTER_PASS

            #include "Include/GrassFragmentPass.hlsl"
            #include "Include/GrassGeometryPass.hlsl"
            #include "Include/GrassVertexPass.hlsl"

            ENDHLSL

        }
    }
    CustomEditor "Tuntenfisch.Grass.Editor.GrassShaderGUI"
}