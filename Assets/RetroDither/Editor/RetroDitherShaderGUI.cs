using UnityEditor;
using UnityEngine;

namespace AAR.RetroDither.Editor
{
    /// <summary>Material authoring only; no runtime component or global shader state.</summary>
    public sealed class RetroDitherShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.LabelField("RETRO / DITHER", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Four palette colors, ordered from ink to highlight. Lighting selects a band; Bayer dithering blends neighboring bands. All controls belong to this material.", MessageType.None);

            Section("Palette");
            using (new EditorGUILayout.HorizontalScope())
            {
                PresetButton(editor, "Nightdrive", "100A24", "4A2E7A", "F25F66", "FFE8AB");
                PresetButton(editor, "Handheld", "0F380F", "306230", "8BAC0F", "9BBC0F");
                PresetButton(editor, "Amber", "17100B", "613A20", "CB813A", "FFE1A3");
                PresetButton(editor, "Glacier", "091D36", "275578", "59B7BB", "E4F4DF");
            }
            Draw(editor, properties, "_ShadowColor", "_DarkColor", "_LightColor", "_HighlightColor");

            Section("Pattern");
            Draw(editor, properties, "_DitherStrength", "_PixelSize", "_PatternSize", "_PatternSpace", "_UVDensity");
            EditorGUILayout.HelpBox("Screen coordinates give a stable display grid. Mesh UV coordinates anchor the pattern to the surface. Pixel Size scales the dither pattern; it does not pixelate geometry.", MessageType.None);

            Section("Tone & lighting");
            Draw(editor, properties, "_Contrast", "_Brightness", "_Exposure", "_Ambient", "_LightWrap", "_Specular", "_Shininess", "_RimStrength", "_RimPower");

            Section("Surface input");
            editor.TexturePropertySingleLine(new GUIContent("Tone texture / tint"), FindProperty("_BaseMap", properties), FindProperty("_BaseColor", properties));
            editor.TextureScaleOffsetProperty(FindProperty("_BaseMap", properties));
            EditorGUILayout.HelpBox("Texture luminance controls the palette ramp. Output always uses the four palette colors. Opaque URP material; realtime lights and shadows are supported.", MessageType.None);
            editor.EnableInstancingField();
        }

        private static void Section(string name)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
        }

        private void Draw(MaterialEditor editor, MaterialProperty[] properties, params string[] names)
        {
            foreach (string name in names)
            {
                MaterialProperty property = FindProperty(name, properties);
                editor.ShaderProperty(property, property.displayName);
            }
        }

        private static void PresetButton(MaterialEditor editor, string name, params string[] hexColors)
        {
            if (!GUILayout.Button(name)) return;
            Undo.RecordObjects(editor.targets, "Change retro palette");
            string[] propertyNames = { "_ShadowColor", "_DarkColor", "_LightColor", "_HighlightColor" };
            foreach (Object target in editor.targets)
            {
                Material material = (Material)target;
                for (int i = 0; i < propertyNames.Length; i++)
                {
                    if (ColorUtility.TryParseHtmlString("#" + hexColors[i], out Color color))
                        material.SetColor(propertyNames[i], color);
                }
                EditorUtility.SetDirty(material);
            }
            editor.PropertiesChanged();
        }
    }
}
