using System.Reflection;
using UnityEngine;

public class StatModifierReceiver_UnitWorkSpeed : MonoBehaviour
{
    private Unit_Processor _drone;
    private bool _modifiersApplied;
    private float _originalProcessingSpeed;

    private void Awake()
    {
        _drone = GetComponent<Unit_Processor>();
    }

    private void Start()
    {
        _originalProcessingSpeed = GetProcessingSpeed();
        ApplyModifiers();
    }

    private void OnDestroy()
    {
        if (_modifiersApplied && _drone != null) {
            SetProcessingSpeed(_originalProcessingSpeed);
        }
    }

    public void ApplyModifiers()
    {
        if (_drone == null || ModuleEffectManager.Instance == null) return;

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.UnitWorkSpeed);
        float newSpeed = _originalProcessingSpeed * (1f + modifier);
        SetProcessingSpeed(newSpeed);

        _modifiersApplied = true;
    }

    private float GetProcessingSpeed()
    {
        FieldInfo field = typeof(Unit_Processor).GetField("processingSpeed",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field != null ? (float)field.GetValue(_drone) : 1f;
    }

    private void SetProcessingSpeed(float value)
    {
        FieldInfo field = typeof(Unit_Processor).GetField("processingSpeed",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) {
            field.SetValue(_drone, value);
        }
    }
}
