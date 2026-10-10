using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AAR.PCB
{
    public static class PCBMacroRouting
    {
        // The same hashed edge defines both neighboring connector faces.
        public static List<Vector2[]> SharedRoutes(int[] masks,int width,int depth,float size,int seed,float socketVariation,float junctionVariation)
        {
            var result=new List<Vector2[]>();
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {
                int cell=z*width+x;
                var center=new Vector2((x-(width-1)*.5f)*size,(z-(depth-1)*.5f)*size);
                var random=new System.Random(seed ^ (cell*73856093));
                var junction=center+new Vector2((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1)*size*junctionVariation;
                var ports=new List<Vector2>();var leads=new List<Vector2>();
                for(int d=0;d<4;d++)if((masks[cell]&(1<<d))!=0)
                {
                    var dir=PCBLayout.Directions[d];
                    int neighbor=cell+dir.x+dir.y*width;
                    int a=Math.Min(cell,neighbor),b=Math.Max(cell,neighbor);
                    var edgeRandom=new System.Random(unchecked(seed ^ a*73856093 ^ b*19349663));
                    float offset=((float)edgeRandom.NextDouble()*2-1)*socketVariation*size;
                    var tangent=dir.x==0 ? Vector2.right : Vector2.up;
                    var forward=new Vector2(dir.x,dir.y);
                    var p=center+forward*(size*.5f)+tangent*offset;
                    ports.Add(p);leads.Add(p-forward*(size*.18f));
                }
                if(ports.Count==2)
                {
                    var route=new List<Vector2>{ports[0],leads[0]};
                    AppendOctilinear(route,leads[0],junction);AppendOctilinear(route,junction,leads[1]);route.Add(ports[1]);result.Add(route.ToArray());
                }
                else for(int i=0;i<ports.Count;i++)
                {
                    var route=new List<Vector2>{ports[i],leads[i]};
                    AppendOctilinear(route,leads[i],junction);result.Add(route.ToArray());
                }
            }
            return result;
        }

        public static Vector2[] FeedRoute(Vector2 start,Vector2 end)
        {
            var route=new List<Vector2>{start};AppendOctilinear(route,start,end);return route.ToArray();
        }

        static void AppendOctilinear(List<Vector2> route,Vector2 a,Vector2 b)
        {
            var delta=b-a;
            float diagonal=Mathf.Min(Mathf.Abs(delta.x),Mathf.Abs(delta.y));
            var direction=new Vector2(Mathf.Sign(delta.x),Mathf.Sign(delta.y));
            var p=a+direction*(diagonal*.5f);var q=b-direction*(diagonal*.5f);
            foreach(var point in new[]{p,q,b})if((point-route[route.Count-1]).sqrMagnitude>1e-14f)route.Add(point);
        }

        public static Mesh Build(IReadOnlyList<Vector2[]> routes,float width,float spacing,float height,int laneCount=3,bool taperEnds=false)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            foreach(var route in routes)
                for(int laneIndex=0;laneIndex<Mathf.Max(1,laneCount);laneIndex++)
                {
                    float lane=laneIndex-(Mathf.Max(1,laneCount)-1)*.5f;
                    var shifted=new Vector2[route.Length];
                    // Join offsets using a miter so all rails remain continuous at 45° bends.
                    for(int i=0;i<route.Length;i++)
                    {
                        if(i==0||i==route.Length-1)
                        {
                            if(taperEnds){shifted[i]=route[i];continue;}
                            var direction=(i==0 ? route[1]-route[0] : route[i]-route[i-1]).normalized;
                            shifted[i]=route[i]+new Vector2(-direction.y,direction.x)*(lane*spacing);
                            continue;
                        }
                        var incoming=(route[i]-route[i-1]).normalized;
                        var outgoing=(route[i+1]-route[i]).normalized;
                        if(incoming.sqrMagnitude<.01f)incoming=outgoing;
                        if(outgoing.sqrMagnitude<.01f)outgoing=incoming;
                        var n0=new Vector2(-incoming.y,incoming.x);var n1=new Vector2(-outgoing.y,outgoing.x);
                        var miter=(n0+n1).normalized;
                        float denominator=Mathf.Max(.45f,Mathf.Abs(Vector2.Dot(miter,n1)));
                        shifted[i]=route[i]+miter*(lane*spacing/denominator);
                    }
                    for(int i=1;i<shifted.Length;i++)
                    {
                        var delta=shifted[i]-shifted[i-1];if(delta.sqrMagnitude<1e-12f)continue;
                        var midpoint=(shifted[i]+shifted[i-1])*.5f;
                        Box(new Vector3(midpoint.x,height*.5f,midpoint.y),new Vector3(width,height,delta.magnitude),Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);
                        // Small covered joint; output stays actual raised geometry.
                        if(i<shifted.Length-1)Box(new Vector3(shifted[i].x,height*.5f,shifted[i].y),new Vector3(width,height,width),0);
                    }
                }
            var mesh=new Mesh {name="PCB Continuous Routing",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x,p.z));triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            void Box(Vector3 center,Vector3 size,float angle)
            {
                var p=new Vector3[8];var rotation=Quaternion.Euler(0,angle,0);
                for(int i=0;i<8;i++)p[i]=center+rotation*Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),size);
                Quad(p[2],p[6],p[7],p[3]);Quad(p[0],p[1],p[5],p[4]);Quad(p[0],p[2],p[3],p[1]);Quad(p[4],p[5],p[7],p[6]);Quad(p[0],p[4],p[6],p[2]);Quad(p[1],p[3],p[7],p[5]);
            }
        }
    }
}
