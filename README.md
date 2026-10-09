# AARgame

Unity game project for the GameJamAAR team.

## Get started

1. Install Git, [Git LFS](https://git-lfs.com), and Unity Hub.
2. Install **Unity 6000.6.5f1** through Hub. The source of truth is
   `ProjectSettings/ProjectVersion.txt`; coordinate editor upgrades with the team.
3. Clone and set up the project:

   ```sh
   git lfs install
   git clone https://github.com/GameJamAAR/AARgameProject.git
   cd AARgameProject
   bash scripts/setup-git.sh
   git lfs pull
   ```

   Use Terminal on macOS/Linux or Git Bash on Windows. The setup script configures
   only this clone. For a custom Unity installation, supply its merge tool path:

   ```sh
   bash scripts/setup-git.sh "/full/path/to/UnityYAMLMerge"
   ```

4. In Unity Hub, add the cloned folder as a project and open it. Let Unity finish
   importing assets and restoring packages, then open `Assets/Scenes/SampleScene.unity`
   and press Play.

## Working together

- Start each change from an updated default branch and use a short feature branch
  such as `feature/player-movement`. Submit a pull request with what changed and
  how you checked it in Unity.
- Commit `Assets/` **with their `.meta` files**, `Packages/manifest.json`,
  `Packages/packages-lock.json`, and `ProjectSettings/`. Move and rename assets
  in Unity so their references stay intact. Never regenerate a teammate's meta files.
- Unity is already configured for **Force Text** serialization and **Visible Meta
  Files**. Keep these settings enabled.
- Generated folders, builds, IDE projects, and personal editor state are ignored.
  Shared VS Code configuration is kept in the repository.
- Git LFS rules already cover common art, audio, models, and other binary assets.
  Install LFS before adding assets; keep `.meta`, scenes, and prefabs as text.
  Existing binaries committed before LFS was configured are not rewritten by setup.
- Coordinate edits to the same scene or prefab. Prefer separate prefabs for features
  and avoid unrelated scene/settings changes in a pull request.
- Save work and close Unity before switching branches or resolving conflicts.
  Reopen the editor afterward and check the affected scenes, prefabs, and Console.
- Describe validation in your pull request: editor version, scenes tested, and any
  relevant Edit Mode/Play Mode test results. Include a screenshot for visible changes.

## Scene and prefab conflicts

The setup script connects the existing `.gitattributes` rules to Unity Smart Merge.
It runs during Git merges of Unity YAML files. For a conflict that remains, try:

```sh
git mergetool --tool=unityyamlmerge -- path/to/conflicted.unity
```

Review the result in Unity before committing. Binary asset conflicts need an agreed
choice of version or a manual edit in the source tool. See Unity's
[Smart Merge documentation](https://docs.unity.com/en-us/engine/6000.7/manual/get-started/project-configuration/version-control/smart-merge)
for merge tool behavior and fallback configuration.
