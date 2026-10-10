# PCB Music prototype

The ShaderTest scene has a native Unity procedural music system. Camera framing selects chips, RAM and capacitors; their stable identities choose short motifs and timbres. A seeded director alternates Veille, Pression, Suspension, Retombée and true Silence. Activity, tension and silence probability are independent controls. Passive/SMD accents are available in the profile but disabled by default.

## Listening and controls

- Enter Play in ShaderTest. Musical silence can last several seconds; Music Lab displays the current state.
- Press **F8** to release the pointer and suspend player controls without pausing the music. Press it again to resume movement.
- Music Lab offers Auto, forced Veille/Pression/Silence/Suspension, frozen camera observation, solo by family and music volume. Click a solo button again to restore all families.
- The pause menu's Jeu tab has music enable and volume controls. Opening the pause menu suspends the music transport; closing it resumes without replaying late attacks. Volume and enable are saved locally.
- **Tools > AAR > Open PCB Music Profile** selects the project profile outside Play, or its temporary runtime copy in Play. Tempo, activity, tension, form durations, note intervals, visibility smoothing and voice limits live here. Pool size takes effect when the controller is enabled.

The instrument bank contains twelve original, deterministically synthesized mono WAVs, loaded before scheduling. They are a first sound palette for audition, not a finished soundtrack. The composer generates events at runtime; playback uses a fixed AudioSource pool and DSP scheduling. Voice gains have a conservative summed budget, and each sample has finite tails.

## Generation and persistence

Every new Global Board generation attaches a serialized `PCBMusicSnapshot` to Generated PCB Tiles. It stores logical components and connections independently of merged geometry and baked copper. Music survives saving/reopening the scene, transforming the board and regenerating it. Musical seeds are separate from board seeds.

**Tools > AAR > Install PCB Music Prototype** installs the profile, bank and scene controller. For an existing board without metadata, migration recomputes its plan and verifies component positions/count against its logical roots before attaching data. It does not regenerate geometry. A mismatched old board needs an explicit regeneration first. Matched Tiles and Shared Sockets modes are not supported by this prototype.

The analyzer uses projected component bounds and up to three geometric occlusion samples per candidate against PCB bounds. This approximates partial visibility and currently does not account for unrelated walls, the character or mesh holes. A spatial index restricts the candidates; it avoids physical colliders per small component. Only currently eligible visible objects can start notes; existing notes retire naturally or fade when entering Silence/Suspension.

Multiple routed links between two parts collapse into one musical relationship. A selected RAM directly connected to the leading contributor uses a delayed response pattern. This is an initial graph interaction, not a full propagation model.

## Audition exports and verification

**Tools > AAR > Export PCB Music Auditions** writes three 32-second WAVs to the project Temp/PCBMusicAuditions folder: Resonance, Pulses and Hybrid. They use the same composer and instrument synthesis with scripted attention. They are sound-palette demonstrations, not recordings of a camera traversal. Each has an explicit silent section from approximately 14.15 to 21 seconds.

In Play, **Tools > AAR > Record PCB Music > Start 32 seconds**, then **Save recording**, exports the actual listener mix to Temp/PCBMusicAuditions/Live-PCB.wav. This also captures any other game sounds. Save before leaving Play; the buffer lives only in memory. The recorder stops after its allotted duration.

Validation covers semantic serialization, hidden and behind-camera rejection, moving the board, deterministic 400-step composition, automatic/forced silence, loaded audio samples, pause/resume, debug input release and the settings volume callback. The live scene was checked with 641 components and 778 links; no late or dropped attacks were observed during the runtime check. An eight-second stereo listener capture contained signal with a peak of approximately 0.137. Scripted audition waveforms were also checked for headroom and zero samples in the silent interval.

The full design and later stages are described in [Procedural Music Plan](../../Docs/Procedural-Music-Plan.md). Next artistic work is auditioning the palette and tuning tension, fatigue, phrase recognition and the value of silence during a recorded player route.
