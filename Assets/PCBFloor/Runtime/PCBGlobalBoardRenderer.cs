using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AAR.PCB
{
    public static class PCBGlobalBoardRenderer
    {
        public static Transform Build(PCBFloorGenerator floor,PCBGlobalBoard.Plan plan,out int removed)
        {
            removed=0;var root=new GameObject("Generated PCB Tiles").transform;root.SetParent(floor.transform,false);
            var parts=new GameObject("Global Components").transform;parts.SetParent(root,false);
            Material body=floor.componentMaterial?floor.componentMaterial:floor.library.bodyMaterial;
            Material contacts=floor.pinMaterial?floor.pinMaterial:floor.library.contactMaterial;
            float mount=floor.bakeSurface?0:.014f*floor.traceRelief;
            var procedural=new List<MeshFilter>();
            void Primitive(Transform parent,PrimitiveType type,Vector3 position,Vector3 size,Material material)
            {procedural.Add(PCBGlobalBoardRenderer.Primitive(parent,type,position,size,material));}
            foreach(var part in plan.parts)
            {
                GameObject obj;
                if(part.prefab)
                {
                    obj=Object.Instantiate(part.prefab,parts);obj.name="Group "+part.group+" "+part.prefab.name;
                    float horizontal=part.sourceScale;
                    obj.transform.localScale=new Vector3(horizontal,part.height/part.sourceSize.y,horizontal);
                }
                else {obj=new GameObject("Group "+part.group+" Component");obj.transform.SetParent(parts,false);
                    var localSize=part.quarterTurn%2==0?part.size:new Vector2(part.size.y,part.size.x);
                    Primitive(obj.transform,part.family==1?PrimitiveType.Cylinder:PrimitiveType.Cube,Vector3.up*(part.height*.5f),new Vector3(localSize.x,part.family==1?part.height*.5f:part.height,localSize.y),body);
                    if(part.family==3)for(int end=-1;end<=1;end+=2)Primitive(obj.transform,PrimitiveType.Cube,new Vector3(0,part.height*.35f,end*localSize.y*.38f),new Vector3(localSize.x*1.08f,part.height*.7f,localSize.y*.24f),contacts);
                }
                obj.transform.localPosition=new Vector3(part.position.x,mount+(part.family==2?.10f:0),part.position.y);
                obj.transform.localRotation=Quaternion.Euler(0,part.quarterTurn*90,0);
                if(part.prefab)foreach(var r in obj.GetComponentsInChildren<MeshRenderer>()){var mats=r.sharedMaterials;for(int j=0;j<mats.Length;j++)mats[j]=body;r.sharedMaterials=mats;}
                if(part.family==2)Primitive(parts,PrimitiveType.Cube,new Vector3(part.position.x,.05f+mount,part.position.y),new Vector3(part.size.x,.10f,part.size.y),body);
                if(part.family==2)foreach(var port in part.ports){var pinSize=port.direction.x!=0?new Vector3(.12f,.08f,.10f):new Vector3(.10f,.08f,.12f);Primitive(parts,PrimitiveType.Cube,new Vector3(port.point.x,.04f+mount,port.point.y),pinSize,contacts);}
            }
            // Dense passive banks share two meshes instead of hundreds of cube draw calls.
            // Imported models remain separate and retain their native geometry.
            int batchIndex=0;
            foreach(var batch in procedural.GroupBy(filter=>filter.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(batch.Select(filter=>new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix}).ToArray(),true,true);
                Group(root,"Procedural Components "+batchIndex++,mesh,batch.Key,floor);
                foreach(var filter in batch){if(Application.isPlaying){filter.gameObject.SetActive(false);Object.Destroy(filter.gameObject);}else Object.DestroyImmediate(filter.gameObject);}
            }
            var paths=new List<Vector2[]>();var pads=new List<Vector2[]>();
            foreach(var link in plan.links)
            {
                if(link.layer==0)paths.Add(link.path);
                else {paths.Add(new[]{link.path[0],link.viaA});paths.Add(new[]{link.viaB,link.path[link.path.Length-1]});}
                foreach(int index in new[]{0,link.path.Length-1}){var p=link.path[index];pads.Add(new[]{p-new Vector2(.10f,0),p+new Vector2(.10f,0)});}
            }
            var copper=Group(root,"Global Copper",PCBMacroRouting.Build(paths,floor.globalTraceWidth,floor.globalBusSpacing,.012f*floor.traceRelief,1,false),floor.traceMaterial?floor.traceMaterial:floor.library.copperMaterial,floor);
            var padMesh=PCBMacroRouting.Build(pads,floor.globalTraceWidth*1.5f,0,.014f*floor.traceRelief,1);
            if(plan.vias.Count>0)
            {
                var rings=ViaMesh(plan.vias,floor.globalTraceWidth*1.1f,.014f*floor.traceRelief);
                var combined=new Mesh{indexFormat=IndexFormat.UInt32};combined.CombineMeshes(new[]{new CombineInstance{mesh=padMesh,transform=Matrix4x4.identity},new CombineInstance{mesh=rings,transform=Matrix4x4.identity}},true,true);
                if(Application.isPlaying){Object.Destroy(padMesh);Object.Destroy(rings);}else{Object.DestroyImmediate(padMesh);Object.DestroyImmediate(rings);}padMesh=combined;
            }
            var lands=Group(root,"Global Pads",padMesh,contacts,floor);
            if(!floor.bakeSurface)
            {
                foreach(var layer in plan.links.Where(link=>link.layer>0).GroupBy(link=>link.layer))
                {
                    var backPaths=new List<Vector2[]>();foreach(var link in layer){var path=new Vector2[link.path.Length-2];System.Array.Copy(link.path,1,path,0,path.Length);backPaths.Add(path);}
                    var back=Group(root,"Back Copper Layer "+layer.Key,PCBMacroRouting.Build(backPaths,floor.globalTraceWidth,0,.012f*floor.traceRelief,1),floor.traceMaterial?floor.traceMaterial:floor.library.copperMaterial,floor);
                    back.localPosition=Vector3.down*(floor.boardThickness+.012f*floor.traceRelief+.03f*(layer.Key-1));
                }
            }
            float chunk=Mathf.Max(1,floor.globalChunkSize);int cols=Mathf.CeilToInt(plan.size.x/chunk),rows=Mathf.CeilToInt(plan.size.y/chunk);
            var surfaceMesh=floor.library.tiles[0].prefab.transform.Find("Surface").GetComponent<MeshFilter>().sharedMesh;
            var resources=Application.isPlaying?root.gameObject.AddComponent<PCBBakeResources>():null;
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++)
            {
                float sx=Mathf.Min(chunk,plan.size.x-x*chunk),sz=Mathf.Min(chunk,plan.size.y-z*chunk);
                var tile=new GameObject("PCB Chunk ["+x+","+z+"]").transform;tile.SetParent(root,false);
                tile.localPosition=new Vector3(-plan.size.x*.5f+x*chunk+sx*.5f,0,-plan.size.y*.5f+z*chunk+sz*.5f);tile.localScale=new Vector3(sx,1,sz);
                var surface=new GameObject("Surface").transform;surface.SetParent(tile,false);surface.localScale=new Vector3(1,floor.boardThickness/.05f,1);
                surface.gameObject.AddComponent<MeshFilter>().sharedMesh=surfaceMesh;
                var renderer=surface.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=floor.boardMaterial?floor.boardMaterial:floor.library.surfaceMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(floor.bakeSurface)
                {
                    var baked=PCBSurfaceBake.Bake(tile,floor,"Global Chunk "+x+" "+z,copper,lands);renderer.sharedMaterial=baked.material;
                    if(resources){resources.owned.Add(baked.normals);resources.owned.Add(baked.mask);resources.owned.Add(baked.material);}
                }
            }
            if(floor.bakeSurface)
            {
                removed=copper.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3+lands.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
                if(Application.isPlaying){copper.gameObject.SetActive(false);lands.gameObject.SetActive(false);Object.Destroy(copper.gameObject);Object.Destroy(lands.gameObject);}
                else {Object.DestroyImmediate(copper.gameObject);Object.DestroyImmediate(lands.gameObject);}
            }
            if(floor.addFloorCollider){var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.down*floor.boardThickness*.5f;collider.size=new Vector3(plan.size.x,floor.boardThickness,plan.size.y);}
            return root;
        }
        static Mesh ViaMesh(List<Vector2> vias,float radius,float height)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();
            foreach(var center in vias)for(int i=0;i<16;i++)
            {
                int offset=vertices.Count;float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                foreach(var p in new[]{center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius*.43f,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*.43f}){vertices.Add(new Vector3(p.x,height,p.y));uv.Add(p);}
                indices.AddRange(new[]{offset,offset+2,offset+1,offset,offset+3,offset+2});
            }
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();return mesh;
        }
        static MeshFilter Primitive(Transform parent,PrimitiveType type,Vector3 position,Vector3 size,Material material)
        {var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<MeshRenderer>().sharedMaterial=material;var c=go.GetComponent<Collider>();if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);return go.GetComponent<MeshFilter>();}
        static Transform Group(Transform parent,string name,Mesh mesh,Material material,PCBFloorGenerator floor)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                string id=Hash128.Compute(UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(floor).ToString()).ToString().Substring(0,12);
                string folder="Assets/PCBFloor/GlobalGeometry/"+id;System.IO.Directory.CreateDirectory(folder);UnityEditor.AssetDatabase.Refresh();
                string path=folder+"/"+name+".asset";var existing=UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){existing.Clear();existing.indexFormat=IndexFormat.UInt32;existing.vertices=mesh.vertices;existing.uv=mesh.uv;existing.triangles=mesh.triangles;existing.normals=mesh.normals;existing.bounds=mesh.bounds;Object.DestroyImmediate(mesh);mesh=existing;UnityEditor.EditorUtility.SetDirty(mesh);}
                else UnityEditor.AssetDatabase.CreateAsset(mesh,path);
            }
#endif
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(Application.isPlaying)go.AddComponent<PCBTransientMesh>().mesh=mesh;return go.transform;
        }
    }
}
