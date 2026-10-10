using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCB
{
    [DisallowMultipleComponent]
    public sealed class PCBFloorGenerator : MonoBehaviour
    {
        [Header("Tile kit")]
        public PCBTileLibrary library;
        public enum RoutingStyle { MatchedTiles, SharedSockets }
        [Header("Layout")]
        public RoutingStyle routingStyle = RoutingStyle.MatchedTiles;
        [Range(0, .25f)] public float socketVariation = .20f;
        [Range(0, .12f)] public float junctionVariation = .10f;
        [SerializeField, HideInInspector] Mesh macroMesh;
        [Range(1, 64)] public int columns = 10;
        [Range(1, 64)] public int rows = 12;
        public int seed = 1984;
        [Min(0.1f)] public float tileSize = 1.8f;
        [Range(0, 1)] public float traceCoverage = 0.85f;
        [Range(0, 1)] public float loopChance = 0.2f;
        [Range(0, 1)] public float straightBias = 0.6f;
        [Header("3D relief")]
        [Min(0.01f)] public float boardThickness = 0.08f;
        [Range(0.1f, 12)] public float traceRelief = 1;
        [Range(0, 1)] public float componentDensity = 0.4f;
        [Range(0.1f, 20)] public float componentHeight = 1;
        [Header("Shared material overrides (optional)")]
        public Material boardMaterial;
        public Material traceMaterial;
        public Material componentMaterial;
        public Material pinMaterial;
        public Material legendMaterial;
        [Header("Baked surface (components stay 3D)")]
        public bool bakeSurface = true;
        [Tooltip("Pixels per tile, rounded to a power of two. 512 is recommended for fine traces.")]
        [Range(128,1024)] public int surfaceResolution = 512;
        [Range(0,3)] public float normalStrength = 1;
        public int LastSurfaceTrianglesRemoved { get; private set; }
        [Header("Generation")]
        public bool generateOnStart;
        public bool addFloorCollider = true;
        [SerializeField, HideInInspector] Transform generatedRoot;
        public int[] LastLayout { get; private set; }

        void Start() { if (generateOnStart) Generate(); }

        public void Generate()
        {
            if (!library) throw new InvalidOperationException("Assign a PCB tile library first.");
            if(bakeSurface)
            {
                var copper = traceMaterial ? traceMaterial : library.copperMaterial;
                if(!copper || copper.shader.name != "AAR/Retro Dither")
                    throw new InvalidOperationException("Baked PCB surfaces require an AAR/Retro Dither trace material. Disable Bake Surface to use a different shader.");
            }
            LastSurfaceTrianglesRemoved = 0;
            int w = Mathf.Clamp(columns, 1, 64), h = Mathf.Clamp(rows, 1, 64);
            bool macro = routingStyle == RoutingStyle.SharedSockets;
            var masks = PCBLayout.Generate(w, h, seed, traceCoverage, loopChance, straightBias);
            PCBLayout.Validate(masks, w, h);
            var random = new System.Random(seed ^ 0x504342);
            // Validate the whole library before replacing the existing floor.
            var choices = new PCBTileLibrary.Entry[masks.Length];
            var rotations = new int[masks.Length];
            for (int i = 0; i < masks.Length; i++)
            {
                if (!library.TryPick(macro ? 0 : masks[i], random, out choices[i], out rotations[i]))
                    throw new InvalidOperationException($"Tile library has no match for sockets {masks[i]}.");
                if(macro && !choices[i].prefab.transform.Find("Network Port"))
                    throw new InvalidOperationException("Shared Socket tiles need an authored Network Port child.");
            }
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Generate PCB Floor");
#endif
            Clear();
            generatedRoot = new GameObject("Generated PCB Tiles").transform;
            generatedRoot.SetParent(transform, false);
            float size = Mathf.Max(0.1f, tileSize);
            var feeds = new List<Vector2[]>();
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    int i = z * w + x;
                    GameObject tile;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        tile = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(choices[i].prefab, generatedRoot);
                    else
#endif
                        tile = Instantiate(choices[i].prefab, generatedRoot);
                    tile.name = $"PCB [{x},{z}] {choices[i].prefab.name}";
                    tile.transform.localPosition = new Vector3((x - (w - 1) * 0.5f) * size, 0, (z - (h - 1) * 0.5f) * size);
                    tile.transform.localRotation = Quaternion.Euler(0, rotations[i] * 90, 0);
                    tile.transform.localScale = new Vector3(size, 1, size);
                    ApplyGroup(tile.transform, "Surface", boardMaterial, Mathf.Max(.01f, boardThickness) / .05f, true);
                    ApplyGroup(tile.transform, "Traces", traceMaterial, traceRelief, true);
                    bool module = true;
                    bool components = module && random.NextDouble() < componentDensity;
                    ApplyGroup(tile.transform, "Components", componentMaterial, componentHeight, components);
                    ApplyGroup(tile.transform, "Pins", pinMaterial, componentHeight, components);
                    ApplyGroup(tile.transform, "Pads", pinMaterial, traceRelief, module);
                    ApplyGroup(tile.transform, "Markings", legendMaterial, 1, module);
                    if (macro)
                    {
                        var port=tile.transform.Find("Network Port");
                        if(port && masks[i]!=0)
                        {
                            var p=generatedRoot.InverseTransformPoint(port.position);
                            var center=new Vector2(tile.transform.localPosition.x,tile.transform.localPosition.z);
                            // Match the same junction definition used by the routed tile.
                            var junctionRandom=new System.Random(seed ^ (i*73856093));
                            var junction=center+new Vector2((float)junctionRandom.NextDouble()*2-1,(float)junctionRandom.NextDouble()*2-1)*size*junctionVariation;
                            feeds.Add(new[]{new Vector2(p.x,p.z),junction});
                        }
                    }
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(tile.transform);
#endif
                }
            if (macro) CreateMacroRouting(masks, w, h, feeds, size);
            if(bakeSurface) BakeGeneratedSurfaces(macro);
            if (addFloorCollider)
            {
                var collider = generatedRoot.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, -boardThickness * .5f, 0);
                collider.size = new Vector3(w * size, boardThickness, h * size);
            }
            LastLayout = masks;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(generatedRoot.gameObject, "Generate PCB Floor");
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        void BakeGeneratedSurfaces(bool macro)
        {
            LastSurfaceTrianglesRemoved = 0;
            var cache = new Dictionary<string,PCBSurfaceBake.Result>();
            PCBBakeResources resources = Application.isPlaying ? generatedRoot.gameObject.AddComponent<PCBBakeResources>() : null;
            var routing = generatedRoot.Find("Continuous PCB Network");
            int index = 0;
            // Finish routing first so both procedural modes bake exactly their generated paths.
            var tiles = new List<Transform>();
            foreach(Transform child in generatedRoot) if(child.Find("Surface")) tiles.Add(child);
            foreach(var tile in tiles)
            {
                string key = macro ? "Tile "+index : tile.name.Substring(tile.name.IndexOf("] ",StringComparison.Ordinal)+2);
                if(!cache.TryGetValue(key,out var result))
                {
                    result = PCBSurfaceBake.Bake(tile,this,key,routing); cache.Add(key,result);
                    if(resources){resources.owned.Add(result.material);resources.owned.Add(result.normals);resources.owned.Add(result.mask);}
                }
                foreach(string name in new[]{"Traces","Pads","Markings"})
                {
                    var group=tile.Find(name);if(!group)continue;
                    foreach(var filter in group.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh)LastSurfaceTrianglesRemoved+=filter.sharedMesh.triangles.Length/3;
                }
                PCBSurfaceBake.Apply(tile,result); index++;
            }
            if(routing)
            {
                foreach(var filter in routing.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh)LastSurfaceTrianglesRemoved+=filter.sharedMesh.triangles.Length/3;
                if(Application.isPlaying){routing.gameObject.SetActive(false);Destroy(routing.gameObject);}else DestroyImmediate(routing.gameObject);
            }
        }

        void CreateMacroRouting(int[] masks,int width,int depth,List<Vector2[]> feeds,float size)
        {
            var routes=PCBMacroRouting.SharedRoutes(masks,width,depth,size,seed,socketVariation,junctionVariation);
            var mesh=PCBMacroRouting.Build(routes,Mathf.Clamp(library.traceWidth,.012f,.035f)*size,
                library.busSpacing*size,.012f*traceRelief);
            var fanouts=PCBMacroRouting.Build(feeds,Mathf.Clamp(library.traceWidth,.012f,.035f)*size,0,.012f*traceRelief,1);
            var combined=new Mesh {name="PCB Shared Socket Routing",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            combined.CombineMeshes(new[]{new CombineInstance{mesh=mesh,transform=Matrix4x4.identity},new CombineInstance{mesh=fanouts,transform=Matrix4x4.identity}});
            DestroyImmediate(mesh);DestroyImmediate(fanouts);mesh=combined;
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                if(!macroMesh)
                {
                    System.IO.Directory.CreateDirectory("Assets/PCBFloor/GeneratedRouting");
                    UnityEditor.AssetDatabase.Refresh();
                    string path=UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/PCBFloor/GeneratedRouting/PCB Network.asset");
                    UnityEditor.AssetDatabase.CreateAsset(mesh,path);
                    macroMesh=mesh;
                }
                else
                {
                    UnityEditor.Undo.RegisterCompleteObjectUndo(macroMesh,"Update PCB routing");
                    macroMesh.Clear();macroMesh.indexFormat=mesh.indexFormat;
                    macroMesh.vertices=mesh.vertices;macroMesh.uv=mesh.uv;macroMesh.triangles=mesh.triangles;
                    macroMesh.normals=mesh.normals;macroMesh.bounds=mesh.bounds;
                    DestroyImmediate(mesh);UnityEditor.EditorUtility.SetDirty(macroMesh);
                }
                mesh=macroMesh;
            }
#endif
            var routing=new GameObject("Continuous PCB Network");routing.transform.SetParent(generatedRoot,false);
            routing.AddComponent<MeshFilter>().sharedMesh=mesh;
            routing.AddComponent<MeshRenderer>().sharedMaterial=traceMaterial ? traceMaterial : library.copperMaterial;
            if(Application.isPlaying)routing.AddComponent<PCBTransientMesh>().mesh=mesh;
        }

        static void ApplyGroup(Transform tile, string name, Material material, float height, bool active)
        {
            var group = tile.Find(name);
            if (!group) return;
            group.localScale = new Vector3(1, Mathf.Max(.01f, height), 1);
            group.gameObject.SetActive(active);
            if (material) foreach (var r in group.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(group.gameObject);
                foreach (var r in group.GetComponentsInChildren<Renderer>(true)) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
#endif
        }

        public void Clear()
        {
            if (!generatedRoot) return;
            if (Application.isPlaying)
            {
                generatedRoot.gameObject.SetActive(false);
                Destroy(generatedRoot.gameObject);
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(this, "Clear PCB Floor");
                UnityEditor.Undo.DestroyObjectImmediate(generatedRoot.gameObject);
#else
                DestroyImmediate(generatedRoot.gameObject);
#endif
            }
            generatedRoot = null;
        }
    }
}
