using System;
using Tuntenfisch.Commons.Coupling.Variables;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace Tuntenfisch.Grass
{
    public class GrassInteraction : MonoBehaviour
    {
        #region Private Properties
        private Camera Camera => m_interactionCamera.CurrentValue;
        #endregion

        #region Inspector Fields
        [SerializeField]
        private Material m_grassMaterial;
        [SerializeField]
        private VariableReadReference<Camera> m_interactionCamera;
        [Range(16, 2048)]
        [SerializeField]
        private int m_renderTextureResolution = 256;
        [SerializeField]
        private LayerMask m_interactionLayer;
        [Header("Debug")]
        [SerializeField]
        private bool m_drawInteractionRenderTexture = false;
        #endregion

        #region Private Fields
        private const float c_debugTextureSizePercentage = 0.2f;
        private RenderTexture m_interactionRenderTexture;
        private RenderTexture m_interactionDepthRenderTexture;
        private Material m_copyDepthMaterial;
        #endregion

        #region Unity Callbacks
        private void Start()
        {
            if (Camera != null)
            {
                CreateRenderTextures(Camera);
                ConfigureCamera(Camera);
                ApplyInteractionProperties();
            }
            m_copyDepthMaterial = Resources.Load<Material>("Materials/CopyDepthMaterial");
            RenderPipelineManager.endCameraRendering += CopyDepth;
            m_grassMaterial.EnableKeyword(GrassShader.GrassInteractionEnabled);
#if UNITY_EDITOR
            SceneView.duringSceneGui += OnSceneGUI;
#endif
        }

        private void LateUpdate()
        {
            if (Camera.transform.hasChanged)
            {
                ApplyInteractionProperties(applyCameraProperties: false);
                Camera.transform.hasChanged = false;
            }
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGUI;
#endif
            m_grassMaterial.DisableKeyword(GrassShader.GrassInteractionEnabled);
            RenderPipelineManager.endCameraRendering -= CopyDepth;
            ReleaseRenderTextures(Camera);
        }

        private void OnValidate()
        {
            m_renderTextureResolution = Mathf.ClosestPowerOfTwo(m_renderTextureResolution);

            if (Application.isPlaying && Camera != null)
            {
                CreateRenderTextures(Camera);
                ConfigureCamera(Camera);
                ApplyInteractionProperties(applyTransformProperties: false);
            }
        }

#if UNITY_EDITOR
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!m_drawInteractionRenderTexture)
            {
                return;
            }
            Handles.BeginGUI();
            float textureSize = math.lerp(0.0f, Screen.height, c_debugTextureSizePercentage);
            Rect interactionRect = new Rect(0.0f, 0.0f, textureSize, textureSize);
            Rect interactionDepthRect = new Rect(textureSize, 0.0f, textureSize, textureSize);
            GUI.DrawTexture(interactionRect, m_interactionRenderTexture);
            GUI.DrawTexture(interactionDepthRect, m_interactionDepthRenderTexture);
            Handles.EndGUI();
        }
#endif
        #endregion

        #region Private Methods
        private void CreateRenderTextures(Camera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }
            CreateRenderTexture(ref m_interactionRenderTexture, RenderTextureFormat.Default);
            CreateRenderTexture(ref m_interactionDepthRenderTexture, RenderTextureFormat.RFloat);
            camera.targetTexture = m_interactionRenderTexture;
            camera.enabled = true;
        }

        private void CreateRenderTexture(ref RenderTexture renderTexture, RenderTextureFormat format)
        {
            if (renderTexture == null || renderTexture.width != m_renderTextureResolution)
            {
                if (renderTexture != null)
                {
                    renderTexture.Release();
                }
                renderTexture = new RenderTexture(m_renderTextureResolution, m_renderTextureResolution, 0, format);
            }
        }

        private void ReleaseRenderTextures(Camera camera)
        {
            if (camera != null)
            {
                camera.enabled = false;
                camera.targetTexture = null;
            }

            if (m_interactionRenderTexture != null)
            {
                m_interactionRenderTexture.Release();
            }

            if (m_interactionDepthRenderTexture != null)
            {
                m_interactionDepthRenderTexture.Release();
            }
        }

        private void ConfigureCamera(Camera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.cullingMask = m_interactionLayer;
        }

        private void CopyDepth(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cullingMask != m_interactionLayer)
            {
                return;
            }
            // We blit the depth off the interaction camera into the color buffer of another render texture so we can feed the interaction depth to the  
            // grass material.
            //
            // Normally this shouldn't be necessary because we theoretically could just use the material's
            // SetTexture(int nameID, RenderTexture value, Rendering.RenderTextureSubElement element) call to assign the depth sub-element to the grass
            // material. But this doesn't seem to work in URP.
            //
            // Another method would have been using the camera's SetTargetBuffers(RenderBuffer colorBuffer, RenderBuffer depthBuffer) call to render
            // color and depth information in two separate render textures so we can avoid using the sub-element specifier mentioned above.
            // But this also doesn't work in URP...
            // 
            // So the solution I came up with is to manually blit the depth into the color buffer of our depth texture after the interaction camera is
            // done rendering. You could also use a ScriptableRendererFeature to do this but then the grass interaction setup becomes more cumbersome
            // because it requires more setup in the editor (assigning render textures manually, etc.).
            Graphics.Blit(null, m_interactionDepthRenderTexture, m_copyDepthMaterial);
        }

        private void ApplyInteractionProperties(bool applyCameraProperties = true, bool applyTransformProperties = true)
        {
            if (applyCameraProperties)
            {
                m_grassMaterial.SetTexture(GrassShader.InteractionTexture, m_interactionRenderTexture);
                m_grassMaterial.SetTexture(GrassShader.InteractionDepthTexture, m_interactionDepthRenderTexture);
                m_grassMaterial.SetVector(GrassShader.InteractionAreaSize, new float4(2.0f * Camera.orthographicSize, 1.0f / (2.0f * Camera.orthographicSize), 0.0f, 0.0f));
                m_grassMaterial.SetVector(GrassShader.InteractionCameraClippingPlanes, new float4(Camera.nearClipPlane, Camera.farClipPlane, 0.0f, 0.0f));
            }

            if (applyTransformProperties)
            {
                m_grassMaterial.SetMatrix(GrassShader.InteractionToWorldTransform, Camera.transform.localToWorldMatrix);
                m_grassMaterial.SetMatrix(GrassShader.WorldToInteractionTransform, Camera.transform.worldToLocalMatrix);
            }
        }
        #endregion
    }
}