using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tuntenfisch.Grass.Painter
{
    [CreateAssetMenu(fileName = "Grass Painter Presets", menuName = "Tuntenfisch/Grass/Painter/New Grass Painter Presets")]
    public class GrassPainterPresets : ScriptableObject
    {
        #region Public Properties
        public GrassBladeProperties this[string name]
        {
            get
            {
                int index = m_presets.FindIndex(preset => preset.Name == name);

                if (index == -1)
                {
                    throw new KeyNotFoundException();
                }
                return m_presets[index].BladeProperties;
            }
        }
        #endregion

        #region Inspector Fields
        [HideInInspector]
        [SerializeField]
        private List<Preset> m_presets;
        #endregion

        #region Private Structs, Classes and Enums
        [Serializable]
        public struct Preset
        {
            #region Public Fields
            public string Name;
            public GrassBladeProperties BladeProperties;
            #endregion
        }
        #endregion
    }
}