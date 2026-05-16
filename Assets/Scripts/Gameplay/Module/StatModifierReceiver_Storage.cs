using System.Collections;
using UnityEngine;

public class StatModifierReceiver_Storage : MonoBehaviour
{
    private bool _modifiersApplied;
    private int _originalMaxStorage;
    private BaseStorage _storage;

    private void Awake()
    {
        _storage = GetComponent<BaseStorage>();
    }

    private void Start()
    {
        _originalMaxStorage = _storage.GetMaxCapacity();
        StartCoroutine(DelayedApplyModifiers());
    }

    private void OnDestroy()
    {
        if (_modifiersApplied && _storage != null) {
            _storage.SetMaxStorage(_originalMaxStorage);
        }
    }

    private IEnumerator DelayedApplyModifiers()
    {
        yield return null;
        ApplyModifiers();
    }

    public void ApplyModifiers()
    {
        if (_storage == null) return;

        if (ModuleEffectManager.Instance == null) {
            StartCoroutine(WaitForManagerAndApply());
            return;
        }

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.StorageCapacity);
        int newMaxStorage = Mathf.RoundToInt(_originalMaxStorage * (1f + modifier));

        _storage.SetMaxStorage(newMaxStorage);

        _modifiersApplied = true;
    }

    private IEnumerator WaitForManagerAndApply()
    {
        while (ModuleEffectManager.Instance == null) {
            yield return null;
        }
        ApplyModifiers();
    }
}
