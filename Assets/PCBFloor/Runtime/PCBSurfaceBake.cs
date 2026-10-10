using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCB
{
    // A top-down height/material bake: source geometry remains in the kit, never in the render output.
    public static class PCBSurfaceBake
    {
        public sealed class Result
        {
            public Texture2D normals, mask;
            public Material material;
        }
        public static Result Bake(Transform tile, PCBFloorGenerator floor, string key, Transform extraRouting = null)
        {
            int resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(floor.surfaceResolution,128,1024));
            var height = new float[resolution*resolution];
            var ids = new byte[height.Length];
            // Higher layers win, including silkscreen projected from package tops.
            Raster(tile,"Traces",1,resolution,height,ids);
            if(extraRouting) RasterGroup(tile,extraRouting,1,resolution,height,ids);
            Raster(tile,"Pads",2,resolution,height,ids);
            Raster(tile,"Markings",3,resolution,height,ids);
            var pixels = new Color32[height.Length]; var masks = new Color32[height.Length];
            float step = Mathf.Max(.1f,floor.tileSize)/resolution;
            for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
            {
                int i=y*resolution+x;
                float dx=(height[y*resolution+Mathf.Min(x+1,resolution-1)]-height[y*resolution+Mathf.Max(x-1,0)])/(2*step);
                float dz=(height[Mathf.Min(y+1,resolution-1)*resolution+x]-height[Mathf.Max(y-1,0)*resolution+x])/(2*step);
                var normal=new Vector3(-dx*floor.normalStrength,-dz*floor.normalStrength,1).normalized;
                pixels[i]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);
                masks[i]=new Color32(ids[i]==1?(byte)255:(byte)0,ids[i]==2?(byte)255:(byte)0,ids[i]==3?(byte)255:(byte)0,255);
            }
            var result=new Result { normals=Texture(key+" Normals",resolution,pixels),mask=Texture(key+" Mask",resolution,masks)};
            var copper=floor.traceMaterial ? floor.traceMaterial : floor.library.copperMaterial;
            var board=floor.boardMaterial ? floor.boardMaterial : floor.library.surfaceMaterial;
            var contact=floor.pinMaterial ? floor.pinMaterial : floor.library.contactMaterial;
            var legend=floor.legendMaterial ? floor.legendMaterial : floor.library.legendMaterial;
            result.material=new Material(copper){name=key+" Baked Surface"};
            result.material.SetFloat("_PCBBaked",1);
            result.material.SetFloat("_ShadowOnly",0);
            result.material.SetTexture("_PCBNormalMap",result.normals); result.material.SetTexture("_PCBMaskMap",result.mask);
            if(board)
            {
                result.material.SetColor("_GroundColor",board.GetColor("_GroundColor"));
                result.material.SetColor("_GroundShadowColor",board.GetColor("_GroundShadowColor"));
                result.material.SetFloat("_ShadowOpacity",board.GetFloat("_ShadowOpacity"));
            }
            result.material.SetFloat("_PCBContactBrightness",contact?contact.GetFloat("_Brightness"):copper.GetFloat("_Brightness"));
            result.material.SetFloat("_PCBContactSpecular",contact?contact.GetFloat("_Specular"):copper.GetFloat("_Specular"));
            result.material.SetColor("_PCBLegendColor",legend?legend.GetColor("_GroundColor"):Color.gray);
#if UNITY_EDITOR
            if(!Application.isPlaying) Persist(result,floor,key);
#endif
            if(Application.isPlaying){result.normals.Apply(false,true);result.mask.Apply(false,true);}
            return result;
        }
        static Texture2D Texture(string name,int size,Color32[] data)
        {
            // Raw RGB normals, not Unity's platform-dependent imported normal encoding.
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
            texture.SetPixels32(data);texture.Apply(true,false);return texture;
        }
        static void Raster(Transform tile,string groupName,byte id,int size,float[] height,byte[] ids)
        {
            var group=tile.Find(groupName);if(!group||!group.gameObject.activeSelf)return;
            RasterGroup(tile,group,id,size,height,ids);
        }
        static void RasterGroup(Transform tile,Transform group,byte id,int size,float[] height,byte[] ids)
        {
            foreach(var filter in group.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;if(!mesh)continue;
                var vertices=mesh.vertices;var triangles=mesh.triangles;
                var matrix=tile.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                    float area=(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
                    if(Mathf.Abs(area)<1e-10f)continue;
                    int minX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,Mathf.Min(b.x,c.x))+.5f)*size),0,size-1);
                    int maxX=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,Mathf.Max(b.x,c.x))+.5f)*size),0,size-1);
                    int minY=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,Mathf.Min(b.z,c.z))+.5f)*size),0,size-1);
                    int maxY=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,Mathf.Max(b.z,c.z))+.5f)*size),0,size-1);
                    for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                    {
                        float px=(x+.5f)/size-.5f,pz=(y+.5f)/size-.5f;
                        float u=((px-a.x)*(c.z-a.z)-(pz-a.z)*(c.x-a.x))/area;
                        float v=((b.x-a.x)*(pz-a.z)-(b.z-a.z)*(px-a.x))/area;
                        if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                        float h=a.y+u*(b.y-a.y)+v*(c.y-a.y);int index=y*size+x;
                        if(h>=height[index]-.00001f){height[index]=h;ids[index]=id;}
                    }
                }
            }
        }
        public static void Apply(Transform tile,Result result)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(tile.gameObject))
                UnityEditor.PrefabUtility.UnpackPrefabInstance(tile.gameObject,UnityEditor.PrefabUnpackMode.Completely,UnityEditor.InteractionMode.AutomatedAction);
#endif
            foreach(string name in new[]{"Traces","Pads","Markings"})
            {
                var group=tile.Find(name);if(!group)continue;
                // Remove the generated copies, preserving the authored prefab library.
                if(Application.isPlaying){group.gameObject.SetActive(false);UnityEngine.Object.Destroy(group.gameObject);}
                else UnityEngine.Object.DestroyImmediate(group.gameObject);
            }
            foreach(var renderer in tile.Find("Surface").GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=result.material;
        }
#if UNITY_EDITOR
        static void Persist(Result result,PCBFloorGenerator floor,string key)
        {
            string identity=UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(floor).ToString();
            string root="Assets/PCBFloor/Baked/"+Hash128.Compute(identity).ToString().Substring(0,12);
            System.IO.Directory.CreateDirectory(root);UnityEditor.AssetDatabase.Refresh();
            result.normals=SaveTexture(result.normals,root+"/"+key+" Normals.asset");
            result.mask=SaveTexture(result.mask,root+"/"+key+" Mask.asset");
            result.material.SetTexture("_PCBNormalMap",result.normals);result.material.SetTexture("_PCBMaskMap",result.mask);
            string path=root+"/"+key+" Surface.mat";var existing=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing){UnityEditor.Undo.RecordObject(existing,"Update PCB surface");existing.CopyPropertiesFromMaterial(result.material);UnityEngine.Object.DestroyImmediate(result.material);result.material=existing;UnityEditor.EditorUtility.SetDirty(existing);}
            else UnityEditor.AssetDatabase.CreateAsset(result.material,path);
        }
        static Texture2D SaveTexture(Texture2D texture,string path)
        {
            var existing=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!existing){UnityEditor.AssetDatabase.CreateAsset(texture,path);return texture;}
            UnityEditor.Undo.RegisterCompleteObjectUndo(existing,"Update PCB bake");
            existing.Reinitialize(texture.width,texture.height,TextureFormat.RGBA32,true);
            existing.SetPixels32(texture.GetPixels32());existing.Apply(true,false);
            existing.wrapMode=TextureWrapMode.Clamp;existing.filterMode=FilterMode.Trilinear;existing.anisoLevel=4;
            UnityEditor.EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(texture);return existing;
        }
#endif
    }
}
