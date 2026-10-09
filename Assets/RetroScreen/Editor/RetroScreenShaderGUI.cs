using UnityEditor;
using UnityEngine;

namespace AAR.RetroScreen.Editor
{
    public sealed class RetroScreenShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.LabelField("RETRO / SCREEN", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("CRT curvature applied to the whole rendered image. Effect Blend = 0 bypasses the look. For a pristine four-color palette, disable vignette, scanlines, and color separation and keep crisp sampling enabled.", MessageType.None);
            Draw(editor, properties, "_Intensity");
            Section("Screen shape");
            Draw(editor, properties, "_CurvatureX", "_CurvatureY", "_Zoom", "_CornerRadius", "_BorderWidth", "_EdgeFeather", "_BorderColor");
            Section("CRT finish (optional)");
            Draw(editor, properties, "_VignetteStrength", "_VignetteSoftness", "_ScanlineStrength", "_ScanlineCount", "_ScanlineWidth", "_ChromaticAberration", "_PointSampling");
            EditorGUILayout.HelpBox("Scanlines bend with the picture. Line count is capped at the current resolution so lines remain distinct instead of disappearing into a flat tint.", MessageType.None);
            EditorGUILayout.HelpBox("Parameters belong to this shared material. The Full Screen Pass feature is installed on the project's PC and Mobile renderers. Disable its checkbox to skip the pass entirely.", MessageType.None);
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Draw(MaterialEditor editor, MaterialProperty[] properties, params string[] names)
        {
            foreach (string name in names)
            {
                MaterialProperty property = FindProperty(name, properties);
                editor.ShaderProperty(property, property.displayName);
            }
        }
    }
}
