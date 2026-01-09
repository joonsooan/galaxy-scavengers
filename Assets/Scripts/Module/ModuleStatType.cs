using UnityEngine;

public enum ModuleStatType
{
    StorageCapacity,
    UnitMoveSpeed,
    UnitWorkSpeed,
    BuildingHP,
    ResourceGenerationRate,
    TurretAttackDamage
}

[System.Serializable]
public class ModuleStatModifier
{
    [Header("Stat Configuration")]
    [SerializeField] public ModuleStatType statType;
    [SerializeField] public float modifierValue;
    [SerializeField] public Sprite statIcon;
    
    public ModuleStatModifier(ModuleStatType type, float value, Sprite icon = null)
    {
        statType = type;
        modifierValue = value;
        statIcon = icon;
    }
}

