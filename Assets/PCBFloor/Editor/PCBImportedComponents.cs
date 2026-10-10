using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace AAR.PCB.Editor
{
    public static class PCBImportedComponents
    {
        public static void Build()
        {
            const string root="Assets/Imported/ComputerPackv2";
            var kit=AssetDatabase.LoadAssetAtPath<PCBTileLibrary>(PCBStarterKit.LibraryPath);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(root+"/ComputerPackv2.fbx");
            var instance=UnityEngine.Object.Instantiate(source);
            var capacitors=new List<GameObject>();var passives=new List<GameObject>();var modules=new List<GameObject>();
            try
            {
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var sourceMesh=filter.sharedMesh;
                    var mesh=UnityEngine.Object.Instantiate(sourceMesh);mesh.name=filter.name;
                    var matrix=filter.transform.localToWorldMatrix;
                    var vertices=mesh.vertices;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;
                    var bounds=new Bounds();
                    for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);if(i==0)bounds=new Bounds(vertices[i],Vector3.zero);else bounds.Encapsulate(vertices[i]);if(normals.Length==vertices.Length)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;}
                    var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    float width=Mathf.Max(bounds.size.x,bounds.size.z);
                    for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-origin)/Mathf.Max(width,.001f);
                    mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;mesh.RecalculateBounds();
                    string meshPath=root+"/"+filter.name+".asset";
                    var old=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(old){old.Clear();old.indexFormat=mesh.indexFormat;old.vertices=mesh.vertices;old.uv=mesh.uv;old.normals=mesh.normals;old.subMeshCount=mesh.subMeshCount;for(int sub=0;sub<mesh.subMeshCount;sub++)old.SetTriangles(mesh.GetTriangles(sub),sub);old.RecalculateBounds();UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,meshPath);
                    var prefab=new GameObject(filter.name);
                    try
                    {
                        prefab.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var renderer=prefab.AddComponent<MeshRenderer>();
                        var sourceMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                        var materials=new Material[mesh.subMeshCount];
                        for(int i=0;i<materials.Length;i++)
                        {
                            string name=i<sourceMaterials.Length&&sourceMaterials[i]?sourceMaterials[i].name.ToLowerInvariant():"";
                            bool metal=name.Contains("metal")||name.Contains("silver")||name.Contains("pin")||name.Contains("contact");
                            materials[i]=metal?kit.contactMaterial:kit.bodyMaterial;
                        }
                        renderer.sharedMaterials=materials;
                        var saved=PrefabUtility.SaveAsPrefabAsset(prefab,root+"/"+filter.name+".prefab");
                        if(filter.name.StartsWith("Capacitor"))capacitors.Add(saved);else if(filter.name=="Ram")modules.Add(saved);else passives.Add(saved);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(prefab);}
                }
                Undo.RecordObject(kit,"Integrate ComputerPackv2");kit.importedCapacitors=capacitors.ToArray();kit.importedPassives=passives.ToArray();kit.importedModules=modules.ToArray();EditorUtility.SetDirty(kit);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
        }
    }
}
