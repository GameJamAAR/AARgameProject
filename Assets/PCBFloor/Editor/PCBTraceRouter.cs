using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCB.Editor
{
    // Single visible copper layer: occupied traces and package keepouts cannot be crossed.
    internal sealed class PCBTraceRouter
    {
        const int N=201;
        const float Step=.005f;
        readonly bool[] blocked=new bool[N*N];
        readonly float clearance;
        static readonly int[] DX={1,1,0,-1,-1,-1,0,1}, DZ={0,1,1,1,0,-1,-1,-1};
        internal PCBTraceRouter(float clearance) { this.clearance=clearance; }
        static Vector2 Point(int n) => new Vector2(n%N*Step-.5f,n/N*Step-.5f);
        static int Cell(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y+.5f)/Step),0,N-1)*N+Mathf.Clamp(Mathf.RoundToInt((p.x+.5f)/Step),0,N-1);
        internal void Keepout(Vector2 center,Vector2 size)
        {
            var half=size*.5f+Vector2.one*clearance;
            for(int z=Mathf.Max(0,Mathf.FloorToInt((center.y-half.y+.5f)/Step));z<=Mathf.Min(N-1,Mathf.CeilToInt((center.y+half.y+.5f)/Step));z++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt((center.x-half.x+.5f)/Step));x<=Mathf.Min(N-1,Mathf.CeilToInt((center.x+half.x+.5f)/Step));x++)
                {
                    var offset=Point(z*N+x)-center;
                    if(Mathf.Abs(offset.x)<=half.x&&Mathf.Abs(offset.y)<=half.y)blocked[z*N+x]=true;
                }
        }
        internal bool CanPlace(Vector2 center,Vector2 size)
        {
            var half=size*.5f+Vector2.one*clearance;
            if(Mathf.Abs(center.x)+half.x>.49f||Mathf.Abs(center.y)+half.y>.49f)return false;
            int left=Mathf.RoundToInt((center.x-half.x+.5f)/Step),right=Mathf.RoundToInt((center.x+half.x+.5f)/Step);
            int bottom=Mathf.RoundToInt((center.y-half.y+.5f)/Step),top=Mathf.RoundToInt((center.y+half.y+.5f)/Step);
            for(int z=bottom;z<=top;z++)for(int x=left;x<=right;x++)if(blocked[z*N+x])return false;
            return true;
        }
        internal void Reserve(Vector2[] points,float width)
        {
            float radius=width*.5f+clearance;
            for(int i=1;i<points.Length;i++)
            {
                var a=points[i-1];var b=points[i];var delta=b-a;float length=delta.sqrMagnitude;
                var lo=Vector2.Min(a,b)-Vector2.one*radius;var hi=Vector2.Max(a,b)+Vector2.one*radius;
                for(int z=Mathf.Max(0,Mathf.FloorToInt((lo.y+.5f)/Step));z<=Mathf.Min(N-1,Mathf.CeilToInt((hi.y+.5f)/Step));z++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt((lo.x+.5f)/Step));x<=Mathf.Min(N-1,Mathf.CeilToInt((hi.x+.5f)/Step));x++)
                    {
                        int n=z*N+x;var p=Point(n);var nearest=a+delta*(length<1e-10f ? 0 : Mathf.Clamp01(Vector2.Dot(p-a,delta)/length));
                        if((p-nearest).sqrMagnitude<=radius*radius)blocked[n]=true;
                    }
            }
        }
        internal bool TryRoute(Vector2 start,Vector2 end,out Vector2[] route)
        {
            int first=Cell(start),last=Cell(end);route=null;
            if(blocked[first]||blocked[last])return false;
            var cost=new float[N*N];var parent=new int[N*N];var heading=new int[N*N];var closed=new bool[N*N];
            for(int i=0;i<cost.Length;i++){cost[i]=float.PositiveInfinity;parent[i]=-1;heading[i]=-1;}
            var heap=new Heap();cost[first]=0;heap.Add(first,Estimate(first,last));
            while(heap.Count>0)
            {
                int n=heap.Pop();if(closed[n])continue;closed[n]=true;
                if(n==last)
                {
                    var raw=new List<Vector2>();for(int k=last;k!=-1;k=parent[k])raw.Add(Point(k));raw.Reverse();
                    var clean=new List<Vector2>{start,raw[0]};
                    for(int i=1;i<raw.Count-1;i++)if(Mathf.Abs(Cross(raw[i]-raw[i-1],raw[i+1]-raw[i]))>1e-7f)clean.Add(raw[i]);
                    clean.Add(raw[raw.Count-1]);clean.Add(end);
                    for(int i=clean.Count-1;i>0;i--)if((clean[i]-clean[i-1]).sqrMagnitude<1e-10f)clean.RemoveAt(i);
                    route=clean.ToArray();return true;
                }
                int x=n%N,z=n/N;
                for(int d=0;d<8;d++)
                {
                    int nx=x+DX[d],nz=z+DZ[d];if(nx<2||nz<2||nx>=N-2||nz>=N-2)continue;
                    int next=nz*N+nx;if(blocked[next]||closed[next])continue;
                    if(DX[d]!=0&&DZ[d]!=0&&(blocked[z*N+nx]||blocked[nz*N+x]))continue;
                    float candidate=cost[n]+(d%2==0 ? 1 : 1.414214f)+(heading[n]>=0&&heading[n]!=d ? .8f : 0);
                    if(candidate>=cost[next])continue;
                    cost[next]=candidate;parent[next]=n;heading[next]=d;heap.Add(next,candidate+Estimate(next,last));
                }
            }
            return false;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float Estimate(int a,int b){int x=Math.Abs(a%N-b%N),z=Math.Abs(a/N-b/N);return Math.Max(x,z)+.414214f*Math.Min(x,z);}
        sealed class Heap
        {
            readonly List<(int node,float score)> values=new List<(int,float)>();
            internal int Count=>values.Count;
            internal void Add(int node,float score)
            {
                values.Add((node,score));int i=values.Count-1;
                while(i>0){int p=(i-1)/2;if(values[p].score<=score)break;values[i]=values[p];i=p;}values[i]=(node,score);
            }
            internal int Pop()
            {
                int result=values[0].node;var item=values[values.Count-1];values.RemoveAt(values.Count-1);if(values.Count==0)return result;
                int i=0;while(i*2+1<values.Count){int c=i*2+1;if(c+1<values.Count&&values[c+1].score<values[c].score)c++;if(values[c].score>=item.score)break;values[i]=values[c];i=c;}values[i]=item;return result;
            }
        }
    }
}
