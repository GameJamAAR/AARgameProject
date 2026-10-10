using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCB
{
    // Joined FBX meshes retain disconnected metal legs. Weld the face duplicates,
    // then identify islands touching the mounting plane instead of guessing a pin count.
    internal static class PCBGlobalContacts
    {
        internal struct Contact { public Vector2 point, direction; public float width; }
        internal static List<Contact> Contacts(GameObject prefab, Vector2 center, Vector3 fittedSize)
        {
            var result=new List<Contact>();
            var filter=prefab.GetComponent<MeshFilter>();
            if(!filter||!filter.sharedMesh)return result;
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var bounds=mesh.bounds;
            float scale=fittedSize.x/bounds.size.x;
            var parent=new int[vertices.Length];for(int i=0;i<parent.Length;i++)parent[i]=i;
            int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){parent[Root(a)]=Root(b);}
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {var key=Vector3Int.RoundToInt(vertices[i]*100000);if(welded.TryGetValue(key,out int other))Join(i,other);else welded[key]=i;}
            var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var feet=new Dictionary<int,Bounds>();
            for(int i=0;i<vertices.Length;i++)
            {
                var v=vertices[i];if(v.y>bounds.min.y+.001f)continue;
                int key=Root(i);if(!feet.TryGetValue(key,out var foot))foot=new Bounds(v,Vector3.zero);
                foot.Encapsulate(v);feet[key]=foot;
            }
            foreach(var foot in feet.Values)
            {
                var p=foot.center-bounds.center;
                bool sideX=Mathf.Abs(p.x/bounds.extents.x)>Mathf.Abs(p.z/bounds.extents.z);
                var direction=sideX ? new Vector2(Mathf.Sign(p.x),0) : new Vector2(0,Mathf.Sign(p.z));
                result.Add(new Contact {point=center+new Vector2(p.x,p.z)*scale,direction=direction,
                    width=(sideX ? foot.size.z : foot.size.x)*scale});
            }
            result.Sort((a,b)=>a.direction.x!=b.direction.x ? a.direction.x.CompareTo(b.direction.x)
                : a.direction.y!=b.direction.y ? a.direction.y.CompareTo(b.direction.y)
                : (a.direction.x!=0 ? a.point.y.CompareTo(b.point.y) : a.point.x.CompareTo(b.point.x)));
            return result;
        }
    }
}
