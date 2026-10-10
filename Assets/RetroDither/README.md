# Retro dithering challenge

Open `Assets/Scenes/ShaderTest.unity` and select
`Assets/ShaderTests/ShaderTest.mat`. It uses **AAR/Retro Dither** on the cube,
sphere, and triangle. The ground has a separate material using the same shader.

## Art controls

The scene's **Ground** material enables **Dither Shadows Only**: the lit ground is
solid white, while the main light's realtime shadows use the Bayer pattern. Lit Ground
Color, Ground Shadow Color, and Shadow Density control this independently of object
materials. It ignores diffuse/specular texture shading and affects main-light shadows
only. CRT effects can still modify the final screen color.

- **Palette:** four editable colors. With **Sort & Deduplicate Palette** enabled,
  colors are ordered by brightness and duplicates are merged into a true 1–4 color ramp.
  Disable normalization to use the authored slot order.
  Nightdrive, Handheld, Amber, and Glacier buttons change only the colors.
- **Pattern:** 2x2, 4x4, or 8x8 Bayer matrix, strength, and pattern pixel size.
  Strength zero gives hard color bands. One gives full ordered dithering.
  Pattern Phase shifts the Bayer matrix in whole cells (X/Y); different phases can
  separate objects from their background without changing colors or pattern density.
- **Coordinates:** Screen anchors the pattern to a reference-height pixel grid; MeshUV anchors it
  to UV0. UV Density affects MeshUV only. Pixel Size scales the pattern rather than
  lowering render resolution. UV seams and stretching also affect UV patterns.
  Reference Height scales the requested pattern size between resolutions; screen cells are rounded to whole rendered pixels so the Bayer lattice has equal-width cells. Small changes of resolution can change the physical cell size by one pixel instead of introducing uneven rows.
- **Preserve Crisp Dither (default):** keeps Screen mode on exact palette entries with
  binary Bayer thresholds, including at distance. It prevents the pattern fading into gray.
- **Cell Lighting Stability:** reconstructs lighting toward each cell center on gradual
  gradients. Sharp changes are protected, and corrections are bounded to preserve detail.
- **Detail Filtering:** derivative-aware smoothing in the softer mode, and rim stabilization.
- **Specular Antialiasing:** broadens and attenuates subpixel specular highlights;
  normal variation also reduces unstable rim highlights.
- **Distance Smoothing:** in crisp Screen mode, increases cell-lighting stabilization
  between Fade Start and Fade End (world units). In soft mode it averages the pattern.
  UV cells below a pixel are filtered automatically. This is spatial filtering, not TAA.
- **Tone:** exposure, contrast, brightness, ambient floor, and wrapped diffuse light.
- **Highlights:** separate specular and rim strengths/exponents.
- **Texture:** optional tone texture and tint. Their luminance drives the palette;
  texture hue does not introduce extra palette colors. Filtering blends neighboring
  palette entries, so small/distant details may contain intermediate colors.

For a strict discrete palette, enable Preserve Crisp Dither and use Screen coordinates.
Subpixel UV filtering still uses the average to avoid aliasing; use Screen coordinates
and sufficiently large cells for strict palette output. Preserve deliberately hard
mesh edges: the shader filters lighting without recalculating model normals.
CRT Warp Minification Filtering prevents additional aliasing when curvature compresses
the finished image. Disable it only when exact nearest-pixel sampling is required.

Duplicate a material for independent settings, or share it to keep objects cohesive.
No camera component, renderer feature, runtime script, or global shader setting is needed.
Controls can also be changed with `Material.SetFloat` / `SetColor`, or per renderer
with `MaterialPropertyBlock` (property names are listed in the shader). Property blocks
can prevent SRP batching; use shared materials when per-object overrides aren't needed.

## Reusable structure

- `Shaders/RetroDitherCore.hlsl`: pure Bayer and quantization functions, with
  `RetroPalette` and `RetroDitherSettings` structs. Include it from another HLSL
  shader and call `RetroQuantize` with a 0–1 tone and pixel coordinates.
- `Shaders/RetroDitherInput.hlsl`: material properties and struct construction.
- `Shaders/RetroDither.shader`: URP geometry, realtime lighting, shadow casting,
  depth, and depth-normal passes. The same material buffer is shared across passes.
- `Editor/RetroDitherShaderGUI.cs`: grouped controls and Undo-enabled palette presets.

The material is opaque and uses mesh normals, UV0, and realtime lights. Baked
lightmaps, normal maps, transparency, fog, and a fullscreen pixelation pass are outside
this challenge. Render effects such as antialiasing or tone mapping can blend palette
colors after this shader runs. Pattern pixel size follows the actual render resolution.

## Try these challenges

1. Make all three shapes readable using just two distinct colors in the four slots.
2. Build a four-color sunset palette without changing the shader code.
3. Compare the three matrix sizes at strength 0, 0.5, and 1.
4. Move a light around the sphere; tune the rim and specular independently.
5. Switch to MeshUV and compare surface anchoring, UV seams, and distortion.
6. Reuse `RetroQuantize` in another shader without copying its implementation.

Screen coordinates can be compensated by RetroScreenDitherGrid on the camera. In that configuration the Bayer lattice is evaluated in final display coordinates through the inverse CRT transform. Pattern phase, cell sizes, grayscale palette and tone mapping remain material controls. MeshUV mode remains independent of CRT coordinate compensation.

Screen Pattern Sizing selects OutputPixels (fixed cell size in final target pixels) or ReferenceHeight (scales the requested cell size relative to Reference Height). The PCB preset uses OutputPixels at size 2. RetroScreenDitherGrid supplies the camera's final pixel dimensions, separating output resolution from URP's internal render scale. Reference Height is ignored in OutputPixels mode; MeshUV density is unaffected.
