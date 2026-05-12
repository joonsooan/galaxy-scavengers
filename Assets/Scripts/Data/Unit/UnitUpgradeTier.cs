using System;
using UnityEngine;

[Serializable]
public class UnitUpgradeTier
{
    public int tokenCost;
    public float upgradeTime;
    [TextArea(2, 8)]
    public string description;
    public string descriptionKey;
    public float floatStatBonus;
    public int intStatBonus;

    public string GetDescription(string table, string fallback)
    {
        string resolvedFallback = string.IsNullOrEmpty(fallback) ? description : fallback;
        if (string.IsNullOrWhiteSpace(descriptionKey))
        {
            return resolvedFallback ?? string.Empty;
        }

        return GameLocalization.GetOrDefault(table, descriptionKey, resolvedFallback ?? string.Empty);
    }
}
