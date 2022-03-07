using System;
using System.Runtime.InteropServices;
using Tuntenfisch.Commons.Attributes;
using Unity.Mathematics;
using UnityEngine;

namespace Tuntenfisch.Grass
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassBladeProperties
    {
        #region Public Properties
        public Color BaseColor { get => new Color(Mathf.LinearToGammaSpace(m_baseColor.x), Mathf.LinearToGammaSpace(m_baseColor.y), Mathf.LinearToGammaSpace(m_baseColor.z)); set => new float3(value.linear.r, value.linear.b, value.linear.g); }
        public Color TipColor { get => new Color(Mathf.LinearToGammaSpace(m_tipColor.x), Mathf.LinearToGammaSpace(m_tipColor.y), Mathf.LinearToGammaSpace(m_tipColor.z)); set => new float3(value.linear.r, value.linear.b, value.linear.g); }
        public float2 LightmapUV { get => m_lightmapUV; set => m_lightmapUV = value; }
        public int ShapeIndex { get => (int)math.asuint(m_shapeIndex); set => m_shapeIndex = math.asfloat((uint)value); }
        public float Width { get => m_width; set => m_width = value; }
        public float Height { get => m_height; set => m_height = value; }
        public float Pitch { get => m_pitch; set => m_pitch = value; }
        public float Bend { get => m_bend; set => m_bend = value; }
        public float BendExponent { get => m_bendExponent; set => m_bendExponent = value; }
        public float Spread { get => m_spread; set => m_spread = value; }
        #endregion

        #region Inspector Fields
        [FloatColor(isLinear: true)]
        [SerializeField]
        private float3 m_baseColor;
        [FloatColor(isLinear: true)]
        [SerializeField]
        private float3 m_tipColor;
        [HideInInspector]
        [SerializeField]
        private float2 m_lightmapUV;
        [SerializeField]
        private float m_shapeIndex;
        [Range(0.0f, 2.0f)]
        [SerializeField]
        private float m_width;
        [Range(0.0f, 2.0f)]
        [SerializeField]
        private float m_height;
        [Range(0.0f, 90.0f)]
        [SerializeField]
        private float m_pitch;
        [Range(0.0f, 2.0f)]
        [SerializeField]
        private float m_bend;
        [Range(1.0f, 8.0f)]
        [SerializeField]
        private float m_bendExponent;
        [Range(0.0f, 2.0f)]
        [SerializeField]
        private float m_spread;
        #endregion

        #region Public Methods
        public static GrassBladeProperties Lerp(GrassBladeProperties start, GrassBladeProperties end, float factor)
        {
            if (factor <= 0.0f)
            {
                return start;
            }
            else if (factor >= 1.0f)
            {
                return end;
            }

            return new GrassBladeProperties
            (
                Color.Lerp(start.BaseColor, end.BaseColor, factor),
                Color.Lerp(start.TipColor, end.TipColor, factor),
                math.lerp(start.LightmapUV, end.LightmapUV, factor),
                // There is no reasonable way to interpolate the shape index, so we just return the shape index of the start blade properties.
                start.ShapeIndex,
                math.lerp(start.m_width, end.m_width, factor),
                math.lerp(start.m_height, end.m_height, factor),
                math.lerp(start.m_pitch, end.m_pitch, factor),
                math.lerp(start.m_bend, end.m_bend, factor),
                math.lerp(start.m_bendExponent, end.m_bendExponent, factor),
                math.lerp(start.m_spread, end.m_spread, factor)
            );
        }

        public GrassBladeProperties(Color baseColor, Color tipColor, float2 lightmapUV, int shapeIndex, float width, float height, float pitch, float bend, float bendExponent, float spread)
        {
            baseColor = baseColor.linear;
            tipColor = tipColor.linear;

            m_baseColor = new float3(baseColor.r, baseColor.g, baseColor.b);
            m_tipColor = new float3(tipColor.r, tipColor.g, tipColor.b);
            m_lightmapUV = lightmapUV;
            m_shapeIndex = math.asfloat((uint)shapeIndex);
            m_width = width;
            m_height = height;
            m_pitch = pitch;
            m_bend = bend;
            m_bendExponent = bendExponent;
            m_spread = spread;
        }

        public GrassBladeProperties(GrassBladeProperties other)
        {
            m_baseColor = other.m_baseColor;
            m_tipColor = other.m_tipColor;
            m_lightmapUV = other.m_lightmapUV;
            m_shapeIndex = other.m_shapeIndex;
            m_width = other.m_width;
            m_height = other.m_height;
            m_pitch = other.m_pitch;
            m_bend = other.m_bend;
            m_bendExponent = other.m_bendExponent;
            m_spread = other.m_spread;
        }
        #endregion
    }
}