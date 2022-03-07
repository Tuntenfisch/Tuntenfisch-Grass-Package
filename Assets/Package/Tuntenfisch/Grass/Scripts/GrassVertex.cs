using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tuntenfisch.Grass
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassVertex
    {
        #region Public Properties
        public static VertexAttributeDescriptor[] Attributes => s_attributes;

        public float3 PositionOS => m_positionOS;
        public float3 NormalOS => m_normalOS;
        public float4 TangentOS => m_tangentOS;
        public GrassBladeProperties BladeProperties => m_bladeProperties;
        #endregion

        #region Private Fields
        [HideInInspector]
        [SerializeField]
        private float3 m_positionOS;
        [HideInInspector]
        [SerializeField]
        private float3 m_normalOS;
        [HideInInspector]
        [SerializeField]
        private float4 m_tangentOS;
        [HideInInspector]
        [SerializeField]
        private GrassBladeProperties m_bladeProperties;
        #endregion

        #region Private Fields
        private static readonly VertexAttributeDescriptor[] s_attributes =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3)
        };
        #endregion

        #region Public Methods
        public GrassVertex(float3 positionOS, float3 normalOS, float4 tangentOS, GrassBladeProperties bladeProperties)
        {
            m_positionOS = positionOS;
            m_normalOS = normalOS;
            m_tangentOS = tangentOS;
            m_bladeProperties = bladeProperties;
        }
        #endregion
    }
}