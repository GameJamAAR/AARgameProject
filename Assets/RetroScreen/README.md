# Retro curved screen

Select `Assets/RetroScreen/RetroScreen.mat` to edit the effect live in Game view.
The scene's regular materials, including Retro Dither, feed into this screen effect.

## Parameters

- **Effect Blend:** 0 is the original image; 1 applies the full screen treatment.
- **Horizontal / Vertical Curvature:** radial barrel distortion of the entire sampled
  image in each axis, including scene objects and camera-rendered UI.
- **Zoom:** crop in or pull back to balance the curved border and image framing.
- **Corner Radius / Border Width / Border Color:** shape and color of the screen bezel.
- **Edge Feather:** soften the boundary in rendered pixels.
- **Vignette:** strength and softness of darkened corners.
- **Scanlines:** strength, requested line count, and dark line width. They curve with
  the image. Density is capped globally based on rendered height, curvature, and zoom,
  so even high requested counts stay visible without moire. Zero strength disables them.
- **Color Separation:** optional red/blue offset in rendered pixels. Zero disables it.
- **Crisp Pixel Sampling:** nearest sampling preserves dither dots; disable for smoother filtering.

To retain exact scene palette colors, set vignette, scanlines, and color separation
to zero and use crisp sampling. The bezel and feather can still introduce border colors.
The effect does not imitate or copy Balatro assets; it provides a similar CRT screen shape.

## Structure and reuse

- `Shaders/RetroScreenCore.hlsl` contains the reusable settings struct, inverse UV warp,
  and rounded screen mask, independent of URP texture sampling.
- `Shaders/RetroScreen.shader` samples URP's color buffer and applies the treatment.
- `Editor/RetroScreenShaderGUI.cs` groups the material controls.
- `Editor/RetroScreenSetup.cs` installs the built-in URP Full Screen Pass feature.
  Run **Tools > AAR > Retro Screen > Install on project renderers** to reinstall;
  it updates existing features without duplicating them or resetting material values.

The PC and Mobile renderers share the material. Their **Retro Screen** features run
after post-processing and fetch the current color buffer, with no depth/normal request.
Unity's built-in feature handles Render Graph and the intermediate color copy.
Disable the feature checkbox to avoid the pass and copy cost entirely. Effect Blend = 0
removes the visual treatment but still executes the feature.

This is a renderer-wide effect; cameras using the renderer share its settings.
It also affects Scene view. Screen Space Overlay UI renders afterward and stays straight;
use Screen Space Camera or World Space canvases to include UI in the curvature.
Warping the UI image does not change its hit-test coordinates, so interactive controls
may need matching pointer-coordinate conversion. Camera stacks should apply the effect
only once on the final composition; this setup targets the current single-camera scene.

Use material `SetFloat` / `SetColor` calls for animation or runtime controls. Clone and
assign the material for another renderer with independent settings. The material asset
reference keeps the shader included in builds.
