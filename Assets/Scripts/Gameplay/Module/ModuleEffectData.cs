using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "New Module Effect Data", menuName = "Module/Module Effect Data")]
public class ModuleEffectData : ScriptableObject
{
    [Header("Stat Modifiers")]
    [SerializeField] private List<ModuleStatModifier> statModifiers = new List<ModuleStatModifier>();

    [Header("Description")]
    [SerializeField] [TextArea(3, 10)]
    private string effectDescription = "";

    public IReadOnlyList<ModuleStatModifier> StatModifiers {
        get {
            return statModifiers;
        }
    }

    public string EffectDescription {
        get {
            return effectDescription;
        }
    }

    public void ApplyModifiers()
    {
        foreach (ModuleStatModifier modifier in statModifiers) {
            if (modifier.modifierValue > 0f) {
                ModuleEffectManager.Instance.AddStatModifier(modifier.statType, modifier.modifierValue);
            }
        }
    }

    public string GetDescription()
    {
        if (statModifiers == null || statModifiers.Count == 0) {
            return GetStatName(ModuleStatType.None);
        }

        StringBuilder sb = new StringBuilder();
        bool hasAnyEffect = false;
        foreach (ModuleStatModifier modifier in statModifiers) {
            if (modifier.modifierValue > 0f) {
                string statName = GetStatName(modifier.statType);
                sb.AppendLine($"{statName}: +{modifier.modifierValue * 100f:F0}%");
                hasAnyEffect = true;
            }
        }

        if (!hasAnyEffect) {
            return GetStatName(ModuleStatType.None);
        }

        return sb.ToString().TrimEnd();
    }

    private string GetStatName(ModuleStatType statType)
    {
        return statType switch {
            ModuleStatType.None => "None",
            ModuleStatType.StorageCapacity => "Storage Capacity",
            ModuleStatType.UnitMoveSpeed => "Unit Move Speed",
            ModuleStatType.UnitWorkSpeed => "Unit Work Speed",
            ModuleStatType.BuildingHP => "Building HP",
            ModuleStatType.ResourceGenerationRate => "Resource Generation Rate",
            ModuleStatType.TurretAttackDamage => "Turret Attack Damage",
            _ => statType.ToString()
        };
    }
}
