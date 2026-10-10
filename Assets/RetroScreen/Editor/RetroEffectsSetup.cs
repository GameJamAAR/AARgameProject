using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AAR.RetroScreen.Editor
{
    public static class RetroEffectsSetup
    {
        [MenuItem("Tools/AAR/Retro Effects/Select controls")]
        public static void SelectControls()
        {
            var controls = Object.FindAnyObjectByType<RetroEffectsController>();
            if (!controls)
            {
                var go = new GameObject("Retro Effects — Level Design");
                Undo.RegisterCreatedObjectUndo(go, "Create retro effects controls");
                controls = Undo.AddComponent<RetroEffectsController>(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
            Selection.activeGameObject = controls.gameObject;
        }
        [MenuItem("Tools/AAR/Retro Effects/Toggle level design mode %&l")]
        public static void ToggleDesignMode()
        {
            SelectControls();
            var controls = Selection.activeGameObject.GetComponent<RetroEffectsController>();
            Undo.RecordObject(controls, "Toggle level design mode");
            controls.levelDesignMode = !controls.levelDesignMode;
            EditorUtility.SetDirty(controls);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(controls.gameObject.scene);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
