# 3D PCB floor

Select **PCB Floor** in ShaderTest and use **Generate PCB Floor** in the Inspector. Generation is explicit, so changing parameters does not rebuild hundreds of objects while you drag a slider. **New Seed & Generate** explores a new layout. Undo is supported in the editor. At runtime call `Generate()` or enable `generateOnStart` (leave it off for baked scene output).

The board uses your existing material colors and receives dithered shadows. Copper, plated vias and contacts are first generated as geometry, then shallow details can be baked into normal/material maps. Components remain real 3D meshes using AAR/Retro Dither. Five shared materials expose colors, palette and dithering; generator overrides allow another look without changing the kit. The CRT remains a separate camera effect. RetroScreenDitherGrid compensates its warp so the PCB's Screen-mode Bayer cells stay regular on the final display. Meshes also have noncollapsed face UVs and unwrapped cylindrical walls for the optional MeshUV mode.

## Global composition (current default)

**Global Board** is active in ShaderTest. A best-candidate distribution covers the whole 64.8 × 54 board. Weighted recipes create controller, capacitor and RAM groups, while resistors and smaller parts fill remaining gaps. Placement reserves escape space on sides with contacts; non-contact sides use a smaller clearance so passive banks can sit close together without obstructing their terminals. RAM banks can have different orientations; each socket has separate contacts down its long sides.

RAM signal buses are routed first, one physical contact per signal. A connected forest of nearby pairs then joins the remaining components. Each copper layer reserves complete swept segments and enforces a hard minimum distance, including diagonals and escapes. Short, low-bend octilinear candidates are preferred; a heading-aware A* search supplies detours. When top routing is blocked, two visible vias and short top stubs connect a real underside route recorded in the plan. Crossings are permitted between layers, never between separate tracks on the same layer. Global generation no longer adds synthetic offset rails after routing.

The resulting geometry is divided into **render chunks only after planning**. Changing Global Chunk Size does not change component positions or route paths. Each chunk receives normal/material maps generated from the same continuous copper mesh; components remain independent 3D objects. Partial edge chunks cover the exact board dimensions. Materials, camera effects and character references are preserved.

- Global Board Size: physical board dimensions.
- Global Group Count / Group Spread: requested functional groups and their surrounding placement radius. Recipes vary the large anchors; empty recipes leave small passives in that area. All-empty weights still produce an empty board. Footprint rejection can reduce actual counts.
- Global Parts Per Group × Component Density: target population and local surrounding components; large anchors come from the group recipe.
- Global Component Size / Height Scale: horizontal size and vertical proportion of imported models. Capacitor Height Scale is independent and defaults to the restored 0.65 proportion. RAM Height Scale defaults to 1, retaining the imported module’s vertical aspect ratio independently from low-profile IC bodies.
- Global Capacitors Per Group / Resistor Bank Size: mixed-size capacitor clusters and compact, consistently oriented passive banks.
- Global Trace Width / Track Clearance: actual trace width and minimum copper spacing.
- Global Connect All Chip Pins: enabled by default. Each modeled IC pin must receive a distinct connection; failed routing leaves the previous floor intact. ICs have a minimum physical pin pitch that accommodates track/via clearance and routing resolution. All pin launches are protected before routing.
- Global Routing Layers: number of separate copper planes (2–8, default 4). The top plane and via stubs are visible; other planes route between via centers and have independent copper clearance. Via barrels reserve space across every plane.
- Global Signals Per Chip: budget for extra RAM signals when all IC pins are already connected. This does not cap the required 32-pin fanouts.
- Bus Spacing / Bus Lanes: legacy controls, hidden in global mode.
- Global Routing Step: routing grid precision; large boards automatically bound the grid resolution.
- Global Chunk Size / Surface Resolution: bake block dimensions and pixels per block. Larger blocks at equal resolution reduce texture detail.
- Library Central Composition Weights: controller / capacitors / RAM / empty weights. Global generation reads these directly, without rebuilding starter prefabs.

This is a visual PCB generator with two physical copper layers and explicit via continuations. It does not simulate an electrical schematic or choose electrically meaningful signal names. It does enforce geometry clearance for separate connections. It does not implement a shove router or rip-up optimization. If the mandatory connected graph cannot be completed, generation throws before replacing the existing floor; it never accepts a crossing as a fallback.

**Tools → AAR → PCB → Validate Global Board** verifies graph continuity, unique modeled contacts, segment-to-segment distances on every copper layer, via clearance across all layers, complete modeled IC pin coverage, RAM proportions, octilinear paths, footprint overlap and independence from chunk dimensions on three seeds. Planning has a greater CPU cost than the previous soft-cost prototype; generation is explicit rather than per-frame. When Bake Surface is enabled, the shallow top copper and vias become normal/material maps. Routing on the other copper planes is retained in the plan; Bake Surface off generates a separate mesh at a distinct depth for each plane.

## Legacy tile modes

**Shared Sockets** and **Matched Tiles** remain available for existing layouts and authored modular kits. Their Columns/Rows/Tile Size controls apply only to those modes. Shared Sockets aligns variable boundary ports; Matched Tiles uses centered authored connections. The following tile-contract and kit parameters describe these legacy modes.

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

`Assets/Imported/ComputerPackv2` contains the source FBX and five normalized mesh/prefab variants: two capacitors, two small components and a RAM module. The starter library exposes Imported Capacitors, Imported Passives and Imported Modules, plus **Imported Component Variation**, **Imported Height Scale** and **Imported Passive Scale** (2.2 by default). The passive scale enlarges the imported long chips, their placement clearance and fitted terminal positions together; it leaves the procedural resistors at their original size. Rebuild Starter Geometry applies these choices deterministically to existing reserved package positions and preserves their trace lands and keepouts; then Generate refreshes the floor. Capacitors and passives replace their matching procedural packages. RAM occurs as a separate vertical module in a low socket in a subset of controller recipes; the controller retains its matching QFP pin banks. Imported pieces fit inside their reserved footprint without expanding short pieces to fill both axes. Height is derived from that fit, capped by the recipe height, and multiplied by Imported Height Scale. Imported chip contacts are extracted from disconnected mesh islands touching the mounting plane (32 legs in each bundled chip). Every leg receives an aligned solder land and outward copper fanout ending at a via. One longer branch per pin bank is offered to the controller router before capacitors and generic terminals. Routes retain clearance from the actual fitted package, rather than the larger placement reservation; these fanouts are included in the normal-map/material-mask bake. Procedural resistor contacts follow their fitted length and width. Body/contact materials use the existing palette.

Components and pins are authored with their feet at Y=0. Baked floors mount them directly at the board surface; unbaked floors lift both groups to the raised pad support height before applying the bake. RAM is supported by its 3D socket, with dedicated end contacts. Regenerating retains these mounting rules across seeds and both routing modes.

Triangle savings depend on the seed and selected component recipes. The generator reports Last Surface Triangles Removed for the current output; components account for most of the remaining geometry.

## Connected board and central compositions

**Connect Every Tile** is enabled by default. It forces full graph coverage even when Trace Coverage is lower, so every tile belongs to the same network. Loop Chance still controls additional connections. Disable it only when intentionally leaving unconnected board regions. **Shared Sockets**, now the default routing style, builds one continuous board network with matching randomized boundary sockets and 45-degree feed paths; every tile has a feed to its local circuit. The test scene uses this mode. The baked masks retain the connections across tile boundaries.

The library's **Central Composition Weights** are relative weights in this order: controller, capacitor bank, RAM bank, empty center. Rebuild Starter Geometry, then generate the floor to apply changes. Default weights give equal representation across the twelve authored variants. Capacitor banks mix large, medium and small bodies; **Central Capacitor Scale** and **Central Component Height** adjust them. **Central RAM Count** controls one to four parallel mounted modules. Empty centers retain the flat circuit bus and peripheral parts, without a mandatory central package. Non-controller centers receive their own flat solder lands and bus; they do not inherit QFP legs or its top markings. All compositions retain the same boundary connection contract and procedural seed behavior.

Procedural resistor bodies, solder ends and RAM socket contacts share two combined meshes, reducing draw calls as component population grows. Imported component meshes remain independent.
