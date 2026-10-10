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
            var floor=(PCBFloorGenerator)target;
            serializedObject.Update();
            bool global=floor.routingStyle==PCBFloorGenerator.RoutingStyle.GlobalBoard;
            var legacy=new System.Collections.Generic.HashSet<string>{"connectEveryTile","socketVariation","junctionVariation","columns","rows","tileSize","traceCoverage","loopChance","straightBias","componentHeight","globalBusSpacing","globalBusLanes"};
            var property=serializedObject.GetIterator();
            bool children=true;
            while(property.NextVisible(children))
            {
                children=false;
                if(global && legacy.Contains(property.name))continue;
                if(!global && property.name.StartsWith("global",StringComparison.Ordinal))continue;
                using(new EditorGUI.DisabledScope(property.name=="m_Script")) EditorGUILayout.PropertyField(property,true);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(global
                ? "Global Board places component groups and routes their contacts across the entire board. Chunk Size only controls baked surface blocks; it does not change the composition. Group recipe weights and imported models come from the library, without rebuilding the tile kit."
                : "Connect Every Tile guarantees one connected board. Shared Sockets joins neighboring tile ports. Rebuild the starter kit after changing its local recipes.",MessageType.Info);
            EditorGUILayout.HelpBox("Bake Surface replaces shallow copper and pads with normal/material maps. Components remain 3D. Resolution is per render chunk. Disable Bake Surface to inspect the original copper geometry.",MessageType.Info);
            if(floor.LastGlobalPlan!=null) EditorGUILayout.LabelField("Global composition",$"{floor.LastGlobalPlan.parts.Count} components / {floor.LastGlobalPlan.links.Count} connections");
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
