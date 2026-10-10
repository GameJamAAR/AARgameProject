using System;
using UnityEngine;

namespace AAR.PCB
{
    // Runtime meshes belong to one generated output; saved editor assets are never assigned here.
    public sealed class PCBTransientMesh : MonoBehaviour
    {
        [NonSerialized] public Mesh mesh;
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
