using System;
using Tuntenfisch.Grass.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tuntenfisch.Grass.Painter.Editor
{
    [CustomEditor(typeof(GrassPainter))]
    public class GrassPainterEditor : UnityEditor.Editor
    {
        #region Private Properties
        private GrassPainter GrassPainter => target as GrassPainter;

        private GrassPainter.GrassPainterMode Mode
        {
            get => m_mode;

            set
            {
                if (m_mode == GrassPainter.GrassPainterMode.None && value != GrassPainter.GrassPainterMode.None)
                {
                    // We entered paint mode. Save the current tool and active our custom brush tool.
                    m_oldTool = Tools.current;
                    Tools.current = Tool.Custom;
                }

                if (m_mode != GrassPainter.GrassPainterMode.None && value == GrassPainter.GrassPainterMode.None)
                {
                    // We exited paint mode, select the old tool again.
                    Tools.current = m_oldTool;
                }
                m_mode = value;
            }
        }

        private GrassPainterEditorFlags Flags
        {
            get => m_flags;

            set
            {
                const GrassPainterEditorFlags flags = GrassPainterEditorFlags.MousePressed | GrassPainterEditorFlags.Hit;

                if (!HasFlags(flags, all: true) && (value & flags) == flags)
                {
                    // We record an undo operation every time we start painting grass.
                    Undo.RegisterFullObjectHierarchyUndo(GrassPainter, "Paint Grass");
                }
                m_flags = value;
            }
        }
        #endregion

        #region Private Fields
        private const int c_paintGrassButton = 0;

        private SerializedProperty m_grassClusterCapacity;
        private GrassPainterPresetsDrawer m_presetsDrawer;
        private GrassPreview m_grassPreview;
        private int m_controlID;
        private Tool m_oldTool;
        private Hit m_hit;
        private GrassPainter.GrassPainterMode m_mode;
        private GrassPainterEditorFlags m_flags;
        #endregion

        #region Unity Callbacks
        protected virtual void OnEnable()
        {
            m_grassClusterCapacity = serializedObject.FindProperty("m_grassClusterCapacity");
            m_presetsDrawer = new GrassPainterPresetsDrawer(serializedObject.FindProperty("m_presets"), GrassPainter);
            m_grassPreview = new GrassPreview();
            m_controlID = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            m_grassPreview.Dispose();
            Mode = GrassPainter.GrassPainterMode.None;
        }

        public override void OnInspectorGUI()
        {
            Commons.Editor.EditorGUILayout.ScriptHeaderField(serializedObject);

            if (!HasValidMaterial(out string errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error, true);
                Mode = GrassPainter.GrassPainterMode.None;
                return;
            }

            if (GrassPainter.BrushProperties.RaycastLayer == 0)
            {
                EditorGUILayout.HelpBox($"Make sure to select a {ObjectNames.NicifyVariableName(nameof(GrassPainter.BrushProperties.RaycastLayer)).ToLower()} before attempting to paint.", MessageType.Info, true);
            }
            serializedObject.UpdateIfRequiredOrScript();
            DisplayGrassPainterButtons();
            DisplayFreeSpace();
            DisplayMaxGrassClusterCount();
            DisplayProperties();
            m_presetsDrawer.OnInspectorGUI();
            serializedObject.ApplyModifiedProperties();
        }

        public override bool HasPreviewGUI() => HasValidMaterial(out string _);

        public override GUIContent GetPreviewTitle() => new GUIContent($"{ObjectNames.NicifyVariableName(nameof(GrassPainter))} Preview");

        public override void OnInteractivePreviewGUI(Rect rect, GUIStyle backgroundStyle) => m_grassPreview.DrawPreview(rect, backgroundStyle, GrassPainter.BladeProperties, GrassPainter.SharedMaterial);

        private void OnSceneGUI()
        {
            if (Mode == GrassPainter.GrassPainterMode.None)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            SetFlags(GrassPainterEditorFlags.Hit, Physics.Raycast(ray, out m_hit.CurrentHit, float.PositiveInfinity, GrassPainter.BrushProperties.RaycastLayer));

            switch (Event.current.type)
            {
                case EventType.MouseMove:
                    Event.current.Use();
                    break;

                case EventType.MouseDrag:
                    if (Event.current.button == c_paintGrassButton)
                    {
                        Event.current.Use();
                    }
                    break;

                case EventType.MouseDown:
                    if (Event.current.button == c_paintGrassButton)
                    {
                        SetFlags(GrassPainterEditorFlags.MousePressed, true);
                        Event.current.Use();
                    }

                    if (CanPaint())
                    {
                        Paint();
                    }
                    break;

                case EventType.MouseLeaveWindow:
                    SetFlags(GrassPainterEditorFlags.MousePressed, false);
                    break;

                case EventType.MouseUp:
                    if (Event.current.button == c_paintGrassButton)
                    {
                        SetFlags(GrassPainterEditorFlags.MousePressed, false);
                        Event.current.Use();
                    }
                    break;

                case EventType.Repaint:
                    if (HasFlags(GrassPainterEditorFlags.Hit))
                    {
                        DrawBrush();
                    }
                    break;

                case EventType.Layout:
                    HandleUtility.AddDefaultControl(m_controlID);
                    break;
            }

            if (CanPaint(0.5f * GrassPainter.BrushProperties.Radius))
            {
                Paint();
            }
        }
        #endregion

        #region Private Methods
        private void OnUndoRedo()
        {
            GrassPainter.Clear(GrassPainter.ClearGrassFlags.RuntimeGrass);
            GrassPainter.UpdateGrassMeshExplicitly();
        }

        private bool HasValidMaterial(out string errorMessage)
        {
            if (GrassPainter.SharedMaterial == null || GrassPainter.SharedMaterial.shader.name != GrassShader.Name)
            {
                errorMessage = $"Invalid material attached to this game object's mesh renderer. Please attach a material using the \"{GrassShader.Name}\" shader.";
                return false;
            }

            if (!GrassPainter.SharedMaterial.IsKeywordEnabled(GrassShader.MeshContainsBladeProperties))
            {
                errorMessage = $"Grass painting only works if the keyword \"{GrassShader.MeshContainsBladeProperties}\" is enabled. Please enable it for the material of the attached mesh renderer.";
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }

        private void DisplayFreeSpace()
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            Rect progressBarRect = EditorGUI.PrefixLabel(rect, new GUIContent("Free Space"));

            if (progressBarRect.width >= EditorGUIUtility.fieldWidth)
            {
                float percentage = 1.0f - (float)GrassPainter.GrassClusterCount / GrassPainter.GrassClusterCapacity;
                EditorGUI.ProgressBar(progressBarRect, percentage, $"{percentage * 100.0f:0.0}%");
            }
        }

        private void DisplayMaxGrassClusterCount()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(m_grassClusterCapacity.displayName, GUILayout.Width(EditorGUIUtility.labelWidth - 1.0f));
            int maxGrassClusterCount = EditorGUILayout.DelayedIntField(GUIContent.none, m_grassClusterCapacity.intValue);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    GrassPainter.SetGrassClusterCapacity(maxGrassClusterCount);
                }
                catch (ArgumentException exception)
                {
                    string message = exception.Message[0..exception.Message.LastIndexOf("Parameter name")];

                    if (AskForConfirmation($"Cannot set {ObjectNames.NicifyVariableName(nameof(GrassPainter.GrassClusterCapacity)).ToLower()}. {message}\nDo you want to set it anyways?"))
                    {
                        GrassPainter.SetGrassClusterCapacity(maxGrassClusterCount, force: true);
                    }
                }
            }
        }

        private void DisplayGrassPainterButtons()
        {
            EditorGUILayout.BeginHorizontal();
            DisplayGrassPainterButton(GrassPainter.GrassPainterMode.Add, StaticStyle.AddButtonLabel);
            DisplayGrassPainterButton(GrassPainter.GrassPainterMode.Remove, StaticStyle.RemoveButtonLabel);
            DisplayGrassPainterButton(GrassPainter.GrassPainterMode.Replace, StaticStyle.ReplaceButtonLabel);
            DisplayGrassPainterButton(GrassPainter.GrassPainterMode.Copy, StaticStyle.CopyButtonLabel);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(ObjectNames.NicifyVariableName(nameof(GrassPainter.Clear))))
            {
                if (AskForConfirmation("Clearing the grass mesh cannot be undone.\n\nDo you want to proceed?"))
                {
                    GrassPainter.Clear();
                    Undo.ClearUndo(GrassPainter);
                }
            }
        }

        private void DisplayGrassPainterButton(GrassPainter.GrassPainterMode mode, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            bool isToggled = GUILayout.Toggle(mode == Mode, label, "Button");

            if (EditorGUI.EndChangeCheck())
            {
                Mode = isToggled ? mode : GrassPainter.GrassPainterMode.None;
            }
        }

        private void DisplayProperties()
        {
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);

            do
            {
                switch (property.name)
                {
                    case "m_Script":
                        break;

                    case "m_bladeProperties":
                        DisplayBladeProperties(property.Copy());
                        break;

                    default:
                        EditorGUILayout.PropertyField(property, true);
                        break;
                }
            } while (property.NextVisible(false));
        }

        private void DisplayBladeProperties(SerializedProperty bladePropertiesProperty)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Space(0.5f * EditorGUIUtility.singleLineHeight);
            EditorGUILayout.LabelField(bladePropertiesProperty.displayName, EditorStyles.boldLabel);

            int parentDepth = bladePropertiesProperty.depth;
            bladePropertiesProperty.NextVisible(true);

            do
            {
                switch (bladePropertiesProperty.name)
                {
                    case "m_baseColor":
                    case "m_tipColor":
                        DisplayColor(bladePropertiesProperty);
                        break;

                    case "m_shapeIndex":
                        DisplayShapeIndex(bladePropertiesProperty);
                        break;

                    default:
                        EditorGUILayout.PropertyField(bladePropertiesProperty, true);
                        break;
                }
            }
            while (bladePropertiesProperty.NextVisible(false) && bladePropertiesProperty.depth != parentDepth);

            if (EditorGUI.EndChangeCheck())
            {
                m_presetsDrawer.DeselectPreset();
            }
        }

        private void DisplayColor(SerializedProperty colorProperty)
        {
            EditorGUILayout.PropertyField(colorProperty, true);
        }

        private void DisplayShapeIndex(SerializedProperty shapeIndexProperty)
        {
            int shapeIndex = math.asint(shapeIndexProperty.floatValue);
            Texture2D bladeShapeTexture = GrassPainter.SharedMaterial.HasTexture(GrassShader.BladeShapeTexture) ? GrassPainter.SharedMaterial.GetTexture(GrassShader.BladeShapeTexture) as Texture2D : null;

            GUILayoutOption labelWidth = GUILayout.Width(EditorGUIUtility.labelWidth - 1.0f);
            GUILayoutOption controlHeight = GUILayout.Height(Commons.Editor.EditorGUIIcons.IconSize.y);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shape", labelWidth);

            if (bladeShapeTexture != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginDisabledGroup(shapeIndex <= 0);

                if (GUILayout.Button(Commons.Editor.EditorGUIIcons.LeftArrowIcon))
                {
                    shapeIndex--;
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button($"{shapeIndex + 1} / {bladeShapeTexture.width}", controlHeight);
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(shapeIndex >= bladeShapeTexture.width - 1);

                if (GUILayout.Button(Commons.Editor.EditorGUIIcons.RightArrowIcon))
                {
                    shapeIndex++;
                }
                EditorGUI.EndDisabledGroup();

                if (EditorGUI.EndChangeCheck())
                {
                    shapeIndexProperty.floatValue = math.asfloat((uint)math.clamp(shapeIndex, 0, bladeShapeTexture.width - 1));
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Cannot edit grass shape. The material attached to this game object's mesh renderer doesn't have a {ObjectNames.NicifyVariableName(nameof(GrassShader.BladeShapeTexture)).ToLower()} assigned.", MessageType.Warning);
            }
            int selectedPopupOptionIndex = Commons.Editor.EditorGUILayout.PaneOptions(-1, StaticStyle.OpenBladeShapeEditorPopupOptions, controlHeight);

            switch (selectedPopupOptionIndex)
            {
                case 0:
                    GrassBladeShapeEditor.Open(bladeShapeTexture);
                    break;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBrush()
        {
            // Draw brush fill for smoothing radius.
            Color brushColor = StaticStyle.GetBrushColor(Mode);
            Handles.color = brushColor;
            Handles.DrawSolidDisc(m_hit.CurrentHit.point, m_hit.CurrentHit.normal, GrassPainter.BrushProperties.SmoothingRadius);

            // Draw brush edge.
            brushColor.a = 1.0f;
            Handles.color = brushColor;
            Handles.DrawWireDisc(m_hit.CurrentHit.point, m_hit.CurrentHit.normal, GrassPainter.BrushProperties.Radius);

            // Draw brush smoothing.
            brushColor.a *= 0.5f;
            Handles.color = brushColor;
            Handles.DrawWireDisc(m_hit.CurrentHit.point, m_hit.CurrentHit.normal, GrassPainter.BrushProperties.SmoothingRadius);
        }

        private bool CanPaint(float minDistanceMoved = 0.01f)
        {
            return HasFlags(GrassPainterEditorFlags.MousePressed | GrassPainterEditorFlags.Hit, all: true) && math.lengthsq(m_hit.CurrentHit.point - m_hit.LastHit.point) >= minDistanceMoved * minDistanceMoved;
        }

        private void Paint()
        {
            GrassPainter.SetBrushProperties(mode: Mode);
            GrassPainter.Paint(m_hit.CurrentHit.point, m_hit.CurrentHit.normal);
            m_hit.LastHit.point = m_hit.CurrentHit.point;
        }

        private bool AskForConfirmation(string message)
        {
            return EditorUtility.DisplayDialog("Confirmation Dialog", message, "Yes", "No");
        }

        private bool HasFlags(GrassPainterEditorFlags flags, bool all = false) => all ? (Flags & flags) == flags : (Flags & flags) != 0;

        private void SetFlags(GrassPainterEditorFlags flags, bool set) => Flags = set ? Flags | flags : Flags & ~flags;
        #endregion

        #region Private Structs, Classes and Enums
        [Flags]
        private enum GrassPainterEditorFlags
        {
            MousePressed = 2 << 0,
            Hit = 2 << 1
        }

        private struct Hit
        {
            #region Public Fields
            public RaycastHit CurrentHit;
            public RaycastHit LastHit;
            #endregion
        }

        private static class StaticStyle
        {
            #region Public Fields
            public static readonly string[] OpenBladeShapeEditorPopupOptions;
            public static readonly GUIContent AddButtonLabel;
            public static readonly GUIContent RemoveButtonLabel;
            public static readonly GUIContent ReplaceButtonLabel;
            public static readonly GUIContent CopyButtonLabel;
            #endregion

            static StaticStyle()
            {
                OpenBladeShapeEditorPopupOptions = new string[] { $"Open {GrassBladeShapeEditor.Name}" };
                AddButtonLabel = EditorGUIUtility.IconContent("Grid.PaintTool", $"|{GrassPainter.GrassPainterMode.Add}");
                RemoveButtonLabel = EditorGUIUtility.IconContent("Grid.EraserTool", $"|{GrassPainter.GrassPainterMode.Remove}");
                ReplaceButtonLabel = EditorGUIUtility.IconContent("preAudioLoopOff", $"|{GrassPainter.GrassPainterMode.Replace}");
                CopyButtonLabel = EditorGUIUtility.IconContent("Grid.PickingTool", $"|{GrassPainter.GrassPainterMode.Copy}");
            }

            #region Public Methods
            public static Color GetBrushColor(GrassPainter.GrassPainterMode mode)
            {
                return mode switch
                {
                    GrassPainter.GrassPainterMode.Add => new Color(0.0f, 1.0f, 0.0f, 0.1f),
                    GrassPainter.GrassPainterMode.Remove => new Color(1.0f, 0.0f, 0.0f, 0.2f),
                    GrassPainter.GrassPainterMode.Replace => new Color(1.0f, 0.65f, 0.0f, 0.2f),
                    GrassPainter.GrassPainterMode.Copy => new Color(1.0f, 1.0f, 1.0f, 0.1f),
                    _ => throw new ArgumentException("No brush color defined for thise mode.", nameof(mode))
                };
            }
            #endregion
        }
        #endregion
    }
}