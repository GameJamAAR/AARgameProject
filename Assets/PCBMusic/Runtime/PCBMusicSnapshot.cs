using System;
using UnityEngine;
using AAR.PCB;

namespace AAR.PCBMusic
{
    [Serializable]
    public struct MusicPart
    {
        public int id, group;
        public MusicRole role;
        public Vector3 center, size;
    }
    [Serializable]
    public struct MusicLink { public int a,b; public bool topLayer; }

    // Kept on the generated root, independently of merged meshes and baked copper.
    [DisallowMultipleComponent]
    public sealed class PCBMusicSnapshot : MonoBehaviour
    {
        public int version = 1, boardSeed;
        public MusicPart[] parts = Array.Empty<MusicPart>();
        public MusicLink[] links = Array.Empty<MusicLink>();
        public static event Action Changed;
        public static PCBMusicSnapshot Capture(Transform root, PCBGlobalBoard.Plan plan, PCBFloorGenerator floor)
        {
            var snapshot=root.GetComponent<PCBMusicSnapshot>();
            if(!snapshot) snapshot=root.gameObject.AddComponent<PCBMusicSnapshot>();
            snapshot.boardSeed=floor.seed;
            snapshot.parts=new MusicPart[plan.parts.Count];
            for(int i=0;i<plan.parts.Count;i++)
            {
                var p=plan.parts[i];float mount=floor.bakeSurface?0:.014f*floor.traceRelief;
                snapshot.parts[i]=new MusicPart {id=i, group=p.group,
                    role=p.family==0?MusicRole.Chip:p.family==1?MusicRole.Capacitor:p.family==2?MusicRole.RAM:MusicRole.Passive,
                    center=new Vector3(p.position.x,mount+(p.family==2?.1f:0)+p.height*.5f,p.position.y),
                    size=new Vector3(p.size.x,Mathf.Max(.02f,p.height),p.size.y)};
            }
            snapshot.links=new MusicLink[plan.links.Count];
            for(int i=0;i<snapshot.links.Length;i++) snapshot.links[i]=new MusicLink{a=plan.links[i].a,b=plan.links[i].b,topLayer=plan.links[i].layer==0};
            Changed?.Invoke();return snapshot;
        }
        void OnEnable(){Changed?.Invoke();}
        void OnDisable(){Changed?.Invoke();}
    }
}
