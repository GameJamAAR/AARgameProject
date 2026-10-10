using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AAR.RetroScreen
{
    /// <summary>Non-destructive, per-camera switches. Zero shader globals preserve the original look.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RetroEffectsController : MonoBehaviour
    {
        [Header("Level design")]
        [Tooltip("Editor only: bypass all retro effects and Volume post-processing in Scene and Game views, including Play mode. Builds keep their normal settings.")]
        public bool levelDesignMode;
        [Tooltip("Show the retro look in Scene view. Game view remains independent.")]
        public bool effectsInSceneView;
        public bool effectsInGameView = true;
        [Header("Individual effects")]
        public bool dither = true;
        [Tooltip("Turn off to see continuous base-material lighting instead of the retro palette.")]
        public bool paletteShader = true;
        public bool crt = true;
        public bool curvature = true;
        public bool screenBorder = true;
        public bool vignette = true;
        public bool scanlines = true;
        public bool colorSeparation = true;
        [Tooltip("Unity Volume effects, independently of the custom CRT shader.")]
        public bool volumePostProcessing = true;

        bool DesignBypass
        {
            get
            {
#if UNITY_EDITOR
                return levelDesignMode;
#else
                return false;
#endif
            }
        }
        static readonly int Suppression = Shader.PropertyToID("_RetroEffectsSuppression");
        static readonly int Geometry = Shader.PropertyToID("_RetroEffectsGeometry");
        static RetroEffectsController owner;
        readonly Dictionary<UniversalAdditionalCameraData, bool> cameraStates = new();
        public static bool CurvatureEnabled => Shader.GetGlobalVector(Suppression).y < .5f && Shader.GetGlobalVector(Geometry).x < .5f;

        void OnEnable()
        {
            if (owner && owner != this) { Debug.LogWarning("Only one Retro Effects controller can be active.", this); enabled = false; return; }
            owner = this;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        void OnDisable()
        {
            if (owner != this) return;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            foreach (var state in cameraStates) if (state.Key) state.Key.renderPostProcessing = state.Value;
            cameraStates.Clear();
            owner = null;
            Shader.SetGlobalVector(Suppression, Vector4.zero);
            Shader.SetGlobalVector(Geometry, Vector4.zero);
        }
        public static void PrepareCamera(Camera camera)
        {
            if (!owner) return;
            bool bypass = owner.DesignBypass || (camera.cameraType == CameraType.SceneView ? !owner.effectsInSceneView : !owner.effectsInGameView);
            Shader.SetGlobalVector(Suppression, new Vector4(bypass || !owner.dither ? 1 : 0,
                bypass || !owner.crt ? 1 : 0, bypass || !owner.vignette ? 1 : 0, bypass || !owner.scanlines ? 1 : 0));
            Shader.SetGlobalVector(Geometry, new Vector4(bypass || !owner.curvature ? 1 : 0,
                bypass || !owner.screenBorder ? 1 : 0, bypass || !owner.colorSeparation ? 1 : 0, bypass || !owner.paletteShader ? 1 : 0));
        }
        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            PrepareCamera(camera);
            bool bypass = DesignBypass || (camera.cameraType == CameraType.SceneView ? !effectsInSceneView : !effectsInGameView);
            if ((bypass || !volumePostProcessing) && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                cameraStates.TryAdd(data, data.renderPostProcessing);
                data.renderPostProcessing = false;
            }
        }
        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && cameraStates.TryGetValue(data, out bool previous))
            {
                data.renderPostProcessing = previous;
                cameraStates.Remove(data);
            }
        }
    }
}
