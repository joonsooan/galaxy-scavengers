# Galaxy Scavengers - Unity C# Coding Standards

This document defines the coding conventions, performance guidelines, and safety practices for C# development in the Galaxy Scavengers project. All custom subagents reference and enforce these standards.

---

## 1. Naming Conventions

- **PascalCase**: Used for Classes, Structs, Enums, Methods, Properties, Public fields, and Events.
  - _Examples_: `GameManager`, `SpawnUnits()`, `OnPowerTick`
- **camelCase**: Used for local variables, method parameters, and non-public instance fields.
  - _Examples_: `unitData`, `count`, `currentHealth`
- **Private Fields (Static/Readonly)**: Prefix with `_` followed by camelCase.
  - _Examples_: `_tickWait`, `_waitForSecondsCache`
- **Unity Inspector Fields**: Expose private fields to the Inspector using `[SerializeField] private Type fieldName;` rather than using `public` variables where encapsulation is needed.

---

## 2. ⚠️ Unsafe Null Checks Guard (CRITICAL)

- **NEVER use null-conditional (`?.`) or null-coalescing (`??`) operators** on variables inheriting from `UnityEngine.Object` (e.g., `MonoBehaviour`, `GameObject`, `Transform`, `Component`, `ScriptableObject`, `Sprite`).
- **NEVER use `System.Object.ReferenceEquals(obj, null)`** to check if a Unity object has been destroyed.
- **Why**: Unity overrides the `==` and `!=` operators for these types to query the native C++ object's lifecycle. `?.`, `??`, and `ReferenceEquals` bypass this overridden operator and check the C# wrapper reference directly. If a Unity Object is destroyed in the engine, its C# wrapper still exists in memory, making these bypasses return false (incorrectly treating the destroyed object as alive), which leads to `MissingReferenceException` or logic corruption.
- **Rule**: Always perform null checks using standard comparison operators:
  - `if (myMonoBehaviour == null)` or `if (myMonoBehaviour != null)`

---

## 3. Performance & Memory Optimization (Unity 6)

### A. Update & Loop Allocations

- **NO LINQ in hot paths**: Never use LINQ queries (e.g., `Where`, `Select`, `Any`, `First`) inside `Update()`, `FixedUpdate()`, `LateUpdate()`, or inside any high-frequency loops. LINQ creates delegate allocations and enumerator garbage.
- **NO Lambda Expressions in hot paths**: Avoid passing anonymous lambda expressions (e.g., `x => x.isActive`) inside `Update()` or loops, as they allocate memory for closures.
- **No string concatenations in hot paths**: Do not construct strings (like `logMsg + score`) in `Update()` or loop methods. Use `StringBuilder` or pre-formatted strings if updates are required.

### B. Unity API Caching

- **`GetComponent` Caching**: Cache all references returned by `GetComponent`, `GetComponentInChildren`, `FindObjectOfType`, `Find`, or `Camera.main` in `Awake()` or `Start()`. NEVER call these methods in `Update()` or frequently-executed loops.
- **Coroutine Yield Instruction Caching**: Reuse yield instructions to minimize Garbage Collection (GC) spikes. Use the project's static `CoroutineCache`:
  - `CoroutineCache.GetWaitForSeconds(float seconds)`
  - `CoroutineCache.GetWaitForSecondsRealtime(float seconds)`
  - `CoroutineCache.GetWaitForEndOfFrame()`
  - `CoroutineCache.GetWaitForFixedUpdate()`

### C. Collisions and Tags

- **CompareTag**: Use `other.CompareTag("Player")` instead of `other.tag == "Player"` to avoid string duplication and heap allocation.
