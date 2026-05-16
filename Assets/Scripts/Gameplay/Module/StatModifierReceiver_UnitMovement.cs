using UnityEngine;

public class StatModifierReceiver_UnitMovement : MonoBehaviour
{
    private bool _modifiersApplied;
    private UnitMovement _movement;
    private float _originalMoveSpeed;

    private void Awake()
    {
        _movement = GetComponent<UnitMovement>();
    }

    private void Start()
    {
        _originalMoveSpeed = _movement.moveSpeed;
        ApplyModifiers();
    }

    private void OnDestroy()
    {
        if (_modifiersApplied && _movement != null) {
            _movement.moveSpeed = _originalMoveSpeed;
        }
    }

    public void ApplyModifiers()
    {
        if (_movement == null || ModuleEffectManager.Instance == null) return;

        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.UnitMoveSpeed);
        _movement.moveSpeed = _originalMoveSpeed * (1f + modifier);

        _modifiersApplied = true;
    }
}
