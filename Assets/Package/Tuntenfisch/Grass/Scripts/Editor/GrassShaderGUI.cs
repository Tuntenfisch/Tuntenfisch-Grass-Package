using Tuntenfisch.Commons.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tuntenfisch.Grass.Editor
{
    public class GrassShaderGUI : GroupedPropertiesShaderGUI
    {
        #region Unity Callbacks
        public override void OnMaterialInteractivePreviewGUI(MaterialEditor editor, Rect rect, GUIStyle backgroundStyle)
        {
            Material material = editor.target as Material;

            if (material.IsKeywordEnabled(GrassShader.MeshContainsBladeProperties)) 
            {
                UnityEditor.EditorGUI.HelpBox(rect, $"Preview isn't supported while the keyword \"{GrassShader.MeshContainsBladeProperties}\" is enabled.", MessageType.Info);
                return;
            }
            using GrassPreview grassPreview = new GrassPreview();
            float4 bladeHeightRange = material.GetVector(GrassShader.BladeHeightRange);
            GrassBladeProperties grassBladeProperties = default;
            grassBladeProperties.Height = 0.5f * (bladeHeightRange.x + bladeHeightRange.y);
            grassPreview.DrawPreview(rect, backgroundStyle, grassBladeProperties, material);
        }
        #endregion
    }
}