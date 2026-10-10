using System;
using System.Collections.Generic;
using UnityEngine;
namespace AAR.PCB
{
    // Runtime bakes belong to this generated output. Editor assets are not registered here.
    public sealed class PCBBakeResources : MonoBehaviour
    {
        [NonSerialized] public readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        void OnDestroy(){foreach(var resource in owned)if(resource)Destroy(resource);}
    }
}
