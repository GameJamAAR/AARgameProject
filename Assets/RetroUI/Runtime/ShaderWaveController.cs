using System;
using UnityEngine;

namespace AAR.RetroUI
{
    [Serializable]
    public class ShaderLook
    {
        public string label;
        public bool useMaterialPalette;
        public Color ink = new Color32(16,16,16,255);
        public Color shadow = new Color32(75,75,75,255);
        public Color light = new Color32(175,175,175,255);
        public Color highlight = Color.white;
        public Vector4 Parameters => new Vector4(useMaterialPalette ? 0 : 1,0,0,0);
    }

    // One scene owner publishes two looks. Materials and their palettes stay untouched.
    public sealed class ShaderWaveController : MonoBehaviour
    {
        public Transform character;
        public ShaderLook[] looks = DefaultLooks();
        public static ShaderLook[] DefaultLooks() => new[] {
            new ShaderLook { label = "Original", useMaterialPalette = true },
            new ShaderLook { label = "Ambre", ink = new Color32(25,14,9,255), shadow = new Color32(98,44,19,255), light = new Color32(211,125,49,255), highlight = new Color32(255,226,164,255) },
            new ShaderLook { label = "Menthe", ink = new Color32(10,26,24,255), shadow = new Color32(35,85,75,255), light = new Color32(106,173,141,255), highlight = new Color32(218,246,205,255) },
            new ShaderLook { label = "Violet", ink = new Color32(23,16,40,255), shadow = new Color32(81,49,107,255), light = new Color32(167,114,176,255), highlight = new Color32(245,218,239,255) }
        };
        [Min(.1f)] public float duration = 1.8f;
        [Tooltip("Gaussian front standard deviation in world units.")] [Min(.05f)] public float softness = 1.4f;
        [Min(1)] public float minimumRadius = 60;
        public AnimationCurve expansion = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public int Current { get; private set; }
        public int Target { get; private set; }
        public bool IsTransitioning { get; private set; }
        public float Progress { get; private set; }
        public event Action Changed;
        int queued = -1;
        float elapsed, radius;
        Vector3 origin;
        static readonly int From = Shader.PropertyToID("_RetroLookFrom");
        static readonly int To = Shader.PropertyToID("_RetroLookTo");
        static readonly int Wave = Shader.PropertyToID("_RetroLookWave");
        static readonly int Center = Shader.PropertyToID("_RetroLookCenter");

        void OnEnable()
        {
            Current = Target = 0; queued = -1; elapsed = Progress = 0;
            IsTransitioning = false; Publish();
        }
        void OnDisable() { Shader.SetGlobalVector(Wave, Vector4.zero); }
        public void Select(int index)
        {
            if (index < 0 || looks == null || index >= looks.Length) return;
            if (IsTransitioning) { queued = index; return; }
            if (index == Current) return;
            Target = index;
            origin = character ? character.position : transform.position;
            radius = minimumRadius;
            foreach (var renderer in FindObjectsByType<Renderer>())
            {
                bool affected = false;
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.shader.name == "AAR/Retro Dither") affected = true;
                if (!affected) continue;
                var b = renderer.bounds;
                // Conservative horizontal distance covering every corner of the bounds.
                var d = b.center - origin;
                radius = Mathf.Max(radius, new Vector2(Mathf.Abs(d.x) + b.extents.x,
                    Mathf.Abs(d.z) + b.extents.z).magnitude);
            }
            elapsed = Progress = 0;
            IsTransitioning = true;
            Publish(); Changed?.Invoke();
        }
        void Update()
        {
            if (!IsTransitioning) return;
            elapsed += Time.unscaledDeltaTime;
            Progress = Mathf.Clamp01(elapsed / Mathf.Max(duration, .1f));
            if (Progress >= 1)
            {
                Current = Target; IsTransitioning = false; Publish(); Changed?.Invoke();
                int next = queued; queued = -1;
                if (next >= 0) Select(next);
            }
            else Publish();
        }
        void Publish()
        {
            if (looks == null || looks.Length == 0) { Shader.SetGlobalVector(Wave, Vector4.zero); return; }
            Shader.SetGlobalVector(From, looks[Mathf.Clamp(Current, 0, looks.Length - 1)].Parameters);
            Shader.SetGlobalVector(To, looks[Mathf.Clamp(Target, 0, looks.Length - 1)].Parameters);
            PublishPalette("_RetroFrom", looks[Current]);
            PublishPalette("_RetroTo", looks[Target]);
            float sigma = Mathf.Max(.05f, softness);
            float travel = Mathf.Lerp(-4 * sigma, radius + 4 * sigma,
                Mathf.Clamp01(expansion.Evaluate(Progress)));
            Shader.SetGlobalVector(Center, origin);
            Shader.SetGlobalVector(Wave, new Vector4(travel, sigma, IsTransitioning ? 1 : 0, 1));
        }
        static void PublishPalette(string prefix, ShaderLook look)
        {
            Color Convert(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            Shader.SetGlobalVector(prefix+"Ink", Convert(look.ink));
            Shader.SetGlobalVector(prefix+"Shadow", Convert(look.shadow));
            Shader.SetGlobalVector(prefix+"Light", Convert(look.light));
            Shader.SetGlobalVector(prefix+"Highlight", Convert(look.highlight));
        }

    }
}
