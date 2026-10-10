using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AAR.RetroScreen.Editor
{
    public static class RetroScreenSetup
    {
        public const string MaterialPath = "Assets/RetroScreen/RetroScreen.mat";

        [MenuItem("Tools/AAR/Retro Screen/Install on project renderers")]
        public static void Install()
        {
            Shader shader = Shader.Find("AAR/Retro Screen");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new System.InvalidOperationException("Retro Screen shader must import successfully before setup.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "RetroScreen" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            InstallOnRenderer("Assets/Settings/PC_Renderer.asset", material);
            InstallOnRenderer("Assets/Settings/Mobile_Renderer.asset", material);
            if (Camera.main)
                InstallDitherGrid(Camera.main, AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = material;
        }

        public static void InstallDitherGrid(Camera camera, ScriptableRendererData renderer)
        {
            if (!camera || !renderer) return;
            foreach (var feature in renderer.rendererFeatures)
                if (feature is FullScreenPassRendererFeature full && full.name == "Retro Screen")
                {
                    var grid = camera.GetComponent<RetroScreenDitherGrid>();
                    if (!grid) grid = Undo.AddComponent<RetroScreenDitherGrid>(camera.gameObject);
                    Undo.RecordObject(grid, "Configure final dither grid");
                    grid.screenFeature = full;
                    EditorUtility.SetDirty(grid);
                    break;
                }
        }

        // Can also be used by other authoring tools to install on any URP renderer asset.
        public static void InstallOnRenderer(string rendererPath, Material material)
        {
            ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (renderer == null) throw new System.ArgumentException("URP renderer asset missing: " + rendererPath);
            FullScreenPassRendererFeature feature = null;
            foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
            {
                if (existing is FullScreenPassRendererFeature fullScreen && fullScreen.name == "Retro Screen")
                {
                    feature = fullScreen;
                    break;
                }
            }
            Undo.RecordObject(renderer, "Install Retro Screen");
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "Retro Screen";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                Undo.RegisterCreatedObjectUndo(feature, "Create Retro Screen feature");
                renderer.rendererFeatures.Add(feature);
            }
            else Undo.RecordObject(feature, "Configure Retro Screen");
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(true);
            feature.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }
    }
}
