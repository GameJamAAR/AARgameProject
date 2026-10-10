using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCB
{
    // All coordinates belong to the board. Render chunks are absent from this plan.
    public static class PCBGlobalBoard
    {
        public sealed class Part
        {
            public GameObject prefab;
            public Vector2 position, size, padHalo,placementHalo;
            public float height,sourceScale;
            public bool gapFiller;
            public int quarterTurn, family, group;
            public Vector3 sourceSize;
            public readonly List<PCBPort> ports=new List<PCBPort>();
        }
        public struct PCBPort { public Vector2 point,direction; }
        public sealed class Link { public int a,b,layer; public Vector2[] path; public Vector2 viaA,viaB; }
        public sealed class Plan
        {
            public Vector2 size;
            public readonly List<Part> parts=new List<Part>();
            public readonly List<Link> links=new List<Link>();
            public int groups, expandedNodes, gapFillParts; public readonly List<Vector2> vias=new List<Vector2>();
        }
        static Vector2 Turn(Vector2 p,int turn)=>turn%2==0 ? (turn==0?p:-p) : (turn==1?new Vector2(p.y,-p.x):new Vector2(-p.y,p.x));
        public static Plan Create(PCBFloorGenerator floor)
        {
            var plan=new Plan{size=new Vector2(Mathf.Max(8,floor.globalBoardSize.x),Mathf.Max(8,floor.globalBoardSize.y))};
            var rng=new System.Random(floor.seed);var centers=new List<Vector2>();
            float unit=Mathf.Clamp(floor.globalComponentSize,.5f,6);
            float spread=Mathf.Max(unit*2,floor.globalGroupSpread);
            int requested=Mathf.Clamp(floor.globalGroupCount,1,32);
            float routingStep=PCBTrackRouter.EffectiveStep(plan.size,floor.globalRoutingStep);
            float routingClearance=floor.globalTraceWidth+floor.globalTrackClearance;
            float terminalHalo=routingClearance+routingStep*.75f+floor.globalTraceWidth*1.6f+floor.globalTrackClearance;
            float trackHalo=floor.globalTraceWidth*.5f+floor.globalTrackClearance;
            float routingAisle=routingStep*.65f;
            var contactSides=new Dictionary<GameObject,Vector2>();
            var occupied=new Dictionary<Vector2Int,List<int>>();float bucket=unit*2;
            bool Place(Part part)
            {
                var half=part.size*.5f+part.placementHalo+Vector2.one*routingAisle*.5f;
                if(Mathf.Abs(part.position.x)+half.x>plan.size.x*.5f-routingClearance-routingStep*2-.2f||Mathf.Abs(part.position.y)+half.y>plan.size.y*.5f-routingClearance-routingStep*2-.2f)return false;
                var lo=Vector2Int.FloorToInt((part.position-half)/bucket);var hi=Vector2Int.FloorToInt((part.position+half)/bucket);
                for(int y=lo.y;y<=hi.y;y++)for(int x=lo.x;x<=hi.x;x++)if(occupied.TryGetValue(new Vector2Int(x,y),out var ids))foreach(int id in ids)
                {var other=plan.parts[id];var d=part.position-other.position;var sum=(part.size+other.size)*.5f+part.placementHalo+other.placementHalo+Vector2.one*routingAisle;if(Mathf.Abs(d.x)<sum.x&&Mathf.Abs(d.y)<sum.y)return false;}
                int index=plan.parts.Count;plan.parts.Add(part);
                for(int y=lo.y;y<=hi.y;y++)for(int x=lo.x;x<=hi.x;x++){var key=new Vector2Int(x,y);if(!occupied.TryGetValue(key,out var list))occupied[key]=list=new List<int>();list.Add(index);}return true;
            }
            Part Make(int family,Vector2 p,int group,float multiplier)
            {
                var part=new Part{family=family,position=p,group=group,quarterTurn=rng.Next(4)};if(family==1)part.quarterTurn=0;
                var kit=floor.library;var variants=family>=3?null:family==1?kit.importedCapacitors:family==2?kit.importedModules:kit.importedPassives;
                if(variants!=null&&variants.Length>0)part.prefab=variants[rng.Next(variants.Length)];
                float length=unit*multiplier;
                if(part.prefab)
                {
                    var b=part.prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;part.sourceSize=b.size;
                    float s=length/Mathf.Max(b.size.x,b.size.z);
                    if(family==0&&floor.globalConnectAllChipPins)
                    {
                        var pins=PCBGlobalContacts.Contacts(part.prefab,Vector2.zero,b.size);
                        float pitch=float.MaxValue;
                        for(int i=0;i<pins.Count;i++)for(int j=i+1;j<pins.Count;j++)pitch=Mathf.Min(pitch,(pins[i].point-pins[j].point).magnitude);
                        // Full fanouts need room for both copper and independent via annuli.
                        if(pitch<float.MaxValue)s=Mathf.Max(s,Mathf.Max(floor.globalTraceWidth*2.2f+floor.globalTrackClearance+.065f, Mathf.Max(.5f,routingStep*2+(floor.globalTraceWidth+floor.globalTrackClearance)*1.5f))/pitch);
                    }
                    part.sourceScale=s;part.size=new Vector2(b.size.x*s,b.size.z*s);
                    if(family==2)part.size+=new Vector2(.12f,.08f);
                    part.height=b.size.y*s*(family==1?floor.globalCapacitorHeightScale:family==2?floor.globalRamHeightScale:floor.globalHeightScale);
                }
                else {part.size=family==1?Vector2.one*length:family==3?new Vector2(length*.42f,length):new Vector2(length*.7f,length);part.height=(family==1?length*1.5f:family==3?length*.22f:length*.3f)*(family==1?floor.globalCapacitorHeightScale:family==2?floor.globalRamHeightScale:floor.globalHeightScale);}
                Vector2 sides=family>=3?Vector2.up:family==1?Vector2.right:family==2?(part.size.x>part.size.y?Vector2.up:Vector2.right):Vector2.one;
                if(family==0&&part.prefab)
                {
                    if(!contactSides.TryGetValue(part.prefab,out sides))
                    {sides=Vector2.zero;foreach(var port in PCBGlobalContacts.Contacts(part.prefab,Vector2.zero,part.sourceSize))sides+=new Vector2(Mathf.Abs(port.direction.x),Mathf.Abs(port.direction.y));if(sides==Vector2.zero)sides=Vector2.one;contactSides[part.prefab]=sides;}
                }
                // Only the sides with contacts require a long protected escape halo.
                part.padHalo=new Vector2(sides.x>0?terminalHalo:trackHalo,sides.y>0?terminalHalo:trackHalo);
                part.placementHalo=part.padHalo;
                if(family==0&&floor.globalConnectAllChipPins)part.placementHalo+=new Vector2(sides.x>0?.4f:0,sides.y>0?.4f:0);
                if(part.quarterTurn%2!=0){part.placementHalo=new Vector2(part.placementHalo.y,part.placementHalo.x);part.size=new Vector2(part.size.y,part.size.x);part.padHalo=new Vector2(part.padHalo.y,part.padHalo.x);}
                return part;
            }
            for(int group=0;group<requested;group++)
            {
                // Best-candidate coverage uses the whole board, including its edges.
                // Regions are placement guides only, never repeated circuit tiles.
                Vector2 center=Vector2.zero;float best=-1;
                for(int attempt=0;attempt<180;attempt++)
                {
                    var candidate=new Vector2(((float)rng.NextDouble()-.5f)*(plan.size.x-unit*4),((float)rng.NextDouble()-.5f)*(plan.size.y-unit*4));
                    float score=float.MaxValue;foreach(var c in centers)score=Mathf.Min(score,(candidate-c).sqrMagnitude);
                    if(score>best){best=score;center=candidate;}
                }
                centers.Add(center);
                var weights=floor.library.centralCompositionWeights;float total=Mathf.Max(0,weights.x)+Mathf.Max(0,weights.y)+Mathf.Max(0,weights.z)+Mathf.Max(0,weights.w);
                double choice=rng.NextDouble()*total;int kind=3;for(int k=0;k<4;k++){choice-=Mathf.Max(0,weights[k]);if(choice<0){kind=k;break;}}
                if(group==0&&kind==3 && weights.x+weights.y+weights.z>0)
                {double activeChoice=rng.NextDouble()*(Mathf.Max(0,weights.x)+Mathf.Max(0,weights.y)+Mathf.Max(0,weights.z));for(int k=0;k<3;k++){activeChoice-=Mathf.Max(0,weights[k]);if(activeChoice<0){kind=k;break;}}}
                if(total<=0 || weights.x+weights.y+weights.z<=0)continue;
                int family=kind==0?0:kind==1?1:2;
                int anchors=kind==3?0:family==0?1:family==1?rng.Next(3,Mathf.Clamp(floor.globalCapacitorsPerGroup,3,8)+1):Mathf.Clamp(floor.library.centralRamCount,1,4);
                for(int a=0;a<anchors;a++)
                {
                    var p=family==1?center+new Vector2((a%2-.5f)*unit*2.15f,(a/2-(anchors/2)*.5f)*unit*1.5f):center+Turn(new Vector2((a-(anchors-1)*.5f)*unit*1.8f,0),family==2?group%2:0);
                    var part=Make(family,p,group,family==0?1.8f:family==2?2.6f:a==0?1.3f:a<3?.85f:.55f);
                    if(family==2){if(part.quarterTurn%2!=group%2){part.size=new Vector2(part.size.y,part.size.x);part.padHalo=new Vector2(part.padHalo.y,part.padHalo.x);part.placementHalo=new Vector2(part.placementHalo.y,part.placementHalo.x);}part.quarterTurn=group%2;}
                    Place(part);
                }
                // Compact passive banks share an orientation and leave their pad exits free.
                int bankTurn=group%2,bankCount=Mathf.Clamp(floor.globalResistorBankSize,2,8);
                float bankPitch=unit*.42f*.46f+trackHalo*2+routingAisle+.08f;
                var bankCenter=center+Turn(new Vector2(0,spread*.48f),bankTurn);
                for(int slot=0;slot<bankCount;slot++)
                {
                    var part=Make(3,bankCenter+Turn(new Vector2((slot-(bankCount-1)*.5f)*bankPitch,0),bankTurn),group,.46f);
                    if(part.quarterTurn%2!=bankTurn){part.size=new Vector2(part.size.y,part.size.x);part.padHalo=new Vector2(part.padHalo.y,part.padHalo.x);part.placementHalo=new Vector2(part.placementHalo.y,part.placementHalo.x);}part.quarterTurn=bankTurn;Place(part);
                }
                int satellites=Mathf.RoundToInt(Mathf.Clamp(floor.globalPartsPerGroup,3,40)*floor.componentDensity);
                for(int n=0;n<satellites;n++)for(int attempt=0;attempt<60;attempt++)
                {
                    float angle=(float)rng.NextDouble()*Mathf.PI*2;
                    float radius=spread*(.15f+.60f*(float)rng.NextDouble());
                    var p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    var part=Make(rng.NextDouble()<.5?3:rng.NextDouble()<.8?1:0,p,group,.35f+(float)rng.NextDouble()*.42f);
                    if(Place(part))break;
                }
            }

            // Fill residual board-wide gaps with small passives, not another central chip.
            int target=Mathf.RoundToInt(requested*floor.globalPartsPerGroup*floor.componentDensity);
            var composition=floor.library.centralCompositionWeights;
            if(composition.x+composition.y+composition.z>0)
            for(int n=plan.parts.Count;n<target;n++)
            {
                bool placed=false;
                for(int attempt=0;attempt<100;attempt++)
                {
                    var p=new Vector2(((float)rng.NextDouble()-.5f)*(plan.size.x-unit*2),((float)rng.NextDouble()-.5f)*(plan.size.y-unit*2));
                    int nearest=0;float nearestDistance=float.MaxValue;for(int g=0;g<centers.Count;g++){float distance=(centers[g]-p).sqrMagnitude;if(distance<nearestDistance){nearestDistance=distance;nearest=g;}}
                    if(Place(Make(rng.NextDouble()<.65?3:1,p,nearest,.32f+(float)rng.NextDouble()*.46f))){placed=true;break;}
                }
                if(!placed)break;
            }

            // Fill actual gaps independently of the primary group quota. Best-candidate
            // sampling measures distance to footprints, so large empty spaces are filled
            // first without imposing another tile grid. The normal router connects these
            // two-pad SMD parts along with the rest of the circuit.
            if(composition.x+composition.y+composition.z>0 && floor.componentDensity>0)
            {
                int banks=Mathf.Min(160,Mathf.RoundToInt(plan.size.x*plan.size.y/
                    Mathf.Max(4,unit*unit*5)*floor.globalGapFillDensity*floor.componentDensity));
                for(int bank=0;bank<banks;bank++)
                {
                    Vector2 center=Vector2.zero;float best=-1;
                    for(int sample=0;sample<32;sample++)
                    {
                        var candidate=new Vector2(((float)rng.NextDouble()-.5f)*plan.size.x,((float)rng.NextDouble()-.5f)*plan.size.y);
                        float distance=Mathf.Min(plan.size.x*.5f-Mathf.Abs(candidate.x),plan.size.y*.5f-Mathf.Abs(candidate.y));
                        foreach(var part in plan.parts)
                        {
                            var delta=candidate-part.position;var half=part.size*.5f+part.placementHalo;
                            var outside=new Vector2(Mathf.Max(0,Mathf.Abs(delta.x)-half.x),Mathf.Max(0,Mathf.Abs(delta.y)-half.y));
                            distance=Mathf.Min(distance,outside.magnitude);
                        }
                        if(distance>best){best=distance;center=candidate;}
                    }
                    if(best<unit*.5f)continue;
                    int nearest=0;float nearestDistance=float.MaxValue;
                    for(int g=0;g<centers.Count;g++){float distance=(centers[g]-center).sqrMagnitude;if(distance<nearestDistance){nearestDistance=distance;nearest=g;}}
                    int turn=rng.Next(4),slots=rng.Next(2,5);
                    float scale=floor.globalGapComponentSize*(.8f+(float)rng.NextDouble()*.4f);
                    float pitch=unit*scale*.7f+trackHalo*2+routingAisle+.10f;
                    for(int slot=0;slot<slots;slot++)
                    {
                        var part=Make(rng.NextDouble()<.6?3:4,center+Turn(new Vector2((slot-(slots-1)*.5f)*pitch,0),turn),nearest,scale);
                        if(part.quarterTurn%2!=turn%2){part.size=new Vector2(part.size.y,part.size.x);part.padHalo=new Vector2(part.padHalo.y,part.padHalo.x);part.placementHalo=new Vector2(part.placementHalo.y,part.placementHalo.x);}
                        part.quarterTurn=turn;part.height=floor.globalGapComponentHeight*(part.family==4?1.25f:1);part.gapFiller=true;
                        if(Place(part))plan.gapFillParts++;
                    }
                }
            }
            plan.groups=centers.Count;
            foreach(var part in plan.parts)
            {
                if(part.family==0&&part.prefab)
                {
                    float s=part.sourceScale;
                    var contacts=PCBGlobalContacts.Contacts(part.prefab,Vector2.zero,part.sourceSize*s);
                    // Keep real mounting contacts. Only routed pins receive copper.
                    foreach(var c in contacts)part.ports.Add(new PCBPort{point=part.position+Turn(c.point,part.quarterTurn),direction=Turn(c.direction,part.quarterTurn)});
                }
                if(part.ports.Count==0)
                {
                    if(part.family==2)
                    {
                        // RAM sockets use separate pads along both long banks.
                        for(int side=-1;side<=1;side+=2)for(int pin=0;pin<12;pin++)
                        {bool alongZ=part.size.y>part.size.x;var local=alongZ?new Vector2(side*part.size.x*.5f,(pin-5.5f)*(part.size.y/14)):new Vector2((pin-5.5f)*(part.size.x/14),side*part.size.y*.5f);var dir=alongZ?new Vector2(side,0):new Vector2(0,side);part.ports.Add(new PCBPort{point=part.position+local,direction=dir});}
                    }
                    else for(int sign=-1;sign<=1;sign+=2)
                    {var dir=part.family>=3?Turn(new Vector2(0,sign),part.quarterTurn):new Vector2(sign,0);var half=dir.x!=0?part.size.x*.5f:part.size.y*.5f;part.ports.Add(new PCBPort{point=part.position+dir*half,direction=dir});}
                }
            }
            Route(plan,floor);return plan;
        }
        struct Edge {public int a,b;public float length;}
        static void Route(Plan plan,PCBFloorGenerator floor)
        {
            int count=plan.parts.Count;if(count==0)return;
            var top=new PCBTrackRouter(plan,floor.globalRoutingStep,floor.globalTraceWidth,floor.globalTrackClearance);
            var bottom=new PCBTrackRouter(plan,floor.globalRoutingStep,floor.globalTraceWidth,floor.globalTrackClearance,true);
            var layers=new List<PCBTrackRouter>{top,bottom};
            for(int layer=2;layer<Mathf.Clamp(floor.globalRoutingLayers,2,8);layer++)layers.Add(new PCBTrackRouter(plan,floor.globalRoutingStep,floor.globalTraceWidth,floor.globalTrackClearance,true));
            var used=new HashSet<int>[count];var parent=new int[count];for(int i=0;i<count;i++){used[i]=new HashSet<int>();parent[i]=i;}
            int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            var edges=new List<Edge>();for(int i=0;i<count;i++)for(int j=i+1;j<count;j++)edges.Add(new Edge{a=i,b=j,length=(plan.parts[i].position-plan.parts[j].position).sqrMagnitude});
            edges.Sort((a,b)=>a.length!=b.length?a.length.CompareTo(b.length):(a.a*count+a.b).CompareTo(b.a*count+b.b));
            int components=count;
            if(floor.globalConnectAllChipPins)foreach(var part in plan.parts)if(part.family==0)foreach(var port in part.ports)
            {var exit=Escape(part,port,1);foreach(var router in layers)router.ProtectLaunch(port.point,exit);}
            if(floor.globalConnectAllChipPins)
                for(int a=0;a<count;a++)if(plan.parts[a].family==0)
                for(int pin=0;pin<plan.parts[a].ports.Count;pin++)
                    if(!used[a].Contains(pin)&&!ConnectPin(a,pin))
                        throw new InvalidOperationException("PCB cannot connect pin "+(pin+1)+" of "+plan.parts[a].prefab.name+" with the requested clearance. Increase board area, routing layers or reduce component density.");
            // Route ordered socket buses first, before local copper occupies their corridors.
            // Each rail joins a different physical pad and is validated individually.
            foreach(var e in edges)
            {
                var a=plan.parts[e.a];var b=plan.parts[e.b];
                if(a.family!=2||b.family!=2||a.group!=b.group||e.length>floor.globalGroupSpread*floor.globalGroupSpread)continue;
                for(int signal=0;signal<Mathf.Min(8,floor.globalSignalsPerChip);signal++)
                {
                    if(!TryLink(e.a,e.b,false))break;
                    if(Root(e.a)!=Root(e.b)){parent[Root(e.a)]=Root(e.b);components--;}
                }
            }
            // Then connect the short local pairs with hard clearance on both layers.
            foreach(var e in edges)
            {
                if(Root(e.a)==Root(e.b))continue;
                if(TryLink(e.a,e.b,true)){parent[Root(e.a)]=Root(e.b);components--;if(components==1)break;}
            }
            if(components!=1)throw new InvalidOperationException("PCB has "+components+" disconnected groups after clearance-aware routing. Increase board area or reduce density/clearance.");
            // Real independent signals between nearby IC/RAM pads create ordered fanouts.
            int signals=Mathf.Clamp(floor.globalSignalsPerChip,0,24);
            for(int a=0;a<count;a++)if(plan.parts[a].family==0||plan.parts[a].family==2)
            {
                var nearby=new List<int>();for(int b=0;b<count;b++)if(b!=a&&(plan.parts[b].position-plan.parts[a].position).sqrMagnitude<floor.globalGroupSpread*floor.globalGroupSpread*3)nearby.Add(b);
                nearby.Sort((b,c)=>(plan.parts[b].position-plan.parts[a].position).sqrMagnitude.CompareTo((plan.parts[c].position-plan.parts[a].position).sqrMagnitude));
                int routed=0;foreach(int b in nearby){if(routed>=signals)break;if(TryLink(a,b,false))routed++;}
                // Revisit controller pairs for several separate, parallel signal pins.
                foreach(int b in nearby)if(plan.parts[b].family==0||plan.parts[b].family==2)
                    for(int signal=routed;signal<signals;signal++){if(!TryLink(a,b,false))break;routed++;}
            }
            // Use the remaining SMD terminals for short visible connections. These
            // are real, clearance-checked nets rather than decorative loose tracks.
            foreach(var edge in edges)
                if((plan.parts[edge.a].gapFiller||plan.parts[edge.b].gapFiller) &&
                    edge.length<floor.globalComponentSize*floor.globalComponentSize*25)
                    TryLink(edge.a,edge.b,false);
            plan.expandedNodes=0;foreach(var router in layers)plan.expandedNodes+=router.expanded;
            bool ConnectPin(int a,int pin)
            {
                var candidates=new List<int>();for(int b=0;b<count;b++)if(b!=a&&used[b].Count<plan.parts[b].ports.Count)candidates.Add(b);
                var point=plan.parts[a].ports[pin].point;
                candidates.Sort((b,c)=>(plan.parts[b].position-point).sqrMagnitude.CompareTo((plan.parts[c].position-point).sqrMagnitude));
                foreach(int b in candidates)if(TryLink(a,b,true,pin))
                {if(Root(a)!=Root(b)){parent[Root(a)]=Root(b);components--;}return true;}
                return false;
            }
            int OpenEnds(int root)
            {int available=0;for(int i=0;i<count;i++)if(Root(i)==root)available+=plan.parts[i].ports.Count-used[i].Count;return available;}
            bool TryLink(int ai,int bi,bool allowBottom,int requiredPin=-1)
            {
                var a=plan.parts[ai];var b=plan.parts[bi];if(used[ai].Count>=a.ports.Count||used[bi].Count>=b.ports.Count)return false;
                // A completed IC fanout must leave its connected subgraph a way
                // to join the rest of the board. Avoid closing all remaining pads.
                if(floor.globalConnectAllChipPins&&components>1)
                {
                    int ra=Root(ai),rb=Root(bi);
                    if(ra==rb&&OpenEnds(ra)<=2)return false;
                    if(ra!=rb&&components>2&&OpenEnds(ra)+OpenEnds(rb)<=2)return false;
                }
                var pairs=new List<(int a,int b,float score)>();
                for(int i=0;i<a.ports.Count;i++)if(!used[ai].Contains(i)&&(requiredPin<0||i==requiredPin))for(int j=0;j<b.ports.Count;j++)if(!used[bi].Contains(j))
                {var pa=a.ports[i];var pb=b.ports[j];var delta=pb.point-pa.point;float facing=(2-Vector2.Dot(delta.normalized,pa.direction)+Vector2.Dot(delta.normalized,pb.direction))*2;
                    pairs.Add((i,j,delta.magnitude+facing));}
                pairs.Sort((p,q)=>p.score!=q.score?p.score.CompareTo(q.score):(p.a*64+p.b).CompareTo(q.a*64+q.b));
                for(int layer=0;layer<(allowBottom?layers.Count:1);layer++)
                {
                for(int attempt=0;attempt<Mathf.Min(pairs.Count,layer==0?8:12);attempt++)
                {
                    var pair=pairs[attempt];var pa=a.ports[pair.a];var pb=b.ports[pair.b];var sa=Escape(a,pa,layer);var sb=Escape(b,pb,layer);
                    if(layer>0)
                    {
                        bool free=top.Valid(new[]{pa.point,sa},ai,ai)&&top.Valid(new[]{pb.point,sb},bi,bi);
                        foreach(var plane in layers)if(!plane.CanVia(sa)||!plane.CanVia(sb))free=false;
                        if(!free)continue;
                    }
                    var router=layers[layer];
                    Vector2 routeStart=layer==0?pa.point:sa,routeEnd=layer==0?pb.point:sb;
                    if(!router.Find(routeStart,sa,sb,routeEnd,ai,bi,out var routedPath))continue;
                    router.Reserve(routedPath,ai,bi);
                    var path=routedPath;
                    if(layer>0){var complete=new List<Vector2>{pa.point};complete.AddRange(routedPath);complete.Add(pb.point);path=complete.ToArray();}used[ai].Add(pair.a);used[bi].Add(pair.b);
                    plan.links.Add(new Link{a=ai,b=bi,path=path,layer=layer,viaA=sa,viaB=sb});
                    if(layer>0){top.Reserve(new[]{pa.point,sa},ai,ai);top.Reserve(new[]{pb.point,sb},bi,bi);plan.vias.Add(sa);plan.vias.Add(sb);foreach(var plane in layers){plane.ReserveVia(sa);plane.ReserveVia(sb);}}return true;
                }
                }
                return false;
            }
            Vector2 Escape(Part part,PCBPort port,int layer)
            {
                var p=port.point;float margin=layer==0||part.family==0?top.padKeepout+top.step*1.25f:top.gap+top.step*.75f;
                if(port.direction.x!=0)p.x=part.position.x+port.direction.x*(part.size.x*.5f+margin);
                else p.y=part.position.y+port.direction.y*(part.size.y*.5f+margin);
                return p;
            }
        }
    }
}
