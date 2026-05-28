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

### Naming Styles

- **PascalCase**: Classes, Structs, Enums, Methods, Properties, Public fields, Events (`GameManager`, `SpawnUnits()`).
- **camelCase**: Method parameters, local variables (`unitData`, `count`).
- **Private Fields**: Prefix with camelCase (or prefix with `_` for static/readonly variables, e.g. `_tickWait`).

### Unity Best Practices & Optimization

- **`GetComponent` Caching**: Cache `GetComponent` references in `Awake()` or `Start()`. NEVER call `GetComponent`, `FindObjectOfType`, or camera reference properties (like `Camera.main`) in `Update()`, `FixedUpdate()`, `LateUpdate()`, or inside performance-critical loops.
- **Coroutine Yield Instruction Caching**: Use the utility `CoroutineCache` instead of instantiating `new WaitForSeconds(...)` to avoid GC allocation:
  - Use `CoroutineCache.GetWaitForSeconds(1f)`
  - Use `CoroutineCache.GetWaitForSecondsRealtime(1f)`
  - Use `CoroutineCache.GetWaitForEndOfFrame()`
  - Use `CoroutineCache.GetWaitForFixedUpdate()`
- **CompareTag**: Use `other.CompareTag("Player")` instead of `other.tag == "Player"` (reduces GC allocation).
- **String reference alternatives**: Avoid string-based APIs like `Invoke`, `InvokeRepeating`, or `SendMessage`. Use Coroutines or standard C# events/actions.

### ⚠️ CRITICAL Gotcha: Unity Object Null Checks

- **NEVER use null-coalescing (`??`) or null-conditional (`?.`) operators** on variables inheriting from `UnityEngine.Object` (such as `MonoBehaviour`, `GameObject`, `Transform`, `Component`, `ScriptableObject`).
- **Why**: Unity overrides the `==` and `!=` operators for these objects to check if the underlying native C++ object is destroyed. The `?.` and `??` operators bypass this custom C++ lifetime check, which can lead to bugs where a destroyed Unity object is incorrectly treated as active.
- **Rule**: Always check for null using standard `if (obj == null)` or `if (obj != null)`.

---

## Unity Version Control & Assets

- ⚠️ **Staging Meta Files**: Every asset file (e.g. `.cs`, `.prefab`, `.unity`, `.mat`, `.png`, `.wav`, etc.) in the `Assets/` directory has an associated `.meta` file.
  - When **Adding/Creating** a file, you MUST stage the `.meta` file.
  - When **Renaming/Moving** a file, you MUST rename/move the `.meta` file and stage both.
  - When **Deleting** a file, you MUST delete the `.meta` file and stage the deletion.
- **Binary Assets**: Never attempt to modify binary/serialized asset files (like `.prefab`, `.unity`, `.mat`, `.asset`) directly with text-editing tools unless doing minor, safe textual replacements (like GUID fixing). Prefer letting Unity handle modifications.
