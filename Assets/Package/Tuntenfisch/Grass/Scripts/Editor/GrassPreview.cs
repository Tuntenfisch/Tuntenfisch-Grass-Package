using System;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tuntenfisch.Grass.Editor
{
    public class GrassPreview : IDisposable
    {
        #region Private Fields
        private const float c_distance = 5.0f;
        private const float c_zoomSpeed = 1.5f / 3.0f;
        private const float c_minFOV = 10.0f;
        private const float c_maxFOV = 40.0f;
        private const float c_orbitSpeed = 1.0f;
        private const float c_minPitchAngle = -15.0f;
        private const float c_maxPitchAngle = 85.0f;

        // Keys used for storing preview camera parameter in editor prefs.
        private const string c_cameraFOVKey = "iQUPOPOLIidYnDN8";
        private const string c_cameraYawAngleKey = "1bkZnQ9orI5BoR6I";
        private const string c_cameraPitchAngleKey = "FXbPH18C9Y1fGv4G";


        private readonly PreviewRenderUtility m_renderer;
        private float m_cameraYawAngle;
        private float m_cameraPitchAngle;
        private readonly GrassVertex[] m_vertices;
        private readonly Mesh m_mesh;
        #endregion

        #region Public Methods
        public GrassPreview()
        {
            m_renderer = new PreviewRenderUtility();
            m_renderer.camera.transform.SetPositionAndRotation(new float3(0.0f, 0.0f, -c_distance), Quaternion.identity);
            m_renderer.lights[0].intensity = 1.0f;
            m_renderer.lights[0].transform.rotation = Quaternion.Euler(50.0f, -90.0f, 0.0f);

            // We have the preview camera parameters saved so they don't reset each time the preview is reinstantiated.
            m_renderer.camera.fieldOfView = EditorPrefs.GetFloat(c_cameraFOVKey, 0.5f * (c_minFOV + c_maxFOV));
            m_cameraYawAngle = EditorPrefs.GetFloat(c_cameraYawAngleKey, m_renderer.camera.transform.rotation.eulerAngles.y);
            m_cameraPitchAngle = EditorPrefs.GetFloat(c_cameraPitchAngleKey, m_renderer.camera.transform.rotation.eulerAngles.x);
            // Calling orbit with a delta of 0 aligns the camera for us.
            OrbitCamera(0.0f);

            const float offset = 0.3f;

            m_vertices = new GrassVertex[]
            {
                new GrassVertex(new float3(-offset, 0.0f, offset), new float3(0.0f, 1.0f, 0.0f), new float4(-1.0f, 0.0f, 0.0f, -1.0f), new GrassBladeProperties()),
                new GrassVertex(new float3(offset, 0.0f, offset), new float3(0.0f, 1.0f, 0.0f), new float4(-1.0f, 0.0f, 0.0f, -1.0f), new GrassBladeProperties()),
                new GrassVertex(new float3(0.0f, 0.0f, -offset), new float3(0.0f, 1.0f, 0.0f), new float4(-1.0f, 0.0f, 0.0f, -1.0f), new GrassBladeProperties()),
                new GrassVertex(new float3(0.0f, 0.0f, 0.0f), new float3(0.0f, 1.0f, 0.0f), new float4(-1.0f, 0.0f, 0.0f, -1.0f), new GrassBladeProperties()),
            };
            int[] indices = Enumerable.Range(0, m_vertices.Length).ToArray();

            m_mesh = new Mesh();
            m_mesh.SetVertexBufferParams(indices.Length, GrassVertex.Attributes);
            m_mesh.SetIndices(indices, MeshTopology.Points, 0);
            // Assign some arbitrarily large bounds to ensure the mesh will always be drawn.
            m_mesh.bounds = new Bounds(new float3(0.0f), new float3(100.0f));
        }

        public void Dispose()
        {
            // Save the current preview camera parameters.
            EditorPrefs.SetFloat(c_cameraFOVKey, m_renderer.camera.fieldOfView);
            EditorPrefs.SetFloat(c_cameraYawAngleKey, m_cameraYawAngle);
            EditorPrefs.SetFloat(c_cameraPitchAngleKey, m_cameraPitchAngle);
            m_renderer.Cleanup();
            UnityEngine.Object.DestroyImmediate(m_mesh);
        }

        public void DrawPreview(Rect rect, GUIStyle backgroundStyle, GrassBladeProperties bladeProperties, Material material)
        {
            switch (Event.current.type)
            {
                case EventType.ScrollWheel:
                    ZoomCamera(Event.current.delta);
                    Event.current.Use();
                    break;

                case EventType.MouseDrag:
                    OrbitCamera(Event.current.delta);
                    Event.current.Use();
                    break;

                case EventType.Repaint:
                    Draw(rect, backgroundStyle, bladeProperties, material);
                    break;
            }
        }
        #endregion

        #region Private Methods
        private void ZoomCamera(float2 delta)
        {
            m_renderer.camera.fieldOfView = math.clamp(m_renderer.camera.fieldOfView + c_zoomSpeed * delta.y, c_minFOV, c_maxFOV);
        }

        private void OrbitCamera(float2 delta)
        {
            m_cameraYawAngle += c_orbitSpeed * delta.x;
            m_cameraPitchAngle = math.clamp(m_cameraPitchAngle + c_orbitSpeed * delta.y, c_minPitchAngle, c_maxPitchAngle);
            m_renderer.camera.transform.rotation = Quaternion.Euler(m_cameraPitchAngle, m_cameraYawAngle, 0.0f);
            m_renderer.camera.transform.position = -c_distance * m_renderer.camera.transform.forward;
        }

        private void Draw(Rect rect, GUIStyle backgroundStyle, GrassBladeProperties bladeProperties, Material material)
        {
            for (int index = 0; index < m_vertices.Length; index++)
            {
                m_vertices[index] = new GrassVertex(m_vertices[index].PositionOS, m_vertices[index].NormalOS, m_vertices[index].TangentOS, bladeProperties);
            }
            m_mesh.SetVertexBufferData(m_vertices, 0, 0, m_vertices.Length);
            m_renderer.BeginPreview(rect, backgroundStyle);
            m_renderer.DrawMesh(m_mesh, Matrix4x4.Translate(new float3(0.0f, -0.5f * bladeProperties.Height, 0.0f)), material, 0);
            m_renderer.camera.Render();
            m_renderer.EndAndDrawPreview(rect);
        }
        #endregion
    }
}