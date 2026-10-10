# Shader Lab

The ShaderTest scene includes a reusable Windows 98 style debug window. Tiny5 is baked as a point-filtered bitmap atlas at 16 px, and the overlay uses integer scaling to keep its glyphs and bevels crisp. Font source: https://github.com/google/fonts/tree/8b0a1d0f59/ofl/tiny5. The accompanying OFL license is in Fonts/OFL.txt.

Four buttons select Original, Ambre, Menthe and Violet. Original uses each material's existing palette. The other states override the four palette colors without changing Bayer size, screen pixel spacing, strength, lighting or CRT parameters. Only materials using AAR/Retro Dither participate. The UI remains readable above the CRT effect.

On Shader Debug UI / ShaderWaveController, edit the four colors of each look, Character, Duration, Softness, Minimum Radius and Expansion. A switch captures the character's current position and expands on the world XZ plane. The front follows a Gaussian cumulative distribution (Softness is its standard deviation in world units); Expansion controls the radial animation. The calculated reach covers affected renderer bounds. Rapid clicks queue the latest choice until the current transition completes, preventing a visual jump. Disabling the controller restores ordinary material rendering.

Prefab: Prefabs/Shader Debug UI.prefab. Assign a scene character when using it elsewhere. There should be one active ShaderWaveController per scene. Reusable Win98Bevel graphics expose their face color and inset style and handle mouse/focus/pressed states. The debug panel has no gauge.

Tools > AAR > Install Shader Debug UI installs the UI into a scene without replacing an existing window. The event system uses the project's Input System module.
