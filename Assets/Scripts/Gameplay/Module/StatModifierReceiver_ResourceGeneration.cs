using System.Reflection;
using UnityEngine;

public class StatModifierReceiver_ResourceGeneration : MonoBehaviour
{
    private ResourceGenerator _generator;
    private bool _modifiersApplied;
    private float _originalGenerationInterval;

    private void Awake()
    {
        _generator = GetComponent<ResourceGenerator>() ?? GetComponentInChildren<ResourceGenerator>(true);
    }

    private void Start()
    {
        if (_generator == null) {
            _generator = GetComponent<ResourceGenerator>() ?? GetComponentInChildren<ResourceGenerator>(true);
        }
        
        if (_generator != null) {
            _originalGenerationInterval = GetGenerationInterval();
            ApplyModifiers();
        }
    }
    
    private void OnDestroy()
    {
        if (_modifiersApplied && _generator != null) {
            SetGenerationInterval(_originalGenerationInterval);
        }
    }

    public void ApplyModifiers()
    {
        if (_generator == null) {
            _generator = GetComponent<ResourceGenerator>() ?? GetComponentInChildren<ResourceGenerator>(true);
        }
        
        if (_generator == null || ModuleEffectManager.Instance == null) return;

        if (_originalGenerationInterval == 0f) {
            _originalGenerationInterval = GetGenerationInterval();
        }

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.ResourceGenerationRate);
        float newInterval = _originalGenerationInterval / (1f + modifier);
        SetGenerationInterval(newInterval);

        _modifiersApplied = true;
    }

    private float GetGenerationInterval()
    {
        if (_generator == null) return 5f;
        
        FieldInfo field = typeof(ResourceGenerator).GetField("generationInterval",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && _generator != null) {
            return (float)field.GetValue(_generator);
        }
        return 5f;
    }

    private void SetGenerationInterval(float value)
    {
        if (_generator == null) return;
        
        FieldInfo field = typeof(ResourceGenerator).GetField("generationInterval",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && _generator != null) {
            field.SetValue(_generator, value);
        }
    }
}
