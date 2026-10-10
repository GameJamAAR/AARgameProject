using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AAR.PCB.Editor
{
    public static class PCBValidation
    {
        [MenuItem("Tools/AAR/PCB/Validate System")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            ValidateCopperRouter();
            int layouts=0,seams=0;
            for(int seed=0;seed<100;seed++)
            {
                var masks=PCBLayout.Generate(12,9,seed,.9f,.1f,.4f);
                var routes=PCBMacroRouting.SharedRoutes(masks,12,9,1.8f,seed,.22f,.1f);
                var repeat=PCBMacroRouting.SharedRoutes(masks,12,9,1.8f,seed,.22f,.1f);
                var edgeProfiles=new System.Collections.Generic.Dictionary<string,int>();
                for(int i=0;i<routes.Count;i++)
                {
                    Require(routes[i].SequenceEqual(repeat[i]),"Shared routing seed repeatability.");
                    for(int j=1;j<routes[i].Length;j++)
                    {
                        var delta=routes[i][j]-routes[i][j-1];
                        Require(Mathf.Abs(delta.x)<.00001f||Mathf.Abs(delta.y)<.00001f||Mathf.Abs(Mathf.Abs(delta.x)-Mathf.Abs(delta.y))<.00001f,"Shared routes must be octilinear.");
                    }
                    foreach(int endpoint in new[]{0,routes[i].Length-1})
                    {
                        var p=routes[i][endpoint];
                        bool boundary=Mathf.Abs((p.x/1.8f+6)-Mathf.Round(p.x/1.8f+6))<.00001f
                            ||Mathf.Abs((p.y/1.8f+4.5f)-Mathf.Round(p.y/1.8f+4.5f))<.00001f;
                        if(!boundary)continue;
                        var delta=(endpoint==0 ? routes[i][1]-p : p-routes[i][endpoint-1]).normalized;
                        var normal=new Vector2(-delta.y,delta.x);
                        for(int lane=-1;lane<=1;lane++)
                        {
                            var rail=p+normal*(lane*.055f*1.8f);
                            string key=$"{Mathf.RoundToInt(rail.x*10000)}:{Mathf.RoundToInt(rail.y*10000)}";
                            edgeProfiles.TryGetValue(key,out int count);edgeProfiles[key]=count+1;
                        }
                    }
                }
                Require(edgeProfiles.Count>0&&edgeProfiles.Values.All(count=>count==2),"Every shared rail must coincide with exactly one neighbor.");
                var mesh=PCBMacroRouting.Build(routes,.035f,.099f,.016f);
                try {Require(mesh.vertexCount>0&&mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.z)),"Invalid routing geometry.");}
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            var dimensions=new[] {new Vector2Int(1,1),new Vector2Int(1,11),new Vector2Int(11,1),new Vector2Int(3,3),new Vector2Int(12,9)};
            foreach(var size in dimensions)
                for(int seed=0;seed<60;seed++)
                    foreach(float coverage in new[] {0f,.4f,1f})
                        foreach(float loops in new[] {0f,.5f,1f})
                        {
                            var a=PCBLayout.Generate(size.x,size.y,seed,coverage,loops,.6f);
                            var b=PCBLayout.Generate(size.x,size.y,seed,coverage,loops,.6f);
                            Require(a.SequenceEqual(b),"Seed must reproduce layout.");
                            PCBLayout.Validate(a,size.x,size.y);
                            layouts++;
                        }
            Require(!PCBLayout.Generate(10,10,1,1,.2f,.6f).SequenceEqual(PCBLayout.Generate(10,10,2,1,.2f,.6f)),"Seeds should vary the layout.");
            var library=AssetDatabase.LoadAssetAtPath<PCBTileLibrary>(PCBStarterKit.LibraryPath);
            Require(library!=null,"Build the starter kit first.");
            for(int mask=0;mask<16;mask++) Require(library.TryPick(mask,new System.Random(42),out _,out _),$"No tile for mask {mask}.");
            Vector4? common=null;
            string commonProfile=null;
            foreach(var entry in library.tiles)
                for(int rotation=0;rotation<4;rotation++)
                {
                    int mask=PCBLayout.Rotate(entry.sockets,rotation);
                    var group=entry.prefab.transform.Find("Traces");
                    Require(group!=null,"Missing trace group.");
                    var filter=group.GetComponent<MeshFilter>();
                    Require(filter!=null && filter.sharedMesh!=null,"Missing saved mesh asset.");
                    var q=Quaternion.Euler(0,90*rotation,0);
                    var vertices=filter.sharedMesh.vertices.Select(v=>q*group.localToWorldMatrix.MultiplyPoint3x4(v)).ToArray();
                    for(int d=0;d<4;d++)
                    {
                        var direction=PCBLayout.Directions[d];
                        var endpoint=vertices.Where(v=>Mathf.Abs(v.x*direction.x+v.z*direction.y-.5f)<.00001f).ToArray();
                        bool socket=(mask&(1<<d))!=0;
                        Require(socket==(endpoint.Length>0),$"Geometry/socket mismatch: {entry.prefab.name}, rotation {rotation}, edge {d}.");
                        if(!socket)continue;
                        // Shared bounds ensure coincident endpoint faces after any legal quarter turn.
                        var transverse=endpoint.Select(v=>direction.x==0?v.x:v.z).ToArray();
                        var bounds=new Vector4(transverse.Min(),transverse.Max(),endpoint.Min(v=>v.y),endpoint.Max(v=>v.y));
                        if(common.HasValue)Require((common.Value-bounds).sqrMagnitude<1e-9f,"Physical socket widths/heights differ.");
                        else common=bounds;
                        var profile=endpoint.Select(v=>new Vector2(direction.x==0?v.x:v.z,v.y))
                            .Select(v=>$"{Mathf.RoundToInt(v.x*100000)}:{Mathf.RoundToInt(v.y*100000)}").Distinct().OrderBy(v=>v).ToArray();
                        string signature=string.Join(";",profile);
                        if(commonProfile==null)commonProfile=signature;
                        else Require(commonProfile==signature,"Bus rail endpoints differ after rotation.");
                        Require(profile.Length==12,"Each socket must expose three separate four-corner rail faces.");
                        seams++;
                    }
                    Require(vertices.All(v=>Mathf.Abs(v.x)<=.50001f && Mathf.Abs(v.z)<=.50001f),"Trace geometry escapes tile footprint.");
                }
            Require(common.HasValue && common.Value.y-common.Value.x>.02f,"Socket must have a visible width.");
            foreach(var m in new[] {library.surfaceMaterial,library.copperMaterial,library.bodyMaterial,library.contactMaterial,library.legendMaterial})
            {
                Require(m && m.shader.name=="AAR/Retro Dither","PCB materials must use the dither shader.");
                Require(ShaderUtil.GetShaderMessages(m.shader).All(message=>message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"Shader compile error.");
            }
            return $"PCB PASS: 12 obstacle/clearance routing cases; 100 shared-socket layouts with coincident rail endpoints; {layouts} repeatable connected layouts, all 16 socket masks, {seams} rotated physical ports, five dither materials.";
        }
        static void ValidateCopperRouter()
        {
            for(int recipe=0;recipe<12;recipe++)
            {
                var start=new Vector2(-.40f,-.15f);var end=new Vector2(.40f,.15f);
                var center=new Vector2((recipe%3-1)*.04f,0);
                var router=new PCBTraceRouter(.006f);router.Keepout(center,new Vector2(.20f,.20f));
                var repeat=new PCBTraceRouter(.006f);repeat.Keepout(center,new Vector2(.20f,.20f));
                Require(router.TryRoute(start,end,out var a),"Copper route must avoid package keepout.");
                Require(repeat.TryRoute(start,end,out var b)&&a.SequenceEqual(b),"Copper route must be repeatable.");
                Require((a[0]-start).sqrMagnitude<1e-9f&&(a[a.Length-1]-end).sqrMagnitude<1e-9f,"Copper endpoints drifted.");
                for(int i=1;i<a.Length;i++)
                {
                    var delta=a[i]-a[i-1];
                    Require(Mathf.Abs(delta.x)<1e-5f||Mathf.Abs(delta.y)<1e-5f||Mathf.Abs(Mathf.Abs(delta.x)-Mathf.Abs(delta.y))<1e-5f,"Copper bends must be octilinear.");
                    int samples=Mathf.CeilToInt(delta.magnitude/.002f);
                    for(int k=0;k<=samples;k++)
                    {
                        var p=Vector2.Lerp(a[i-1],a[i],samples==0 ? 0 : k/(float)samples)-center;
                        Require(Mathf.Abs(p.x)>=.105f||Mathf.Abs(p.y)>=.105f,"Copper entered a component keepout.");
                    }
                }
                router.Reserve(a,.006f);
                Require(!router.TryRoute(start,end,out _),"Occupied copper must reject a duplicate track.");
                Require(!router.TryRoute(center,end,out _),"Cannot route from inside a package.");
            }
        }
        static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    }
}
