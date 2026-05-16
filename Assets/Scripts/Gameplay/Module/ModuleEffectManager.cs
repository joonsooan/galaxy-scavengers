using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModuleEffectManager : MonoBehaviour
{
    private readonly Dictionary<ModuleStatType, float> _activeStatModifiers = new Dictionary<ModuleStatType, float>();
    public static ModuleEffectManager Instance { get; private set; }

    public IReadOnlyDictionary<ModuleStatType, float> ActiveStatModifiers {
        get {
            return _activeStatModifiers;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene") {
            StartCoroutine(WaitForCustomizationAndApplyEffects());
        }
    }

    private IEnumerator WaitForCustomizationAndApplyEffects()
    {
        CoreCustomizationManager customizationManager = null;
        int maxWaitFrames = 60;
        int waitFrames = 0;

        while (customizationManager == null && waitFrames < maxWaitFrames) {
            customizationManager = FindFirstObjectByType<CoreCustomizationManager>();
            if (customizationManager == null) {
                yield return null;
                waitFrames++;
            }
        }

        if (customizationManager == null) {
            Debug.LogError("ModuleEffectManager: CoreCustomizationManager를 찾을 수 없습니다.");
            yield break;
        }

        yield return null;

        ApplyModuleEffects();
    }

    private void ApplyModuleEffects()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") {
            return;
        }

        CoreCustomizationManager customizationManager = FindFirstObjectByType<CoreCustomizationManager>();
        if (customizationManager == null) {
            Debug.LogError("ModuleEffectManager: CoreCustomizationManager를 찾을 수 없습니다.");
            return;
        }

        List<Module> modules = customizationManager.GetActiveModules();
        if (modules == null) {
            Debug.LogWarning("ModuleEffectManager: 모듈 리스트가 null입니다.");
            _activeStatModifiers.Clear();
            return;
        }

        _activeStatModifiers.Clear();

        // Debug.Log($"ModuleEffectManager: 활성화된 모듈 {modules.Count} 개 발견");

        foreach (Module module in modules) {
            if (module == null) {
                Debug.LogWarning("ModuleEffectManager: null 모듈이 발견되었습니다.");
                continue;
            }

            if (module.effectData != null) {
                ApplyModuleEffect(module);
            }
            else {
                Debug.LogWarning($"ModuleEffectManager: 모듈 '{module.moduleName}'에 effectData가 없습니다.");
            }
        }

        ApplyModifiersToExistingObjects();
        UpdateActiveStatsDisplay();

        Debug.Log($"ModuleEffectManager: 모듈 {modules.Count} 개 적용 완료");

        if (_activeStatModifiers.Count > 0) {
            Debug.Log("ModuleEffectManager: 활성화된 스탯 수정자:");
            foreach (KeyValuePair<ModuleStatType, float> kvp in _activeStatModifiers) {
                Debug.Log($"  - {kvp.Key}: +{kvp.Value * 100f:F0}%");
            }
        }
    }

    private void ApplyModuleEffect(Module module)
    {
        if (module.effectData == null) return;

        module.effectData.ApplyModifiers();

        Debug.Log($"ModuleEffectManager: 모듈 '{module.moduleName}' 적용");

        if (module.effectData.StatModifiers != null && module.effectData.StatModifiers.Count > 0) {
            foreach (ModuleStatModifier modifier in module.effectData.StatModifiers) {
                if (modifier.modifierValue > 0f) {
                    Debug.Log($"  - {modifier.statType}: +{modifier.modifierValue * 100f:F0}%");
                }
            }
        }
    }

    public void AddStatModifier(ModuleStatType statType, float modifierValue)
    {
        if (_activeStatModifiers.ContainsKey(statType)) {
            _activeStatModifiers[statType] += modifierValue;
        }
        else {
            _activeStatModifiers[statType] = modifierValue;
        }
    }

    public float GetStatModifier(ModuleStatType statType)
    {
        return _activeStatModifiers.TryGetValue(statType, out float value) ? value : 0f;
    }

    public float GetModifiedValue(float baseValue, ModuleStatType statType)
    {
        float modifier = GetStatModifier(statType);
        return baseValue * (1f + modifier);
    }

    private void ApplyModifiersToExistingObjects()
    {
        foreach (BaseStorage storage in FindObjectsByType<BaseStorage>(FindObjectsSortMode.None)) {
            if (storage.TryGetComponent(out StatModifierReceiver_Storage receiver)) {
                receiver.ApplyModifiers();
            }
        }

        foreach (UnitBase unit in FindObjectsByType<UnitBase>(FindObjectsSortMode.None)) {
            if (unit.TryGetComponent(out StatModifierReceiver_UnitMovement receiver)) {
                receiver.ApplyModifiers();
            }

            if (unit.TryGetComponent(out StatModifierReceiver_UnitWorkSpeed receiver2)) {
                receiver2.ApplyModifiers();
            }
        }

        foreach (Damageable building in FindObjectsByType<Damageable>(FindObjectsSortMode.None)) {
            if (building.TryGetComponent(out StatModifierReceiver_BuildingHP receiver)) {
                receiver.ApplyModifiers();
            }
        }

        foreach (ResourceGenerator generator in FindObjectsByType<ResourceGenerator>(FindObjectsSortMode.None)) {
            if (generator.TryGetComponent(out StatModifierReceiver_ResourceGeneration receiver)) {
                receiver.ApplyModifiers();
            }
        }

        foreach (Turret turret in FindObjectsByType<Turret>(FindObjectsSortMode.None)) {
            if (turret.TryGetComponent(out StatModifierReceiver_TurretDamage receiver)) {
                receiver.ApplyModifiers();
            }
        }
    }

    private void UpdateActiveStatsDisplay()
    {
        ActiveStatDisplay display = FindFirstObjectByType<ActiveStatDisplay>();
        if (display != null) {
            display.UpdateDisplay();
        }
    }

    public void OnObjectCreated(GameObject obj)
    {
        if (obj == null) return;

        if (obj.TryGetComponent(out StatModifierReceiver_Storage storageReceiver)) {
            storageReceiver.ApplyModifiers();
        }

        if (obj.TryGetComponent(out StatModifierReceiver_UnitMovement movementReceiver)) {
            movementReceiver.ApplyModifiers();
        }

        if (obj.TryGetComponent(out StatModifierReceiver_UnitWorkSpeed workSpeedReceiver)) {
            workSpeedReceiver.ApplyModifiers();
        }

        if (obj.TryGetComponent(out StatModifierReceiver_BuildingHP hpReceiver)) {
            hpReceiver.ApplyModifiers();
        }

        if (obj.TryGetComponent(out StatModifierReceiver_ResourceGeneration genReceiver)) {
            genReceiver.ApplyModifiers();
        }

        if (obj.TryGetComponent(out StatModifierReceiver_TurretDamage turretReceiver)) {
            turretReceiver.ApplyModifiers();
        }
    }

    public void RefreshModuleEffects()
    {
        if (SceneManager.GetActiveScene().name == "GameScene") {
            StartCoroutine(WaitForCustomizationAndApplyEffects());
        }
    }
}
