using System;
using UnityEngine;

namespace AAR.PCB
{
    [CreateAssetMenu(menuName = "AAR/PCB/Tile Library")]
    public sealed class PCBTileLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public GameObject prefab;
            [Tooltip("Bits: North (+Z)=1, East (+X)=2, South=4, West=8. Ports are centered on each edge.")]
            [Range(0, 15)] public int sockets;
            [Min(0.01f)] public float weight = 1;
        }
        [Header("Imported component variants (rebuild kit to apply)")]
        public GameObject[] importedCapacitors = Array.Empty<GameObject>();
        public GameObject[] importedPassives = Array.Empty<GameObject>();
        public GameObject[] importedModules = Array.Empty<GameObject>();
        [Range(0,1)] public float importedComponentVariation = .65f;
        [Range(.5f,2)] public float importedHeightScale = 1;
        [Tooltip("Footprint multiplier for the imported long chips, including their reserved placement and contacts.")]
        [Range(1,4)] public float importedPassiveScale = 2.2f;
        [Header("Starter kit geometry (rebuild kit to apply)")]
        public bool controllerModules = true;
        [Header("Central composition (rebuild kit to apply)")]
        [Tooltip("Relative weights: controller, capacitor bank, RAM bank, empty center.")]
        public Vector4 centralCompositionWeights = Vector4.one;
        [Range(.6f,1.2f)] public float centralCapacitorScale = 1;
        [Range(1,4)] public int centralRamCount = 3;
        [Range(.5f,2)] public float centralComponentHeight = 1;
        [Range(6,16)] public int starterVariantCount = 12;
        public int moduleVariationSeed = 1984;
        [Range(0,.16f)] public float chipPositionVariation = .13f;
        [Range(0,1)] public float packageSizeVariation = .8f;
        [Range(1,3)] public int localTraceDensity = 2;
        [Range(0,5)] public int capacitorCount = 3;
        [Range(1,8)] public int resistorCount = 5;
        [Range(.30f,.44f)] public float controllerFootprint = .36f;
        [Range(.004f, .018f)] public float traceWidth = .010f;
        [Range(.045f, .085f)] public float busSpacing = .055f;
        [Range(.02f, .04f)] public float padRadius = .028f;
        [Range(.12f, .28f)] public float chipFootprint = .22f;
        public Material surfaceMaterial, copperMaterial, bodyMaterial, contactMaterial, legendMaterial;
        public Entry[] tiles = Array.Empty<Entry>();
        [Tooltip("Normalized tile width. Keep all authored tiles at 1 x 1, top surface at Y=0.")]
        public string authoringContract = "1 x 1; centered edge ports; top Y=0; Surface, Traces, Components, Pins child groups";

        public bool TryPick(int sockets, System.Random random, out Entry chosen, out int turns)
        {
            float total = 0;
            foreach (var entry in tiles)
                if (entry != null && entry.prefab != null && entry.weight > 0)
                    for (int r = 0; r < 4; r++)
                        if (PCBLayout.Rotate(entry.sockets, r) == sockets) total += entry.weight;
            double pick = random.NextDouble() * total;
            foreach (var entry in tiles)
                if (entry != null && entry.prefab != null && entry.weight > 0)
                    for (int r = 0; r < 4; r++)
                        if (PCBLayout.Rotate(entry.sockets, r) == sockets && (pick -= entry.weight) < 0)
                        { chosen = entry; turns = r; return true; }
            chosen = null; turns = 0; return false;
        }
    }
}
