Shader "Tuntenfisch Grass/Grass"
{
    Properties
    {
        // Blade Properties
        [HDR]_BladeBaseColor ("Blade Base Color", Color) = (0.0, 0.75, 0.0, 1.0)
        [HDR]_BladeTipColor ("Blade Tip Color", Color) = (0.0, 1.0, 0.0, 1.0)
        [MainTexture]_BladeTexture ("Blade Texture", 2D) = "white" { }
        [NoScaleOffset]_BladeShapeTexture ("Blade Shape Texture", 2D) = "white" { }
        [MinMaxRange(0.05, 2.0)]_BladeWidthRange ("Blade Width Range", Vector) = (0.1, 0.1, 0.1, 0.1)
        [MinMaxRange(0.05, 2.0)]_BladeHeightRange ("Blade Height Range", Vector) = (0.5, 0.5, 0.5, 0.5)
        [MinMaxRange(0.0, 90.0)]_BladePitchRange ("Blade Pitch Range", Vector) = (0.0, 0.0, 0.0, 0.0)
        [MinMaxRange(0.0, 2.0)]_BladeBendRange ("Blade Bend Range", Vector) = (0.0, 0.0, 0.0, 0.0)
        [MinMaxRange(1.0, 8.0)]_BladeBendExponentRange ("Blade Bend Exponent Range", Vector) = (2.0, 2.0, 2.0, 2.0)
        [MinMaxRange(0.05, 2.0)]_BladeSpreadRange ("Blade Spread Range", Vector) = (1.0, 1.0, 1.0, 1.0)

        // Interaction Properties
        [NoScaleOffset]_InteractionTexture ("Interaction Texture", 2D) = "clear" { }
        [NoScaleOffset]_InteractionDepthTexture ("Interaction Depth Texture", 2D) = "clear" { }
        _InteractionAreaSize ("Interaction Area Size", Vector) = (0.0, 0.0, 0.0, 0.0)
        _InteractionCameraClippingPlanes ("Interaction Camera Clipping Planes", Vector) = (0.0, 0.0, 0.0, 0.0)
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

        CBUFFER_START(UnityPerMaterial)
        // Blade Properties
        float4 _BladeBaseColor;
        float4 _BladeTipColor;
        float4 _BladeTexture_ST;
        float2 _BladeWidthRange;
        float2 _BladeHeightRange;
        float2 _BladePitchRange;
        float2 _BladeBendRange;
        float2 _BladeBendExponentRange;
        float2 _BladeSpreadRange;

        // Interaction Properties
        float2 _InteractionAreaSize;                // (interaction area size, inverse interaction area size)
        float2 _InteractionCameraClippingPlanes;    // (distance to near clip plane, distance to far clip plane)
        float _InteractionTilt;
        float4x4 _InteractionToWorldTransform;
        float4x4 _WorldToInteractionTransform;

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
            #pragma multi_compile _FORWARD_LIT_PASS
            #pragma multi_compile _ _GRASS_INTERACTION_ENABLED

            #pragma vertex VertexPass
            #pragma geometry GeometryPass
            #pragma fragment FragmentPass

            #include "Include/PassInputs.hlsl"
            #include "Include/VertexPass.hlsl"
            #include "Include/GeometryPass.hlsl"
            #include "Include/FragmentPass.hlsl"

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
            #pragma multi_compile _SHADOW_CASTER_PASS
            #pragma multi_compile _ _GRASS_INTERACTION_ENABLED

            #pragma vertex VertexPass
            #pragma geometry GeometryPass
            #pragma fragment FragmentPass

            #include "Include/PassInputs.hlsl"
            #include "Include/VertexPass.hlsl"
            #include "Include/GeometryPass.hlsl"
            #include "Include/FragmentPass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "Tuntenfisch.Commons.Editor.GroupedPropertiesShaderGUI"
}