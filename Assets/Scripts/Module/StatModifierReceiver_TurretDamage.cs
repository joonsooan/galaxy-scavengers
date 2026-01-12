using System.Reflection;
using UnityEngine;

public class StatModifierReceiver_TurretDamage : MonoBehaviour
{
    private bool _modifiersApplied;
    private int _originalAttackDamage;
    private Turret _turret;

    private void Awake()
    {
        _turret = GetComponent<Turret>();
    }

    private void Start()
    {
        if (_turret == null) {
            _turret = GetComponent<Turret>();
        }
        
        if (_turret != null) {
            _originalAttackDamage = GetAttackDamage();
            ApplyModifiers();
        }
    }
    
    private void OnDestroy()
    {
        if (_modifiersApplied && _turret != null) {
            SetAttackDamage(_originalAttackDamage);
        }
    }

    public void ApplyModifiers()
    {
        if (_turret == null) {
            _turret = GetComponent<Turret>();
        }
        
        if (_turret == null || ModuleEffectManager.Instance == null) return;

        if (_originalAttackDamage == 0) {
            _originalAttackDamage = GetAttackDamage();
        }

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.TurretAttackDamage);
        int newDamage = Mathf.RoundToInt(_originalAttackDamage * (1f + modifier));
        SetAttackDamage(newDamage);

        _modifiersApplied = true;
    }

    private int GetAttackDamage()
    {
        if (_turret == null) return 10;
        
        FieldInfo field = typeof(Turret).GetField("attackDamage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && _turret != null) {
            return (int)field.GetValue(_turret);
        }
        return 10;
    }

    private void SetAttackDamage(int value)
    {
        if (_turret == null) return;
        
        FieldInfo field = typeof(Turret).GetField("attackDamage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && _turret != null) {
            field.SetValue(_turret, value);
        }
    }
}
