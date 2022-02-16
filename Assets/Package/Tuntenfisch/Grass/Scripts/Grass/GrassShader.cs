using UnityEngine;

namespace Tuntenfisch.Grass
{
    public static class GrassShader
    {
        // Blade Properties
        public static readonly int BladeBaseColor = Shader.PropertyToID("_BladeBaseColor");
        public static readonly int BladeTipColor = Shader.PropertyToID("_BladeTipColor");
        public static readonly int BladeTexture = Shader.PropertyToID("_BladeTexture");
        public static readonly int BladeShapeTexture = Shader.PropertyToID("_BladeShapeTexture");
        public static readonly int BladeWidthRange = Shader.PropertyToID("_BladeWidthRange");
        public static readonly int BladeHeightRange = Shader.PropertyToID("_BladeHeightRange");
        public static readonly int BladePitchRange = Shader.PropertyToID("_BladePitchRange");
        public static readonly int BladeBendRange = Shader.PropertyToID("_BladeBendRange");
        public static readonly int BladeBendExponentRange = Shader.PropertyToID("_BladeBendExponentRange");
        public static readonly int BladeSpreadRange = Shader.PropertyToID("_BladeSpreadRange");

        // Interaction Properties
        public static readonly int InteractionTexture = Shader.PropertyToID("_InteractionTexture");
        public static readonly int InteractionDepthTexture = Shader.PropertyToID("_InteractionDepthTexture");
        public static readonly int InteractionAreaSize = Shader.PropertyToID("_InteractionAreaSize");
        public static readonly int InteractionCameraClippingPlanes = Shader.PropertyToID("_InteractionCameraClippingPlanes");
        public static readonly int InteractionTilt = Shader.PropertyToID("_InteractionTilt");
        public static readonly int InteractionToWorldTransform = Shader.PropertyToID("_InteractionToWorldTransform");
        public static readonly int WorldToInteractionTransform = Shader.PropertyToID("_WorldToInteractionTransform");

        // Wind Properties
        public static readonly int WindRotation = Shader.PropertyToID("_WindRotation");
        public static readonly int WindRightDirection = Shader.PropertyToID("_WindRightDirection");
        public static readonly int WindUpDirection = Shader.PropertyToID("_WindUpDirection");
        public static readonly int WindForwardDirection = Shader.PropertyToID("_WindForwardDirection");
        public static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        public static readonly int WindFrequency = Shader.PropertyToID("_WindFrequency");
        public static readonly int WindIntensityTexture = Shader.PropertyToID("_WindIntensityTexture");
        public static readonly int WindTilt = Shader.PropertyToID("_WindTilt");
        public static readonly int WindDisplacement = Shader.PropertyToID("_WindDisplacement");

        // Keywords
        public static readonly string GrassInteractionEnabled = "_GRASS_INTERACTION_ENABLED";
    }
}