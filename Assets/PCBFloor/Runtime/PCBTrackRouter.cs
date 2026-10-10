using System;
using System.Collections.Generic;
using UnityEngine;
namespace AAR.PCB
{
    // A copper layer owns complete swept segments, not just polyline vertices.
    // Geometry clearance is authoritative; the grid only accelerates path searches.
    public sealed class PCBTrackRouter
    {
        struct Segment { public Vector2 a,b; public int partA,partB; }
        readonly PCBGlobalBoard.Plan plan;
        readonly List<Segment> tracks=new List<Segment>();
        readonly List<Vector2> vias=new List<Vector2>();
        readonly List<(Vector2 pad,Vector2 via)> launches=new List<(Vector2,Vector2)>();
        readonly float viaRadius,viaGap;
        readonly bool bottom;
        readonly bool[] obstacles, copper;
        readonly int w,h;
        public readonly float step, gap, padKeepout;
        public int expanded;
        public string LastFailure {get;private set;}
        bool Reject(string reason){LastFailure=reason;return false;}
        public PCBTrackRouter(PCBGlobalBoard.Plan plan,float requestedStep,float trackWidth,float clearance,bool bottom=false)
        {
            this.plan=plan;this.bottom=bottom;gap=trackWidth+clearance;viaRadius=trackWidth*1.1f;viaGap=viaRadius+trackWidth*.5f+clearance;
            step=Mathf.Max(requestedStep,Mathf.Max(plan.size.x,plan.size.y)/1000);
            padKeepout=gap+step*.75f+viaGap;
            w=Mathf.CeilToInt(plan.size.x/step)+1;h=Mathf.CeilToInt(plan.size.y/step)+1;
            obstacles=new bool[w*h];copper=new bool[w*h];
            foreach(var part in plan.parts)
            {
                if(bottom&&part.family!=0)continue;
                var half=part.size*.5f+part.padHalo;
                for(int y=Y(part.position.y-half.y);y<=Y(part.position.y+half.y);y++)for(int x=X(part.position.x-half.x);x<=X(part.position.x+half.x);x++)obstacles[y*w+x]=true;
            }
        }
        int X(float x)=>Mathf.Clamp(Mathf.RoundToInt((x+plan.size.x*.5f)/step),0,w-1);
        int Y(float y)=>Mathf.Clamp(Mathf.RoundToInt((y+plan.size.y*.5f)/step),0,h-1);
        int Cell(Vector2 p)=>Y(p.y)*w+X(p.x);
        Vector2 Point(int n)=>new Vector2(n%w*step-plan.size.x*.5f,n/w*step-plan.size.y*.5f);
        public static float PointDistance(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;return (p-(a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(1e-12f,d.sqrMagnitude)))).magnitude;}
        public static float Distance(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            var u=b-a;var v=d-c;float cross=u.x*v.y-u.y*v.x;
            if(Mathf.Abs(cross)>1e-7f){var q=c-a;float t=(q.x*v.y-q.y*v.x)/cross,s=(q.x*u.y-q.y*u.x)/cross;if(t>=0&&t<=1&&s>=0&&s<=1)return 0;}
            return Mathf.Min(PointDistance(a,c,d),PointDistance(b,c,d),PointDistance(c,a,b),PointDistance(d,a,b));
        }
        public bool Valid(Vector2[] path,int a,int b)
        {
            for(int i=1;i<path.Length;i++)
            {
                var from=path[i-1];var to=path[i];
                if(Mathf.Abs(from.x)>plan.size.x*.5f-gap||Mathf.Abs(from.y)>plan.size.y*.5f-gap)return Reject("board edge");
                foreach(var launch in launches)
                {
                    bool own=(path[0]-launch.pad).sqrMagnitude<1e-8f||(path[path.Length-1]-launch.pad).sqrMagnitude<1e-8f||(path[0]-launch.via).sqrMagnitude<1e-8f||(path[path.Length-1]-launch.via).sqrMagnitude<1e-8f;
                    if(!own&&(Distance(from,to,launch.pad,launch.via)<gap-.0001f||PointDistance(launch.via,from,to)<viaGap-.0001f))return Reject("reserved launch");
                }
                foreach(var s in tracks)if(Distance(from,to,s.a,s.b)<gap-.0001f)return Reject("track clearance");
                foreach(var via in vias)if(PointDistance(via,from,to)<viaGap-.0001f)return Reject("via clearance");
                for(int part=0;part<plan.parts.Count;part++)
                {
                    if((part==a&&i==1)||(part==b&&i==path.Length-1))continue;
                    var p=plan.parts[part];if(bottom&&p.family!=0)continue;var half=p.size*.5f+p.padHalo;
                    if(IntersectsBox(from,to,p.position-half,p.position+half))return Reject("footprint");
                }
            }
            // Reject a route folding across itself, including short escape segments.
            for(int i=1;i<path.Length;i++)for(int j=i+2;j<path.Length;j++)
            {
                float distance=Distance(path[i-1],path[i],path[j-1],path[j]);
                float between=0;for(int k=i+1;k<j;k++)between+=(path[k]-path[k-1]).magnitude;
                // Small grid-to-pad doglegs form one joined corner. Their own
                // swept copper may overlap locally, but never cross itself.
                if(distance<.00001f||(between>gap*2&&distance<gap*.8f))return Reject("self fold");
            }
            return true;
        }
        public static bool IntersectsBox(Vector2 a,Vector2 b,Vector2 min,Vector2 max)
        {
            var d=b-a;float lo=0,hi=1;
            for(int axis=0;axis<2;axis++){if(Mathf.Abs(d[axis])<1e-9f){if(a[axis]<min[axis]||a[axis]>max[axis])return false;}
                else{float t0=(min[axis]-a[axis])/d[axis],t1=(max[axis]-a[axis])/d[axis];lo=Mathf.Max(lo,Mathf.Min(t0,t1));hi=Mathf.Min(hi,Mathf.Max(t0,t1));if(lo>hi)return false;}}
            return true;
        }
        public bool Find(Vector2 start,Vector2 escapeA,Vector2 escapeB,Vector2 end,int a,int b,out Vector2[] result)
        {
            result=null;
            if(!Valid(new[]{start,escapeA},a,a)||!Valid(new[]{escapeB,end},b,b))return false;
            // Prefer clean, low-bend octilinear routes before any maze search.
            var d=escapeB-escapeA;float diag=Mathf.Min(Mathf.Abs(d.x),Mathf.Abs(d.y));var sign=new Vector2(Mathf.Sign(d.x),Mathf.Sign(d.y));
            var candidates=new[]{
                new[]{escapeA,escapeA+sign*diag,escapeB},
                new[]{escapeA,escapeB-sign*diag,escapeB},
                PCBMacroRouting.FeedRoute(escapeA,escapeB),
                new[]{escapeA,new Vector2(escapeA.x,escapeB.y),escapeB},
                new[]{escapeA,new Vector2(escapeB.x,escapeA.y),escapeB}};
            foreach(var middle in candidates){var path=Join(start,middle,end);if(Valid(path,a,b)){result=path;return true;}}
            int first=Cell(escapeA),last=Cell(escapeB);
            bool Blocked(int n)
            {
                var p=Point(n);
                // The route may leave its own reserved launch; exact geometry still
                // validates every segment before accepting the grid path.
                float launchRadius=viaGap+step*1.5f;
                if((p-escapeA).sqrMagnitude<launchRadius*launchRadius||(p-escapeB).sqrMagnitude<launchRadius*launchRadius)return false;
                return obstacles[n]||copper[n];
            }
            if(Blocked(first)||Blocked(last))return Reject("endpoint grid");
            // Eight heading states ensure the bend penalty really chooses smooth routes.
            int states=w*h*8;var costs=new Dictionary<int,float>();var parents=new Dictionary<int,int>();var closed=new HashSet<int>();var queue=new Heap();
            int initial=first*8;costs[initial]=0;parents[initial]=-1;queue.Add(initial,Estimate(first,last));
            int[] dx={1,1,0,-1,-1,-1,0,1},dy={0,1,1,1,0,-1,-1,-1};
            int searches=0;
            while(queue.Count>0&&searches++<Mathf.Min(states,30000))
            {
                int state=queue.Pop();if(!closed.Add(state))continue;expanded++;int n=state/8,heading=state%8;
                if(n==last)
                {
                    var raw=new List<Vector2>();for(int s=state;s!=-1;s=parents[s])raw.Add(Point(s/8));raw.Reverse();
                    var middle=new List<Vector2>{escapeA};middle.AddRange(PCBMacroRouting.FeedRoute(escapeA,raw[0]));middle.AddRange(raw);middle.AddRange(PCBMacroRouting.FeedRoute(raw[raw.Count-1],escapeB));
                    var path=Join(start,middle.ToArray(),end);if(Valid(path,a,b)){result=path;return true;}continue;
                }
                int x=n%w,y=n/w;
                for(int dir=0;dir<8;dir++)
                {
                    int nx=x+dx[dir],ny=y+dy[dir];if(nx<1||ny<1||nx>=w-1||ny>=h-1)continue;int next=ny*w+nx;
                    if(Blocked(next))continue;
                    if(dir%2==1&&(Blocked(y*w+nx)||Blocked(ny*w+x)))continue;
                    int ns=next*8+dir;if(closed.Contains(ns))continue;
                    int turn=Mathf.Abs(dir-heading);turn=Mathf.Min(turn,8-turn);
                    float cost=costs[state]+(dir%2==0?1:1.414214f)+(state==initial?0:turn*2.5f);
                    if(costs.TryGetValue(ns,out float old)&&old<=cost)continue;costs[ns]=cost;parents[ns]=state;queue.Add(ns,cost+Estimate(next,last));
                }
            }
            return Reject("search budget or sealed grid");
        }
        float Estimate(int a,int b){float x=Mathf.Abs(a%w-b%w),y=Mathf.Abs(a/w-b/w);return Mathf.Max(x,y)+.414214f*Mathf.Min(x,y);}
        public bool CanVia(Vector2 p)
        {
            foreach(var launch in launches)if((p-launch.via).sqrMagnitude>1e-8f&&
                (PointDistance(p,launch.pad,launch.via)<viaGap||(p-launch.via).magnitude<viaRadius+viaGap))return false;
            foreach(var track in tracks)if(PointDistance(p,track.a,track.b)<viaGap)return false;
            foreach(var via in vias)if((p-via).magnitude<viaRadius+viaGap)return false;return true;
        }
        public void ProtectLaunch(Vector2 pad,Vector2 via)
        {
            launches.Add((pad,via));
            float radius=viaGap+step*.72f;
            int x0=X(Mathf.Min(pad.x,via.x)-radius),x1=X(Mathf.Max(pad.x,via.x)+radius),y0=Y(Mathf.Min(pad.y,via.y)-radius),y1=Y(Mathf.Max(pad.y,via.y)+radius);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(PointDistance(Point(y*w+x),pad,via)<radius)copper[y*w+x]=true;
        }
        public void ReserveVia(Vector2 p)
        {
            vias.Add(p);float radius=viaGap+step*.72f;
            for(int y=Y(p.y-radius);y<=Y(p.y+radius);y++)for(int x=X(p.x-radius);x<=X(p.x+radius);x++)if((Point(y*w+x)-p).magnitude<radius)copper[y*w+x]=true;
        }
        public void Reserve(Vector2[] path,int a,int b)
        {
            for(int i=1;i<path.Length;i++)
            {
                var s=new Segment{a=path[i-1],b=path[i],partA=a,partB=b};tracks.Add(s);
                // Every pixel in the swept track and its clearance is reserved.
                int x0=X(Mathf.Min(s.a.x,s.b.x)-gap-step),x1=X(Mathf.Max(s.a.x,s.b.x)+gap+step),y0=Y(Mathf.Min(s.a.y,s.b.y)-gap-step),y1=Y(Mathf.Max(s.a.y,s.b.y)+gap+step);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(PointDistance(Point(y*w+x),s.a,s.b)<gap+step*.72f)copper[y*w+x]=true;
            }
        }
        static Vector2[] Join(Vector2 start,Vector2[] middle,Vector2 end)
        {
            var points=new List<Vector2>{start};points.AddRange(middle);points.Add(end);var clean=new List<Vector2>();
            foreach(var p in points){if(clean.Count>0&&(p-clean[clean.Count-1]).sqrMagnitude<1e-10f)continue;
                while(clean.Count>=2){var a=clean[clean.Count-1]-clean[clean.Count-2];var b=p-clean[clean.Count-1];if(Mathf.Abs(a.x*b.y-a.y*b.x)<=.000001f*Mathf.Sqrt(a.sqrMagnitude*b.sqrMagnitude)&&Vector2.Dot(a,b)>=0)clean.RemoveAt(clean.Count-1);else break;}clean.Add(p);}
            return clean.ToArray();
        }
        sealed class Heap
        {
            readonly List<(int node,float score)> items=new List<(int,float)>();public int Count=>items.Count;
            public void Add(int n,float s){int i=items.Count;items.Add((n,s));while(i>0){int p=(i-1)/2;if(items[p].score<=s)break;items[i]=items[p];i=p;}items[i]=(n,s);}
            public int Pop(){int n=items[0].node;var last=items[items.Count-1];items.RemoveAt(items.Count-1);if(items.Count==0)return n;int i=0;while(i*2+1<items.Count){int c=i*2+1;if(c+1<items.Count&&items[c+1].score<items[c].score)c++;if(items[c].score>=last.score)break;items[i]=items[c];i=c;}items[i]=last;return n;}
        }
    }
}
