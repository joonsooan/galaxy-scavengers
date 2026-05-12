using UnityEngine;

[CreateAssetMenu(fileName = "Unit Upgrade Line", menuName = "Unit/Unit Upgrade Line")]
public class UnitUpgradeLineData : ScriptableObject
{
    public UnitUpgradeStatType statType;
    public string displayName;
    public Sprite icon;
    public UnitUpgradeTier[] tiers;

    [Header("Localization")]
    [SerializeField] private string localizationTable = "GameData";
    [SerializeField] private string displayNameKey;

    public string LocalizationTable => localizationTable;

    public string GetDisplayName()
    {
        string fallback = string.IsNullOrEmpty(displayName) ? name : displayName;
        if (string.IsNullOrWhiteSpace(displayNameKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, displayNameKey, fallback);
    }
}
