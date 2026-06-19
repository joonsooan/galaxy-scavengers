# Galaxy Scavengers - Developer Guide

Guidelines and commands for development on the Galaxy Scavengers Unity project.

## Build and Test Commands

### Running Unit Tests

Unity Test Runner can be run from the command line:

- Run all Editor tests:

  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testResults Logs/editmode-results.xml -testPlatform editmode
  ```

- Run all Playmode tests:

  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testResults Logs/playmode-results.xml -testPlatform playmode
  ```

### Build Project

- Perform a standalone Windows build:

  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath . -buildWindows64Player Build/Windows/GalaxyScavengers.exe
  ```

---

## Code Conventions (C# / Unity)

All code must conform to the project guidelines defined in [CODING_STANDARD.md](file:///c:/Unity Projects/galaxy-scavengers/Assets/Scripts/CODING_STANDARD.md).

### Core Summary

- **Style**: PascalCase for types/methods/public fields, camelCase for parameters/local variables, `_` prefix for private static/readonly variables.
- **Cache**: Cache `GetComponent` in `Awake`/`Start`. Never call it in `Update` or loops.
- **GC/Memory**: Use `CoroutineCache` instead of `new WaitForSeconds`. Avoid LINQ/lambda expressions in `Update()` or hot paths. Use `CompareTag` instead of `tag ==`.
- **⚠️ Null Checks**: Do NOT use `?.`, `??`, or `ReferenceEquals(obj, null)` on variables inheriting from `UnityEngine.Object`. Use standard `== null` or `!= null` instead.

---

## Unity Version Control & Assets

- ⚠️ **Staging Meta Files**: Every asset file (e.g. `.cs`, `.prefab`, `.unity`, `.mat`, `.png`, `.wav`, etc.) in the `Assets/` directory has an associated `.meta` file.
  - When **Adding/Creating** a file, you MUST stage the `.meta` file.
  - When **Renaming/Moving** a file, you MUST rename/move the `.meta` file and stage both.
  - When **Deleting** a file, you MUST delete the `.meta` file and stage the deletion.
- **Binary Assets**: Never attempt to modify binary/serialized asset files (like `.prefab`, `.unity`, `.mat`, `.asset`) directly with text-editing tools unless doing minor, safe textual replacements (like GUID fixing). Prefer letting Unity handle modifications.

---

## Claude Subagents & Orchestration

The project is structured with specialized subagents located in `.claude/agents/` to handle specific domains:

- `unity-core-architect`: Manages singleton manager scripts, scene loading, saving/loading, and overall framework structure (`Assets/Scripts/Core/`). Also manages system-level ScriptableObject data definitions (`Assets/Scripts/Data/`).
- `unity-gameplay-coder`: Manages gameplay mechanics, units, buildings, resources, energy grids (`Assets/Scripts/Gameplay/`). Also manages gameplay-specific ScriptableObject configurations (`Assets/Scripts/Data/`).
- `unity-gameplay-designer`: Designs and balances the technology trees, unlocks, and stat upgrades using existing game elements.
- `unity-ui-specialist`: Manages UI HUD panels, quest logs, alerts, localized text, and UI animations (`Assets/Scripts/UI/`).
- `unity-audio-fmod`: Manages audio event playbacks, FMOD parameters, and sound integration (`Assets/FMOD/`, `Assets/Scripts/Audio/`).
- `unity-perf-auditor`: A read-only auditor that scans code for Unity-specific performance problems and bad patterns.

### Orchestration Guidelines

- **Gameplay code changes**: Delegate script updates inside `Assets/Scripts/Gameplay/` and gameplay-related definitions in `Assets/Scripts/Data/` to `unity-gameplay-coder`.
- **Game Design & Tech Trees**: Delegate balance changes, tech progression, and unlock planning to `unity-gameplay-designer`.
- **UI changes**: Delegate script updates inside `Assets/Scripts/UI/` to `unity-ui-specialist`.
- **Core system changes**: Delegate changes in `Assets/Scripts/Core/` and system data configurations in `Assets/Scripts/Data/` to `unity-core-architect`.
- **Performance audits**: Run `unity-perf-auditor` to check files for potential bugs or optimizations.
- **Audio triggers**: Delegate FMOD bindings and audio management to `unity-audio-fmod`.

## Debugging Rules

- NEVER run full project rebuilds during debugging loops.
- ALWAYS ask for user permission if a debugging/fixing loop takes more than 3 iterations.
- NEVER read large asset or meta files when searching for code bugs.
