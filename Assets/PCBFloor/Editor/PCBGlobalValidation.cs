using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace AAR.PCB.Editor
{
    public static class PCBGlobalValidation
    {
        [MenuItem("Tools/AAR/PCB/Validate Global Board")]
        public static void RunMenu()=>Debug.Log(Run());
        public static string Run()
        {
            var floor=UnityEngine.Object.FindAnyObjectByType<PCBFloorGenerator>();
            if(!floor)throw new InvalidOperationException("Open a scene with a PCB floor first.");
            int original=floor.seed;float chunk=floor.globalChunkSize;var report=new List<string>();
            try
            {
                foreach(int seed in new[]{original,17,2718})
                {
                    floor.seed=seed;var watch=System.Diagnostics.Stopwatch.StartNew();var p=PCBGlobalBoard.Create(floor);
                    Validate(p,floor);
                    floor.globalChunkSize=chunk*.63f;var same=PCBGlobalBoard.Create(floor);floor.globalChunkSize=chunk;
                    Require(Fingerprint(p)==Fingerprint(same),"Bake chunk dimensions changed the circuit.");
                    report.Add($"{seed}: {p.parts.Count} parts, {p.links.Count} separate connections ({p.links.Count(l=>l.layer>0)} underside); graph, actual unique contacts, complete segment clearance on both layers, octilinear paths and chunk independence PASS ({watch.ElapsedMilliseconds} ms, two plans)");
                }
            }
            finally{floor.seed=original;floor.globalChunkSize=chunk;}
            return string.Join("\n",report);
        }
        public static void Validate(PCBGlobalBoard.Plan p,PCBFloorGenerator f)
        {
            int count=p.parts.Count;
            if(count==0){Require(p.links.Count==0,"Empty recipe produced connections.");return;}
            Require(p.links.Count>=count-1,"Incomplete circuit graph.");
            var seen=new HashSet<int>{0};for(int pass=0;pass<count;pass++)foreach(var link in p.links){if(seen.Contains(link.a))seen.Add(link.b);if(seen.Contains(link.b))seen.Add(link.a);}
            Require(seen.Count==count,"Disconnected component.");
            Require(p.gapFillParts==p.parts.Count(part=>part.gapFiller),"Gap filler count differs from the generated population.");
            foreach(var part in p.parts)if(part.gapFiller)
            {
                Require(part.family>=3&&!part.prefab&&part.ports.Count==2,"Gap filler is not a two-terminal flat SMD component.");
                Require(Mathf.Abs(part.height-f.globalGapComponentHeight*(part.family==4?1.25f:1))<.00001f,"Gap filler height differs from its independent setting.");
            }
            var contacts=new HashSet<string>();var segments=new List<(Vector2 a,Vector2 b,int link,int layer)>();
            for(int index=0;index<p.links.Count;index++)
            {
                var link=p.links[index];
                foreach(var endpoint in new[]{(part:link.a,point:link.path[0]),(part:link.b,point:link.path[link.path.Length-1])})
                {
                    Require(p.parts[endpoint.part].ports.Any(port=>(port.point-endpoint.point).sqrMagnitude<.000001f),"Route misses an actual contact.");
                    Require(contacts.Add(endpoint.part+":"+endpoint.point.x.ToString("R")+":"+endpoint.point.y.ToString("R")),"Two independent signals reuse a pad.");
                }
                for(int i=1;i<link.path.Length;i++)
                {
                    if(link.layer>0&&(i==1||i==link.path.Length-1))continue;
                    var a=link.path[i-1];var b=link.path[i];var delta=b-a;
                    Require(Mathf.Abs(delta.x)<.0001f||Mathf.Abs(delta.y)<.0001f||Mathf.Abs(Mathf.Abs(delta.x)-Mathf.Abs(delta.y))<.0001f,$"Non-octilinear copper, link {index}, segment {i}: {delta}.");
                    if(link.layer==0)
                        for(int k=0;k<count;k++)
                        {
                            if((k==link.a&&i==1)||(k==link.b&&i==link.path.Length-1))continue;
                            var part=p.parts[k];var half=part.size*.5f+Vector2.one*((f.globalTraceWidth+f.globalTrackClearance)*.5f);
                            Require(!PCBTrackRouter.IntersectsBox(a,b,part.position-half,part.position+half),"Top copper crosses a component footprint.");
                        }
                    segments.Add((a,b,index,link.layer));
                }
                if(link.layer>0){segments.Add((link.path[0],link.viaA,index,0));segments.Add((link.viaB,link.path[link.path.Length-1],index,0));}
            }
            if(f.globalConnectAllChipPins)
            for(int i=0;i<count;i++)if(p.parts[i].family==0)
                Require(p.links.Count(l=>l.a==i||l.b==i)==p.parts[i].ports.Count,"Unconnected modeled IC pin on component "+i);
            foreach(var part in p.parts)if(part.prefab&&part.family==2)
                Require(Mathf.Abs(part.height-part.sourceSize.y*part.sourceScale*f.globalRamHeightScale)<.00001f,"RAM height scale differs from its setting.");
            float minimum=f.globalTraceWidth+f.globalTrackClearance;
            for(int i=0;i<segments.Count;i++)for(int j=i+1;j<segments.Count;j++)
            {
                var a=segments[i];var b=segments[j];if(a.layer!=b.layer||a.link==b.link)continue;
                Require(PCBTrackRouter.Distance(a.a,a.b,b.a,b.b)>=minimum-.0002f,$"Copper crossing/clearance violation on layer {a.layer}, links {a.link}/{b.link}.");
            }
            // The visible circles also need clearance from other nets.
            for(int i=0;i<p.links.Count;i++)if(p.links[i].layer>0)
            {
                foreach(var via in new[]{p.links[i].viaA,p.links[i].viaB})
                    foreach(var segment in segments)if(segment.link!=i)
                        Require(PCBTrackRouter.PointDistance(via,segment.a,segment.b)>=f.globalTraceWidth*1.6f+f.globalTrackClearance-.0002f,"Via overlaps another top signal.");
            }
            for(int i=0;i<p.vias.Count;i++)for(int j=i+1;j<p.vias.Count;j++)
                Require((p.vias[i]-p.vias[j]).magnitude>=f.globalTraceWidth*2.2f+f.globalTrackClearance-.0002f,"Overlapping vias.");
            // Footprints must neither overlap nor fall outside the exact board.
            for(int i=0;i<count;i++)
            {
                var a=p.parts[i];Require(Mathf.Abs(a.position.x)+a.size.x*.5f<=p.size.x*.5f&&Mathf.Abs(a.position.y)+a.size.y*.5f<=p.size.y*.5f,"Component outside board.");
                for(int j=i+1;j<count;j++){var b=p.parts[j];var d=a.position-b.position;var half=(a.size+b.size)*.5f;Require(Mathf.Abs(d.x)>=half.x||Mathf.Abs(d.y)>=half.y,"Overlapping components.");}
            }
        }
        static string Fingerprint(PCBGlobalBoard.Plan p)=>string.Join(";",p.parts.Select(c=>$"{c.position.x:R}/{c.position.y:R}/{c.size.x:R}/{c.size.y:R}/{c.height:R}/{c.quarterTurn}/{c.family}"))+string.Join(";",p.links.Select(l=>$"{l.a}/{l.b}/{l.layer}:"+string.Join(",",l.path.Select(q=>$"{q.x:R}/{q.y:R}"))));
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
