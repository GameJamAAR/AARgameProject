using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AAR.RetroScreen
{
    /// <summary>Prewarps the Bayer coordinates so the CRT produces a regular output pixel grid.</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class RetroScreenDitherGrid : MonoBehaviour
    {
        public FullScreenPassRendererFeature screenFeature;
        static readonly int WarpId = Shader.PropertyToID("_RetroScreenDitherWarp");
        static readonly int OutputSizeId = Shader.PropertyToID("_RetroScreenDitherOutputSize");
        static readonly Dictionary<Camera, RetroScreenDitherGrid> Cameras = new Dictionary<Camera, RetroScreenDitherGrid>();
        Camera owner;
        void OnEnable()
        {
            owner = GetComponent<Camera>();
            if (Cameras.Count == 0) RenderPipelineManager.beginCameraRendering += PrepareCamera;
            Cameras[owner] = this;
        }
        void OnDisable()
        {
            if (owner) Cameras.Remove(owner);
            if (Cameras.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= PrepareCamera;
                Shader.SetGlobalVector(WarpId, Vector4.zero);
                Shader.SetGlobalVector(OutputSizeId, Vector4.zero);
            }
        }
        static void PrepareCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Cameras.TryGetValue(camera, out var grid) && camera.cameraType == CameraType.SceneView)
            {
                var main = Camera.main;
                if (main) Cameras.TryGetValue(main, out grid);
            }
            // Camera pixel dimensions describe the final target, independent of URP
            // render scale. Source fragment positions use the scaled internal buffer.
            Shader.SetGlobalVector(OutputSizeId, new Vector4(camera.pixelWidth, camera.pixelHeight, 0, 0));
            var feature = grid ? grid.screenFeature : null;
            var material = feature ? feature.passMaterial : null;
            // Partial blends contain both warped and unwarped images; one lattice
            // cannot cancel both mappings. Leave the original grid during that transition.
            bool active = feature && feature.isActive && material && material.GetFloat("_Intensity") >= .999f;
            Shader.SetGlobalVector(WarpId, active
                ? new Vector4(material.GetFloat("_CurvatureX"), material.GetFloat("_CurvatureY"), material.GetFloat("_Zoom"), 1)
                : Vector4.zero);
        }
    }
}
