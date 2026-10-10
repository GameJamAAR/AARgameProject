using System;
using UnityEditor;
using UnityEngine;

namespace AAR.PCB.Editor
{
    [CustomEditor(typeof(PCBFloorGenerator))]
    public sealed class PCBFloorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Bake Surface replaces traces, pads and silkscreen with normal/material maps after generation; components and pins stay 3D. Resolution and Normal Strength control the bake. Disable Bake Surface to recover full geometry. Both routing modes remain procedural.", MessageType.Info);
            var floor=(PCBFloorGenerator)target;
            if(floor.LastSurfaceTrianglesRemoved>0) EditorGUILayout.LabelField("Surface triangles removed",floor.LastSurfaceTrianglesRemoved.ToString("N0"));
            using(new EditorGUI.DisabledScope(!floor.library))
            {
                if(GUILayout.Button("Generate PCB Floor")) Generate(floor,false);
                if(GUILayout.Button("New Seed & Generate")) Generate(floor,true);
            }
            if(GUILayout.Button("Clear Generated Floor"))
            { Undo.RegisterFullObjectHierarchyUndo(floor.gameObject,"Clear PCB Floor");floor.Clear();EditorUtility.SetDirty(floor); }
        }
        static void Generate(PCBFloorGenerator floor,bool newSeed)
        {
            Undo.RegisterFullObjectHierarchyUndo(floor.gameObject,"Generate PCB Floor");
            if(newSeed) floor.seed=Guid.NewGuid().GetHashCode();
            try { floor.Generate(); EditorUtility.SetDirty(floor); }
            catch(Exception e) { Debug.LogException(e,floor); }
        }
    }

    [CustomEditor(typeof(PCBTileLibrary))]
    public sealed class PCBTileLibraryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("N=+Z, E=+X, S=-Z, W=-X. Every port must use the same centered width and height. Weights choose visual variants; rotations are automatic. Custom tiles can reuse the same four child groups for material and relief overrides.",MessageType.Info);
            if(AssetDatabase.GetAssetPath(target)==PCBStarterKit.LibraryPath)
            {
                EditorGUILayout.HelpBox("Rebuild updates every starter variant, including imported components, and replaces the library entry list. Materials retain your colors. Generate afterward to refresh the floor and its baked maps.",MessageType.None);
                if(GUILayout.Button("Rebuild Starter Geometry")) PCBStarterKit.Build();
            }
        }
    }
}
