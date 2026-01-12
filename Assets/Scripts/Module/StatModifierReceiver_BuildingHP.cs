using UnityEngine;

public class StatModifierReceiver_BuildingHP : MonoBehaviour
{
    private Damageable _damageable;
    private bool _modifiersApplied;
    private int _originalMaxHealth;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
    }

    private void Start()
    {
        _originalMaxHealth = _damageable.MaxHealth;
        ApplyModifiers();
    }

    private void OnDestroy()
    {
        if (_modifiersApplied && _damageable != null) {
            _damageable.SetMaxHealth(_originalMaxHealth);
        }
    }

    public void ApplyModifiers()
    {
        if (_damageable == null || ModuleEffectManager.Instance == null) return;

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.BuildingHP);
        int newMaxHealth = Mathf.RoundToInt(_originalMaxHealth * (1f + modifier));

        _damageable.SetMaxHealth(newMaxHealth);

        _modifiersApplied = true;
    }
}
