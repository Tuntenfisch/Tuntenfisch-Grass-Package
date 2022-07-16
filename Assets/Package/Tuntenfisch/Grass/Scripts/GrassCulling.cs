using Tuntenfisch.Commons.Attributes;
using Tuntenfisch.Commons.Coupling.Variables;
using Unity.Mathematics;
using UnityEngine;

namespace Tuntenfisch.Grass
{
    public class GrassCulling : MonoBehaviour
    {
        #region Inspector Fields
        [SerializeField]
        private Material m_grassMaterial;
        [SerializeField]
        private VariableReadReference<Camera> m_cullingCamera;
        [Tooltip
        (
            "Specifies in which distance range to smoothly cull grass clusters. " +
            "Distance used is grass cluster to culling camera distance. Any grass clusters " +
            "closer than the specified range won't be culled at all. Smiliarly, any grass " +
            "clusters above the specified range will be culled completely."
        )]
        [Header("Distance Culling")]
        [MinMaxRange(0.0f, 2000.0f)]
        [SerializeField]
        private float2 m_smoothDistanceCullingRange = new float2(100.0f, 400.0f);
        #endregion

        #region Unity Callbacks
        private void Start()
        {
            m_grassMaterial.EnableKeyword(GrassShader.DistanceCullingEnabled);
            ApplyCullingProperties();
        }

        private void Update()
        {
            if (m_cullingCamera.CurrentValue.transform.hasChanged)
            {
                ApplyCullingProperties();
                m_cullingCamera.CurrentValue.transform.hasChanged = false;
            }
        }

        private void OnDestroy()
        {
            m_grassMaterial.DisableKeyword(GrassShader.DistanceCullingEnabled);
        }

        private void OnValidate()
        {
            ApplyCullingProperties();
        }
        #endregion

        #region Private Methods
        private void ApplyCullingProperties()
        {
            if (m_cullingCamera.CurrentValue == null || m_grassMaterial == null)
            {
                return;
            }
            m_grassMaterial.SetVector(GrassShader.CullingCameraPosition, m_cullingCamera.CurrentValue.transform.position);
            m_grassMaterial.SetVector(GrassShader.SmoothDistanceCullingRange, new float4(m_smoothDistanceCullingRange, 0.0f, 0.0f));
        }
        #endregion
    }
}