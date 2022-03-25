using System.Linq;
using Tuntenfisch.Commons.Editor;
using Tuntenfisch.Commons.Graphics.UI;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tuntenfisch.Grass.Painter.Editor
{
    public class GrassPainterPresetsDrawer
    {
        #region Private Properties
        private bool HasPresets => m_presetsProperty.objectReferenceValue != null;

        private int SelectedPresetIndex
        {
            get => m_selectedPresetIndex;

            set
            {
                if (value != -1)
                {
                    m_grassPainter.SetBladeProperties(m_presetsArrayProperty.GetArrayElementAtIndex(value).GetValue<GrassPainterPresets.Preset>().BladeProperties);
                }
                m_selectedPresetIndex = value;
            }
        }
        #endregion

        #region Private Fields
        const string c_illegalCharacters = "/?<>\\:*|\"";

        private readonly GrassPainter m_grassPainter;
        private readonly SerializedProperty m_presetsProperty;
        private SerializedObject m_presetsObject;
        private SerializedProperty m_presetsArrayProperty;
        private string m_addPresetName;
        private string m_renamePresetName;
        private int m_selectedPresetIndex;
        #endregion

        #region Public Methods
        public GrassPainterPresetsDrawer(SerializedProperty presetsProperty, GrassPainter grassPainter)
        {
            m_grassPainter = grassPainter;
            m_presetsProperty = presetsProperty;

            if (HasPresets)
            {
                OnPresetsSelected();
            }
        }

        public void OnInspectorGUI()
        {
            UnityEditor.EditorGUI.BeginChangeCheck();
            UnityEditor.EditorGUILayout.Space(0.5f * UnityEditor.EditorGUIUtility.singleLineHeight);
            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.PrefixLabel(m_presetsProperty.displayName, EditorStyles.objectField, EditorStyles.boldLabel);
            m_presetsProperty.objectReferenceValue = UnityEditor.EditorGUILayout.ObjectField(GUIContent.none, m_presetsProperty.objectReferenceValue, typeof(GrassPainterPresets), false);
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                if (HasPresets)
                {
                    OnPresetsSelected();
                }
                else
                {
                    OnPresetsDeselected();
                }
            }

            if (HasPresets)
            {
                UnityEditor.EditorGUIUtility.labelWidth -= Commons.Editor.EditorGUIUtility.Spacing.x;
                UnityEditor.EditorGUILayout.BeginVertical(EditorGUIStyles.BoxStyle);
                DrawTextFieldWithPlaceholder(new GUIContent("Add"), ref m_addPresetName, "type name and press enter");
                UnityEditor.EditorGUI.BeginDisabledGroup(SelectedPresetIndex == -1);
                DrawTextFieldWithPlaceholder(new GUIContent("Rename"), ref m_renamePresetName, "type name and press enter", true);
                UnityEditor.EditorGUI.EndDisabledGroup();
                DrawPresets();
                UnityEditor.EditorGUILayout.EndVertical();
                UnityEditor.EditorGUIUtility.labelWidth = 0.0f;
            }
        }

        public void DeselectPreset() => SelectedPresetIndex = -1;
        #endregion

        #region Private Methods
        private void OnPresetsSelected()
        {
            m_presetsObject = new SerializedObject(m_presetsProperty.objectReferenceValue);
            m_presetsArrayProperty = m_presetsObject.FindProperty("m_presets");
            SelectedPresetIndex = m_presetsArrayProperty.arraySize > 0 ? 0 : -1;
        }

        private void OnPresetsDeselected()
        {
            m_presetsObject = null;
            m_presetsArrayProperty = null;
            SelectedPresetIndex = -1;
        }

        private void DrawTextFieldWithPlaceholder(GUIContent label, ref string text, string placeholder, bool renameSelected = false)
        {
            KeyCode keyCode = Commons.Editor.EditorGUILayout.TextFieldWithPlaceholder(label, ref text, placeholder);

            if (!string.IsNullOrEmpty(text) && !IsValidName(text))
            {
                UnityEditor.EditorGUILayout.HelpBox($"Name cannot contain the following characters: \t{c_illegalCharacters}", MessageType.Info);
            }

            switch (keyCode)
            {
                case KeyCode.Escape:
                    text = string.Empty;
                    break;

                case KeyCode.Return:
                    if (!IsValidName(text))
                    {
                        return;
                    }

                    GrassPainterPresets.Preset preset;

                    if (renameSelected)
                    {
                        preset = new GrassPainterPresets.Preset { Name = text, BladeProperties = m_presetsArrayProperty.GetArrayElementAtIndex(SelectedPresetIndex).GetValue<GrassPainterPresets.Preset>().BladeProperties };
                        m_presetsArrayProperty.DeleteArrayElementAtIndex(SelectedPresetIndex);
                    }
                    else
                    {
                        preset = new GrassPainterPresets.Preset { Name = text, BladeProperties = m_grassPainter.BladeProperties };
                    }
                    SelectedPresetIndex = AddOrUpdatePreset(preset);
                    text = string.Empty;
                    break;
            }
        }

        private bool IsValidName(string name)
        {
            return !string.IsNullOrEmpty(name) && !c_illegalCharacters.Any(name.Contains);
        }

        private int AddOrUpdatePreset(GrassPainterPresets.Preset preset)
        {
            if (m_presetsArrayProperty.arraySize == 0 || preset.Name.CompareTo(m_presetsArrayProperty.GetArrayElementAtIndex(m_presetsArrayProperty.arraySize - 1).GetValue<GrassPainterPresets.Preset>().Name) > 0)
            {
                m_presetsArrayProperty.AppendArrayElement(preset);
                return m_presetsArrayProperty.arraySize - 1;
            }

            // Insert the new preset in alphabetical order.
            for (int presetIndex = 0; presetIndex < m_presetsArrayProperty.arraySize; presetIndex++)
            {
                SerializedProperty presetProperty = m_presetsArrayProperty.GetArrayElementAtIndex(presetIndex);
                int order = preset.Name.CompareTo(presetProperty.GetValue<GrassPainterPresets.Preset>().Name);

                if (order < 0)
                {
                    m_presetsArrayProperty.InsertArrayElementAtIndex(presetIndex, preset);
                    return presetIndex;
                }
                else if (order == 0)
                {
                    presetProperty.SetValue(preset);
                    return presetIndex;
                }
            }
            return -1;
        }

        private void DrawPresets()
        {
            m_presetsObject.Update();

            if (m_presetsArrayProperty.arraySize > 0)
            {
                DrawHeader();
                UnityEditor.EditorGUILayout.BeginVertical(UnityEditorInternal.ReorderableList.defaultBehaviours.boxBackground);

                for (int presetIndex = 0; presetIndex < m_presetsArrayProperty.arraySize; presetIndex++)
                {
                    DrawPreset(presetIndex, m_presetsArrayProperty.GetArrayElementAtIndex(presetIndex), presetIndex == SelectedPresetIndex);
                }
                UnityEditor.EditorGUILayout.EndVertical();
                DrawRemovePresetButton();
            }
            m_presetsObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(0, 5.0f, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                UnityEditorInternal.ReorderableList.defaultBehaviours.emptyHeaderBackground.Draw(headerRect, false, false, false, false);
            }
        }

        private void DrawPreset(int presetIndex, SerializedProperty presetProperty, bool isSelected)
        {
            GrassPainterPresets.Preset preset = presetProperty.GetValue<GrassPainterPresets.Preset>();

            Rect rect = GUILayoutUtility.GetRect(0.0f, 0.0f, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIIcons.IconSize.y + 2.0f * UnityEditor.EditorGUIUtility.standardVerticalSpacing));
            Rect backgroundRect = new Rect(rect).Pad(-UnityEditor.EditorGUIUtility.standardVerticalSpacing);
            Rect contentRect = new Rect(backgroundRect).Pad(-UnityEditor.EditorGUIUtility.standardVerticalSpacing);
            Rect labelRect = new Rect(contentRect.x, contentRect.y, EditorStyles.label.CalcSize(new GUIContent(preset.Name)).x, UnityEditor.EditorGUIUtility.singleLineHeight);
            Rect saveRect = new Rect(new float2(contentRect.xMax - EditorGUIIcons.IconSize.x, contentRect.y), EditorGUIIcons.IconSize);

            if (Event.current.type == EventType.Repaint)
            {
                UnityEditorInternal.ReorderableList.defaultBehaviours.elementBackground.Draw(backgroundRect, false, isSelected, isSelected, isSelected);
            }
            UnityEditor.EditorGUI.LabelField(labelRect, preset.Name);

            if (GUI.Button(saveRect, EditorGUIIcons.SaveIcon))
            {
                AddOrUpdatePreset(new GrassPainterPresets.Preset { Name = preset.Name, BladeProperties = m_grassPainter.BladeProperties });
                SelectedPresetIndex = presetIndex;
            }

            switch (Event.current.type)
            {
                case EventType.MouseDown:
                    if (backgroundRect.Contains(Event.current.mousePosition))
                    {
                        SelectedPresetIndex = presetIndex;
                        Event.current.Use();
                    }
                    break;
            }
        }

        private void DrawRemovePresetButton()
        {
            Rect rect = GUILayoutUtility.GetRect(0.0f, 0.0f, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIIcons.IconSize.y));
            Rect backgroundRect = Rect.MinMaxRect(rect.xMax - 10.0f - EditorGUIIcons.IconSize.x, rect.yMin, rect.xMax - 10.0f, rect.yMax);
            Rect contentRect = new Rect(backgroundRect).Pad(-UnityEditor.EditorGUIUtility.standardVerticalSpacing);
            contentRect.y -= 0.5f * UnityEditor.EditorGUIUtility.standardVerticalSpacing;

            if (Event.current.type == EventType.Repaint)
            {
                UnityEditorInternal.ReorderableList.defaultBehaviours.footerBackground.Draw(backgroundRect, false, false, false, false);
            }
            UnityEditor.EditorGUI.BeginDisabledGroup(SelectedPresetIndex == -1);

            if (GUI.Button(contentRect, EditorGUIIcons.MinusIcon, UnityEditorInternal.ReorderableList.defaultBehaviours.preButton))
            {
                m_presetsArrayProperty.DeleteArrayElementAtIndex(SelectedPresetIndex);
                SelectedPresetIndex = -1;
            }
            UnityEditor.EditorGUI.EndDisabledGroup();
        }
        #endregion
    }
}