# Retro dithering challenge

Open `Assets/Scenes/ShaderTest.unity` and select
`Assets/ShaderTests/ShaderTest.mat`. It uses **AAR/Retro Dither** on the cube,
sphere, and triangle. The ground has a separate material using the same shader.

## Art controls

- **Palette:** four editable colors, ordered by tone from ink to highlight.
  Nightdrive, Handheld, Amber, and Glacier buttons change only the colors.
- **Pattern:** 2x2, 4x4, or 8x8 Bayer matrix, strength, and pattern pixel size.
  Strength zero gives hard color bands. One gives full ordered dithering.
- **Coordinates:** Screen anchors the pattern to rendered pixels; MeshUV anchors it
  to UV0. UV Density affects MeshUV only. Pixel Size scales the pattern rather than
  lowering render resolution. UV seams and stretching also affect UV patterns.
- **Tone:** exposure, contrast, brightness, ambient floor, and wrapped diffuse light.
- **Highlights:** separate specular and rim strengths/exponents.
- **Texture:** optional tone texture and tint. Their luminance drives the palette;
  texture hue does not introduce extra output colors.

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
  and depth passes. The same material buffer is shared across passes.
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
