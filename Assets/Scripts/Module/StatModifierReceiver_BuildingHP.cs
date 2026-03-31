using UnityEngine;

public class StatModifierReceiver_BuildingHP : MonoBehaviour
{
    private Damageable _damageable;
    private bool _modifiersApplied;
    private int _originalMaxHealth;

    private void Awake()
    {
        ResolveDamageable();
    }

    private void Start()
    {
        ResolveDamageable();
        ApplyModifiers();
    }

    private void ResolveDamageable()
    {
        if (_damageable != null) {
            return;
        }

        _damageable = GetComponent<Damageable>();
        if (_damageable == null) {
            _damageable = GetComponentInParent<Damageable>();
        }
    }

    private void OnDestroy()
    {
        if (_modifiersApplied && _damageable != null) {
            _damageable.SetMaxHealth(_originalMaxHealth);
        }
    }

    public void ApplyModifiers()
    {
        ResolveDamageable();
        if (_damageable == null || ModuleEffectManager.Instance == null) {
            return;
        }

        if (!_modifiersApplied) {
            _originalMaxHealth = _damageable.MaxHealth;
        }

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.BuildingHP);
        int newMaxHealth = Mathf.RoundToInt(_originalMaxHealth * (1f + modifier));

        _damageable.SetMaxHealth(newMaxHealth);

        _modifiersApplied = true;
    }
}
