using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCBMusic
{
    public struct MusicAttention
    {
        public int id, group;
        public MusicRole role;
        public float weight, pan;
        public bool connectedResponse;
    }

    // Geometric visibility approximation. No per-part colliders or renderer identities required.
    public sealed class PCBViewAnalyzer
    {
        public readonly MusicAttention[] selected = new MusicAttention[4];
        public int SelectedCount {get;private set;}
        public int Candidates {get;private set;}
        public int Occluded {get;private set;}
        public PCBMusicSnapshot Snapshot {get;private set;}
        readonly Plane[] planes=new Plane[6];
        readonly int[] top=new int[64];
        readonly float[] scores=new float[64], pans=new float[64];
        readonly Dictionary<Vector2Int,List<int>> buckets=new Dictionary<Vector2Int,List<int>>();
        readonly List<int> nearby=new List<int>(1024);
        readonly HashSet<ulong> relationships=new HashSet<ulong>();
        readonly int[] previousIds={-1,-1,-1,-1};
        Bounds[] bounds=Array.Empty<Bounds>();
        float[] weights=Array.Empty<float>(), targets=Array.Empty<float>(), partPans=Array.Empty<float>();
        Matrix4x4 boardMatrix;
        const float Cell=8;
        public void Bind(PCBMusicSnapshot snapshot)
        {
            Snapshot=snapshot;int n=snapshot?snapshot.parts.Length:0;
            bounds=new Bounds[n];weights=new float[n];targets=new float[n];partPans=new float[n];
            SelectedCount=0;buckets.Clear();relationships.Clear();
            for(int i=0;i<4;i++)previousIds[i]=-1;
            if(snapshot){foreach(var link in snapshot.links)relationships.Add(Pair(link.a,link.b));Reindex();}
        }
        static ulong Pair(int a,int b)=>(ulong)(uint)Mathf.Min(a,b)<<32|(uint)Mathf.Max(a,b);
        void Reindex()
        {
            buckets.Clear();boardMatrix=Snapshot.transform.localToWorldMatrix;
            for(int i=0;i<bounds.Length;i++)
            {
                var p=Snapshot.parts[i];Vector3 e=p.size*.5f;
                Vector3 x=boardMatrix.MultiplyVector(new Vector3(e.x,0,0)), y=boardMatrix.MultiplyVector(new Vector3(0,e.y,0)), z=boardMatrix.MultiplyVector(new Vector3(0,0,e.z));
                var ext=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
                bounds[i]=new Bounds(boardMatrix.MultiplyPoint3x4(p.center),ext*2);
                var key=new Vector2Int(Mathf.FloorToInt(bounds[i].center.x/Cell),Mathf.FloorToInt(bounds[i].center.z/Cell));
                if(!buckets.TryGetValue(key,out var list)) buckets.Add(key,list=new List<int>());
                list.Add(i);
            }
        }
        public void Analyze(Camera camera,PCBMusicProfile profile,float dt)
        {
            SelectedCount=Candidates=Occluded=0;if(!Snapshot||!camera)return;
            if(boardMatrix!=Snapshot.transform.localToWorldMatrix) Reindex();
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            Array.Clear(targets,0,targets.Length);nearby.Clear();
            Vector3 origin=camera.transform.position;
            int radius=Mathf.CeilToInt(profile.maxDistance/Cell)+1;
            var center=new Vector2Int(Mathf.FloorToInt(origin.x/Cell),Mathf.FloorToInt(origin.z/Cell));
            for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)
                if(buckets.TryGetValue(center+new Vector2Int(x,z),out var list)) nearby.AddRange(list);
            int count=0, limit=Mathf.Clamp(profile.candidateLimit,4,64);
            for(int j=0;j<nearby.Count;j++)
            {
                int i=nearby[j];var b=bounds[i];
                if(!profile.includePassives&&Snapshot.parts[i].role==MusicRole.Passive)continue;
                if((b.ClosestPoint(origin)-origin).sqrMagnitude>profile.maxDistance*profile.maxDistance||!GeometryUtility.TestPlanesAABB(planes,b))continue;
                float loX=1,hiX=0,loY=1,hiY=0;int corners=0;
                for(int c=0;c<8;c++)
                {
                    var p=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));
                    if(p.z<camera.nearClipPlane)continue;
                    loX=Mathf.Min(loX,Mathf.Clamp01(p.x));hiX=Mathf.Max(hiX,Mathf.Clamp01(p.x));
                    loY=Mathf.Min(loY,Mathf.Clamp01(p.y));hiY=Mathf.Max(hiY,Mathf.Clamp01(p.y));corners++;
                }
                if(corners==0||hiX<=loX||hiY<=loY)continue;
                float midX=(loX+hiX)*.5f, midY=(loY+hiY)*.5f;
                float area=(hiX-loX)*(hiY-loY);
                float score=Mathf.Min(.9f,Mathf.Sqrt(area)*5)*(1-profile.centerBias*.3f+profile.centerBias*.3f*Mathf.Exp(-8*((midX-.5f)*(midX-.5f)+(midY-.5f)*(midY-.5f))));
                if(Snapshot.parts[i].role==MusicRole.Passive)score*=.4f;
                Candidates++;
                int at=count;while(at>0&&scores[at-1]<score)at--;
                if(at>=limit)continue;
                int end=Mathf.Min(count,limit-1);for(int k=end;k>at;k--){top[k]=top[k-1];scores[k]=scores[k-1];pans[k]=pans[k-1];}
                top[at]=i;scores[at]=score;pans[at]=(midX-.5f)*.6f;count=Mathf.Min(count+1,limit);
            }
            for(int k=0;k<count;k++)
            {
                int i=top[k];float visibility=1;
                if(profile.occlusion)
                {
                    int clear=0;
                    for(int sample=0;sample<3;sample++)
                    {
                        var goal=bounds[i].center+(sample==0?Vector3.zero:Vector3.up*bounds[i].extents.y*(sample==1?.8f:-.5f));
                        var delta=goal-origin;float distance=delta.magnitude;var ray=new Ray(origin,delta.normalized);bool blocked=false;
                        for(int j=0;j<nearby.Count;j++)
                        {
                            int other=nearby[j];if(other==i)continue;
                            if(bounds[other].IntersectRay(ray,out float hit)&&hit<distance-.02f){blocked=true;break;}
                        }
                        if(!blocked)clear++;
                    }
                    visibility=clear/3f;if(clear==0)Occluded++;
                }
                targets[i]=scores[k]*visibility;partPans[i]=pans[k];
            }
            for(int i=0;i<weights.Length;i++)
            {
                weights[i]=Mathf.Lerp(weights[i],targets[i],1-Mathf.Exp(-dt/Mathf.Max(.05f,targets[i]>weights[i]?profile.attentionAttack:profile.attentionRelease)));
                // Hidden/offscreen parts retain attention memory but cannot start new notes.
                if(targets[i]<.01f||weights[i]<.025f)continue;
                var role=Snapshot.parts[i].role;int slot=-1;
                for(int k=0;k<SelectedCount;k++)if(selected[k].role==role){slot=k;break;}
                var item=new MusicAttention{id=i,group=Snapshot.parts[i].group,role=role,weight=weights[i],pan=partPans[i]};
                float importance=weights[i]*(previousIds[(int)role]==i?1.2f:1);
                if(slot>=0){if(importance>selected[slot].weight*(previousIds[(int)role]==selected[slot].id?1.2f:1))selected[slot]=item;}
                else if(SelectedCount<4)selected[SelectedCount++]=item;
            }
            for(int a=0;a<SelectedCount;a++)for(int b=a+1;b<SelectedCount;b++)if(selected[b].weight>selected[a].weight){var swap=selected[a];selected[a]=selected[b];selected[b]=swap;}
            SelectedCount=Mathf.Min(SelectedCount,Mathf.Clamp(profile.maxContributors,1,4));
            for(int i=0;i<SelectedCount;i++)
            {
                previousIds[(int)selected[i].role]=selected[i].id;
                if(i>0){var item=selected[i];item.connectedResponse=relationships.Contains(Pair(selected[0].id,item.id));selected[i]=item;}
            }
        }
    }
}
