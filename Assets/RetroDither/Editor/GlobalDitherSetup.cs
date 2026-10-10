using UnityEditor;
using UnityEngine;
using TMPro;
using AAR.RetroUI;

namespace AAR.RetroDither.Editor
{
    public static class GlobalDitherSetup
    {
        public const string ProfilePath="Assets/Resources/RetroDither/Global Settings.asset";
        const string ThemePath="Assets/Resources/RetroUI/Windows 98 Theme.asset";
        [MenuItem("Tools/AAR/Shared Dither Settings")]
        public static void Open(){Install();Selection.activeObject=AssetDatabase.LoadAssetAtPath<GlobalDitherProfile>(ProfilePath);}

        public static void Install()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Install shared settings outside Play mode.");
            Folder("Assets","Resources");Folder("Assets/Resources","RetroDither");Folder("Assets/Resources","RetroUI");
            var profile=AssetDatabase.LoadAssetAtPath<GlobalDitherProfile>(ProfilePath);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<GlobalDitherProfile>();
                // Seed common settings from the current PCB body look; keep palettes local.
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/PCBFloor/StarterKit/Materials/Chip Bodies.mat");
                if(material)
                {
                    profile.pixelSize=Mathf.RoundToInt(material.GetFloat("_PixelSize"));profile.strength=material.GetFloat("_DitherStrength");
                    profile.matrix=(GlobalDitherProfile.Matrix)Mathf.RoundToInt(material.GetFloat("_PatternSize"));
                    profile.sizing=(GlobalDitherProfile.Sizing)Mathf.RoundToInt(material.GetFloat("_PatternScaleMode"));
                    profile.referenceHeight=Mathf.RoundToInt(material.GetFloat("_ReferenceHeight"));
                    profile.detailStability=material.GetFloat("_DetailStability");profile.cellLighting=material.GetFloat("_CellLighting");
                    profile.preserveDither=material.GetFloat("_PreserveDither")>.5f;profile.specularAA=material.GetFloat("_SpecularAA");
                }
                AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            var theme=AssetDatabase.LoadAssetAtPath<Win98Theme>(ThemePath);
            if(!theme){theme=ScriptableObject.CreateInstance<Win98Theme>();AssetDatabase.CreateAsset(theme,ThemePath);}
            if(!theme.font)theme.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RetroUI/Fonts/Tiny5 Bitmap.asset");
            if(theme.font)
            {
                theme.font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
                theme.font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~àâäéèêëîïôöùûüçœÉÀÈÊÎÔÙÇ°—");
                theme.font.atlasPopulationMode=AtlasPopulationMode.Static;
                foreach(var atlas in theme.font.atlasTextures)if(atlas){atlas.filterMode=FilterMode.Point;EditorUtility.SetDirty(atlas);}
                EditorUtility.SetDirty(theme.font);
            }
            EditorUtility.SetDirty(theme);
            var controller=Object.FindAnyObjectByType<GlobalDitherController>();
            if(!controller)
            {
                var go=new GameObject("Shared Dither Settings");Undo.RegisterCreatedObjectUndo(go,"Install shared dither settings");controller=go.AddComponent<GlobalDitherController>();
            }
            Undo.RecordObject(controller,"Assign shared dither profile");controller.profile=profile;controller.Apply();EditorUtility.SetDirty(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        static void Folder(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
    }

    [CustomEditor(typeof(GlobalDitherProfile))]
    public sealed class GlobalDitherProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("One profile controls all AAR/Retro Dither materials, including generated and baked PCB surfaces. Colors, lighting, maps, UV coordinates and shadow-only behavior remain local. A material's Keep Local Controls field creates exceptions.",MessageType.Info);
            DrawDefaultInspector();
            if(GlobalDitherController.Instance)GlobalDitherController.Instance.Apply();
        }
    }
}
