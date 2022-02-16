using Tuntenfisch.Commons.Coupling.Variables;
using Tuntenfisch.Commons.Environment;
using Unity.Mathematics;
using UnityEngine;

namespace Tuntenfisch.Grass
{
    [ExecuteAlways]
    public class GrassSway : MonoBehaviour
    {
        #region Inspector Fields
        [SerializeField]
        private Material m_grassMaterial;
        [SerializeField]
        private VariableReadReference<WindProperties> m_windProperties;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            m_windProperties.OnCurrentValueChanged += ApplyWindProperties;
            ApplyWindProperties(m_windProperties.CurrentValue);
        }

        private void OnValidate()
        {
            m_windProperties.OnCurrentValueChanged += ApplyWindProperties;
            ApplyWindProperties(m_windProperties.CurrentValue);
        }

        private void OnDisable()
        {
            m_windProperties.OnCurrentValueChanged -= ApplyWindProperties;
        }
        #endregion

        #region Private Methods
        private void ApplyWindProperties(WindProperties windProperties)
        {
            if (m_grassMaterial == null)
            {
                return;
            }
            Matrix4x4 matrix = Matrix4x4.Rotate(Quaternion.Euler(windProperties.Rotation));
            m_grassMaterial.SetVector(GrassShader.WindRotation, new float4(windProperties.Rotation, 0.0f));
            m_grassMaterial.SetVector(GrassShader.WindRightDirection, matrix.GetColumn(0));
            m_grassMaterial.SetVector(GrassShader.WindUpDirection, matrix.GetColumn(1));
            m_grassMaterial.SetVector(GrassShader.WindForwardDirection, matrix.GetColumn(2));
            m_grassMaterial.SetFloat(GrassShader.WindSpeed, windProperties.Speed);
            m_grassMaterial.SetFloat(GrassShader.WindFrequency, windProperties.Frequency);
        }
        #endregion
    }
}