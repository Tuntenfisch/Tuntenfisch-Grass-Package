using Tuntenfisch.Commons.Editor;
using Tuntenfisch.Commons.Graphics;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tuntenfisch.Grass.Editor
{
    public class GrassBladeShapeEditor : EditorWindow
    {
        #region Public Properties
        public static string Name => ObjectNames.NicifyVariableName(nameof(GrassBladeShapeEditor));
        #endregion

        #region Private Properties
        private Texture2D BladeShapeTexture
        {
            get => m_bladeshapeTexture;

            set
            {
                if (!IsValidTexture(value))
                {
                    return;
                }
                m_bladeshapeTexture = value;
                SelectedBladeShapeIndex = 0;
            }
        }

        private int SelectedBladeShapeIndex
        {
            get => m_selectedBladeShapeIndex;

            set
            {
                if (BladeShapeTexture == null)
                {
                    return;
                }
                m_selectedBladeShapeIndex = math.clamp(value, 0, BladeShapeTexture.width - 1);

                Color[] pixels = BladeShapeTexture.GetPixels(m_selectedBladeShapeIndex, 0, 1, 2);

                for (int valueIndex = 0; valueIndex < m_graph.ValueCount; valueIndex++)
                {
                    m_graph[valueIndex] = pixels[valueIndex / 4][valueIndex % 4];
                }
                m_graph.SetFlags(Graph.GraphFlags.Dirty, false);
            }
        }
        #endregion

        #region Private Fields
        private static readonly string[] s_popupOptions = { "New" };

        private const int c_textureHeight = 2;
        private const TextureFormat c_textureFormat = TextureFormat.RGBA32;

        private readonly Color[] m_defaultBladeShape = { new Color(0.82f, 0.95f, 1.0f, 0.97f), new Color(0.85f, 0.65f, 0.37f, 0.0f) };

        private Graph m_graph;
        private Texture2D m_bladeshapeTexture;
        private int m_selectedBladeShapeIndex;
        private string m_warningMessage;
        #endregion

        #region Unity Callbacks
        [MenuItem("Tuntenfisch/Grass/Grass Blade Shape Editor")]
        public static void Open()
        {
            GetWindow<GrassBladeShapeEditor>(Name, typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
        }

        public static void Open(Texture2D texture)
        {
            GrassBladeShapeEditor editor = GetWindow<GrassBladeShapeEditor>(Name, typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
            editor.BladeShapeTexture = texture;
        }

        private void OnEnable()
        {
            m_graph = new Graph(new float2(0.0f, 1.0f), new float2(0.0f, 1.0f), new int2(8, 5));
            SelectedBladeShapeIndex = 0;
        }

        private void OnGUI()
        {
            OnBladeShapeTextureSelectionGUI();
            OnBladeShapeGUI();

            if (!string.IsNullOrEmpty(m_warningMessage))
            {
                UnityEditor.EditorGUILayout.HelpBox(m_warningMessage, MessageType.Error);
            }
        }
        #endregion

        #region Private Methods
        private bool IsValidTexture(Texture2D texture)
        {
            if (texture == null)
            {
                m_warningMessage = string.Empty;
                return true;
            }

            if (!texture.isReadable)
            {
                m_warningMessage = "Selected texture asset is invalid. Texture must be readable.";
                return false;
            }

            if (texture.height != c_textureHeight)
            {
                m_warningMessage = $"Selected texture asset is invalid. Texture height must be {c_textureHeight}.";
                return false;
            }

            if (texture.format != c_textureFormat)
            {
                m_warningMessage = $"Selected texture asset is invalid. Texture format must be {c_textureFormat}.";
                return false;
            }
            m_warningMessage = string.Empty;
            return true;
        }

        private void OnBladeShapeTextureSelectionGUI()
        {
            UnityEditor.EditorGUI.BeginChangeCheck();
            int selectedPopupOptionIndex = Commons.Editor.EditorGUILayout.ObjectFieldWithPopupOptions(GUIContent.none, ref m_bladeshapeTexture, -1, s_popupOptions);

            switch (selectedPopupOptionIndex)
            {
                case 0:
                    CreateNewBladeShapeTexture();
                    break;
            }

            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                BladeShapeTexture = m_bladeshapeTexture;
            }
        }

        private void CreateNewBladeShapeTexture()
        {
            string assetPath = EditorUtility.SaveFilePanelInProject("Save Texture", "New Grass Blade Shape Texture", "png", "");

            if (assetPath.Length != 0)
            {
                Texture2D bladeShapeTexture = new Texture2D(1, c_textureHeight, c_textureFormat, false);
                bladeShapeTexture.SetPixels(0, 0, 1, 2, m_defaultBladeShape);
                bladeShapeTexture.Apply();
                bladeShapeTexture.SaveAsset(assetPath);
                TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                textureImporter.sRGBTexture = false;
                // The blade shape texture width can be a non power of two depending on how
                // many blade shapes the user added to the texture. If we don't set the npot
                // scale mode to none, things go wrong...
                textureImporter.npotScale = TextureImporterNPOTScale.None;
                textureImporter.isReadable = true;
                textureImporter.mipmapEnabled = false;
                textureImporter.filterMode = FilterMode.Point;
                textureImporter.wrapMode = TextureWrapMode.Clamp;
                textureImporter.SaveAndReimport();
                BladeShapeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
        }

        private void OnBladeShapeGUI()
        {
            if (BladeShapeTexture == null)
            {
                return;
            }

            UnityEditor.EditorGUILayout.BeginHorizontal();
            DisplayPreviousBladeShapeButton();
            DisplayRemoveBladeShapeButton();
            UnityEditor.EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button($"{SelectedBladeShapeIndex + 1} / {BladeShapeTexture.width}", GUILayout.Height(EditorGUIIcons.IconSize.y));
            UnityEditor.EditorGUI.EndDisabledGroup();
            DisplayAddBladeShapeButton();
            DisplayNextBladeShapeButton();
            UnityEditor.EditorGUILayout.EndHorizontal();
            DisplaySaveBladeShapeButton();
            m_graph.OnGUI();
        }

        private void DisplayPreviousBladeShapeButton()
        {
            UnityEditor.EditorGUI.BeginDisabledGroup(SelectedBladeShapeIndex <= 0);

            if (GUILayout.Button(EditorGUIIcons.LeftArrowIcon))
            {
                SelectedBladeShapeIndex--;
            }
            UnityEditor.EditorGUI.EndDisabledGroup();
        }

        private void DisplayRemoveBladeShapeButton()
        {
            UnityEditor.EditorGUI.BeginDisabledGroup(BladeShapeTexture.width <= 1);

            if (GUILayout.Button(EditorGUIIcons.MinusIcon))
            {
                BladeShapeTexture.RemoveColumn(SelectedBladeShapeIndex);
                BladeShapeTexture.Apply();
                BladeShapeTexture.SaveAsset(AssetDatabase.GetAssetPath(BladeShapeTexture.GetInstanceID()));
                SelectedBladeShapeIndex--;
            }
            UnityEditor.EditorGUI.EndDisabledGroup();
        }

        private void DisplayAddBladeShapeButton()
        {
            if (GUILayout.Button(EditorGUIIcons.PlusIcon))
            {
                BladeShapeTexture.InsertColumn(SelectedBladeShapeIndex + 1, m_defaultBladeShape);
                BladeShapeTexture.Apply();
                BladeShapeTexture.SaveAsset(AssetDatabase.GetAssetPath(BladeShapeTexture.GetInstanceID()));
                SelectedBladeShapeIndex++;
            }
        }

        private void DisplayNextBladeShapeButton()
        {
            UnityEditor.EditorGUI.BeginDisabledGroup(SelectedBladeShapeIndex >= BladeShapeTexture.width - 1);

            if (GUILayout.Button(EditorGUIIcons.RightArrowIcon))
            {
                SelectedBladeShapeIndex++;
            }
            UnityEditor.EditorGUI.EndDisabledGroup();
        }

        private void DisplaySaveBladeShapeButton()
        {
            UnityEditor.EditorGUI.BeginDisabledGroup(!m_graph.HasFlags(Graph.GraphFlags.Dirty));

            if (GUILayout.Button("Save Blade Shape"))
            {
                Color[] pixels = new Color[2];

                if (SelectedBladeShapeIndex != -1)
                {
                    for (int valueIndex = 0; valueIndex < m_graph.ValueCount; valueIndex++)
                    {
                        pixels[valueIndex / 4][valueIndex % 4] = m_graph[valueIndex];
                    }
                    BladeShapeTexture.SetPixels(SelectedBladeShapeIndex, 0, 1, 2, pixels);
                    BladeShapeTexture.Apply();
                    BladeShapeTexture.SaveAsset(AssetDatabase.GetAssetPath(BladeShapeTexture.GetInstanceID()));
                }
                m_graph.SetFlags(Graph.GraphFlags.Dirty, false);
            }
            UnityEditor.EditorGUI.EndDisabledGroup();
        }
        #endregion
    }
}