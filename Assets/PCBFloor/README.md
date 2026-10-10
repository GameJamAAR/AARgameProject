# 3D PCB floor

Select **PCB Floor** in ShaderTest and use **Generate PCB Floor** in the Inspector. Generation is explicit, so changing parameters does not rebuild hundreds of objects while you drag a slider. **New Seed & Generate** explores a new layout. Undo is supported in the editor. At runtime call `Generate()` or enable `generateOnStart` (leave it off for baked scene output).

The board remains white and receives dithered shadows. Three-rail copper buses, plated vias, connected IC footprints, SMD arrays, headers, package stencils and contacts are real meshes using AAR/Retro Dither. No decals or flat circuit textures. Five shared materials expose colors, palette and dithering; generator overrides allow another look without changing the kit. The CRT remains a separate camera effect. RetroScreenDitherGrid compensates its warp so the PCB's Screen-mode Bayer cells stay regular on the final display. Meshes also have noncollapsed face UVs and unwrapped cylindrical walls for the optional MeshUV mode.

## Current visual preset

The test scene uses grayscale controller modules at 10.8 world units per tile (six times the previous 1.8 spacing), on a 3 × 5 board measuring 32.4 × 54 units. Package and trace relief scale up too. QFP-style controller packages have four seven-pin banks; each external three-rail bundle runs directly into three actual lands. Twelve default variants vary package aspect ratio, offset and surrounding component placement. Starter Variant Count (6–16), Module Variation Seed, Chip Position Variation and Package Size Variation control the reusable kit. Support components are placed around reserved copper and package footprints. Cylindrical electrolytic capacitors have metal lids and vent crosses; resistor banks have raised bands and soldered terminals. Short and long through-hole headers complete the modules. Copper width is .010 normalized units in the current preset (about 67% thicker than the previous .006). Local signals join actual IC lands to component pads on one visible copper layer. An octilinear router reserves package keepouts and track clearance; blocked connections are omitted instead of crossing existing copper. Decorative floating bundles have been removed. No green traces or gold contacts in this preset. All five material palettes are neutral grayscale, with 65% dithering with two-pixel cells measured in the final camera output.

Controller Modules and Controller Footprint in the library select/rebuild this kit. Disable Controller Modules and rebuild to use the older local circuit recipes. Rebuild then regenerate. All shape families retain the same three-rail seam profile, regardless of package style.

## Structural routing modes

**Shared Sockets** is an optional mode. The dense component board stays intact. Neighboring tiles share one hashed edge profile: three exact rail positions, the same width and relief, at a variable position along the edge. Interior junctions vary independently and the copper mesh connects those profiles through 45-degree and orthogonal segments. This changes the network geometry while retaining exact physical matches. Socket Variation and Junction Variation control these offsets. Trace Coverage, Loop Chance and Straight Bias still shape the connected tile graph.

**Matched Tiles** is the default and retains authored pin routes with centered connectors. The same library, materials and relief controls work in both modes. Shared Sockets uses blank component modules with a direct Network Port marker at a conductive land. It keeps packages, footprints and contacts together. It is a visual multilayer PCB system, not an electrical netlist or package obstacle-routing simulator.

The editor saves its combined route mesh in GeneratedRouting; regeneration updates it with Undo support. Runtime output owns and disposes a separate mesh. Duplicating a generator with an existing saved mesh copies the reference too; clear the hidden macroMesh field in the Debug Inspector before generating an independent duplicate.

## Parameters

- Columns / rows (1–64), seed, world tile size.
- Trace coverage: fraction of tiles reached by the main network. A spanning tree connects the active region; loop chance adds alternate paths, straight bias favors longer runs. Outer ports are always closed. A one-cell region has no external trace.
- Board thickness, trace relief, component density and component height. The collider follows the board surface, not the decorative components.
- Optional board, trace, body and contact material overrides. Null uses the prefab's material. No per-tile material copies.
- In the starter library: normalized track width, bus spacing, via radius and chip footprint, plus Starter Variant Count, Module Variation Seed, Chip Position Variation, Package Size Variation, Local Trace Density (1–3, up to 7/14/21 component connections per tile when there are enough free pins and clearance), Capacitor Count (0–5) and Resistor Count (1–8). Component recipes adjust the requested counts within these bounds. **Rebuild Starter Geometry**, then regenerate the floor, to apply these geometry settings. Rebuild preserves mesh and prefab GUIDs and material edits, but replaces the starter entry list.

## Tile contract

The default controller kit supplies 72 prefabs covering six shapes with twelve component variants each (the legacy kit supplies 18): blank, end, straight, chamfered corner, tee and cross. The library is data-driven and also accepts hand-authored prefabs with weighted variants.

Author at 1 × 1, centered at the origin, X/Z footprint `[-0.5,0.5]`, top surface Y=0. Socket bits: North (+Z)=1, East (+X)=2, South=4, West=8. A port is a symmetric three-rail bundle at transverse offsets `-busSpacing, 0, +busSpacing`. Each rail reaches the boundary exactly. All kit ports must share identical rail widths, spacing, bottom/top height and material. The center rail carries the connected layout graph; companion signals use local plated via terminations at junctions. This is a visual PCB system, not an electrical netlist simulator. Do not bevel, inset or add arbitrary offsets at the boundary. Quarter turns are applied around +Y. End caps close the outer boundary automatically.

Named direct children `Surface`, `Traces`, `Components`, `Pins`, `Pads`, `Markings` receive overrides. Pads stay visible when packages are disabled; Markings use the legend material and remain on the board. Surface mesh thickness is .05 normalized units below Y=0. Traces begin at Y=0. Components and Pins can be disabled together. Put decorative routes entirely inside the tile; their pads are local ornaments, separate from the continuous main trace graph. Keep relief groups' origins at zero. All meshes include normals and UVs and support screen/UV dithering.

Interior variants offset junctions, stagger headers, change bend lengths and add doglegs; only the edge rail profiles stay fixed. Lower Loop Chance reduces rectangular closed circuits.

The graph is solved before prefab selection. Neighboring cells cannot independently choose incompatible sockets. A preflight rejects an incomplete kit before clearing the previous result. Only the serialized generated child is replaced; other scene objects stay intact. Layout/material references and prefab instances persist in the scene; meshes are saved assets, usable in builds.

Run **Tools → AAR → PCB → Validate System** to exercise repeatability, graph continuity, boundary closure, library coverage, physical mesh seams, and deterministic 45-degree copper routing around keepouts. Rotated seam tests use the current kit width and relief geometry. The default floor uses one collider and six renderers per tile with shared meshes/materials. For very large floors, consider chunk mesh combining after generation; the limit is not a performance guarantee.

## Baked floor surfaces

Enable **Bake Surface** on PCBFloorGenerator (enabled by default). Generation first constructs the complete selected layout and routing, then rasterizes raised traces, solder pads and silkscreen into raw RGB tangent normals plus an RGB material mask. It removes those generated mesh groups, keeping the board surfaces, component bodies and pins in 3D. A normal map preserves lighting relief but intentionally removes the shallow trace silhouettes and their geometry shadows.

**Surface Resolution** is pixels per tile (128–1024, rounded to a power of two; default 512). **Normal Strength** controls the relief. Trace Relief and Tile Size are included in the bake, so changing parameters and regenerating updates the maps. Texture mips and anisotropic filtering resolve distant tracks; the existing screen-space Bayer dithering and palette wave remain active. Contact brightness/specular and board/shadow colors are copied from the current shared overrides. Generated editor assets live in `Assets/PCBFloor/Baked/<generator id>` and are updated in place. Runtime regeneration creates its own textures/materials and disposes them with the generated output; it does not modify editor assets. Increasing resolution costs texture memory; no FPS improvement is implied solely by lower polygon counts.

Both Matched Tiles and Shared Sockets are supported. Shared Socket global routes are baked after routing and clipped into each tile; matched variants share one bake within a generation. Rotation is handled by tile UVs and local board tangent axes. Use **Generate PCB Floor** after changing the seed or kit; **Bake Surface** off restores the authored geometry. Source tile prefabs remain unchanged by the bake.

## ComputerPackv2 components

`Assets/Imported/ComputerPackv2` contains the source FBX and five normalized mesh/prefab variants: two capacitors, two small components and a RAM module. The starter library exposes Imported Capacitors, Imported Passives and Imported Modules, plus **Imported Component Variation** and **Imported Height Scale**. Rebuild Starter Geometry applies these choices deterministically to existing reserved package positions and preserves their trace lands and keepouts; then Generate refreshes the floor. Capacitors and passives replace their matching procedural packages. RAM occurs in a subset of controller recipes. Footprints are fitted inside the reserved placements, and body/contact materials use the existing grayscale dither palette.

On the current 6×5 test floor, full generated geometry with these imported variants has 538,464 triangles. Surface baking at 512 px removes 320,244 surface triangles, leaving 218,220 (59.5% fewer). Components account for most of the remaining geometry.
