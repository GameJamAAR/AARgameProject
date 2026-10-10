using UnityEditor;
using UnityEngine;

namespace AAR.RetroDither.Editor
{
    /// <summary>Material-specific controls and explicit exceptions to the shared profile.</summary>
    public sealed class RetroDitherShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.LabelField("RETRO / DITHER", EditorStyles.boldLabel);
            var overrides=FindProperty("_DitherOverrides",properties);
            EditorGUI.showMixedValue=overrides.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int mask=EditorGUILayout.MaskField("Keep local controls",(int)overrides.floatValue,new[]{"Pixel size / sizing","Strength","Bayer matrix","Filtering"});
            if(EditorGUI.EndChangeCheck()){editor.RegisterPropertyChangeUndo("Local dither overrides");overrides.floatValue=mask&15;}
            EditorGUI.showMixedValue=false;
            EditorGUILayout.HelpBox("Shared Dither Settings controls the pattern across the scene. Checked groups keep this material's own values. Palette, lighting, UV coordinates, PCB maps and shadow behavior always stay local.",MessageType.None);
            if(GUILayout.Button("Open shared dither settings"))Selection.activeObject=UnityEngine.Resources.Load<GlobalDitherProfile>("RetroDither/Global Settings");
            Draw(editor, properties, "_ShadowOnly");
            if (FindProperty("_ShadowOnly", properties).floatValue > 0.5f)
            {
                EditorGUILayout.HelpBox("The lit ground stays flat and clean. Only realtime shadows from the main light receive dithering; object lighting is unaffected.", MessageType.None);
                Section("Ground & shadows");
                Draw(editor, properties, "_GroundColor", "_GroundShadowColor", "_ShadowOpacity");
                Section("Shadow pattern");
                Draw(editor, properties, "_DitherStrength", "_PixelSize", "_PatternScaleMode", "_PatternSize", "_PatternSpace", "_PatternOffset", "_ReferenceHeight", "_UVDensity");
                editor.EnableInstancingField();
                return;
            }
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
            Draw(editor, properties, "_NormalizePalette");
            EditorGUILayout.HelpBox("Palette normalization orders colors by brightness and merges duplicates. Two unique colors produce a true two-color ramp instead of repeated light/dark inversions. Disable for intentionally ordered palettes.", MessageType.None);

            Section("Pattern");
            Draw(editor, properties, "_DitherStrength", "_PixelSize", "_PatternScaleMode", "_PatternSize", "_PatternSpace", "_PatternOffset", "_UVDensity");
            EditorGUILayout.HelpBox("Screen coordinates give a stable display grid. Mesh UV coordinates anchor the pattern to the surface. Pixel Size scales the dither pattern; it does not pixelate geometry.", MessageType.None);

            Section("Detail & distance quality");
            Draw(editor, properties, "_PreserveDither", "_CellLighting", "_DetailStability", "_SpecularAA", "_ReferenceHeight", "_DistanceFade", "_FadeStart", "_FadeEnd");
            EditorGUILayout.HelpBox("Preserve Crisp Dither keeps Screen mode strictly on palette colors and retains the dots at distance. Cell Lighting Stability stabilizes lighting inside each cell, with protection at sharp edges. Distance Smoothing increases this stabilization. Disable Preserve for the softer blended treatment. Subpixel UV patterns still require filtering.", MessageType.None);

            Section("Tone & lighting");
            Draw(editor, properties, "_Contrast", "_Brightness", "_Exposure", "_Ambient", "_LightWrap", "_Specular", "_Shininess", "_RimStrength", "_RimPower");

            Section("Surface input");
            editor.TexturePropertySingleLine(new GUIContent("Tone texture / tint"), FindProperty("_BaseMap", properties), FindProperty("_BaseColor", properties));
            editor.TextureScaleOffsetProperty(FindProperty("_BaseMap", properties));
            EditorGUILayout.HelpBox("Texture luminance controls the palette ramp. Resolved dithering uses the palette; filtered details blend neighboring colors. Opaque URP material; realtime lights and shadows are supported.", MessageType.None);
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
