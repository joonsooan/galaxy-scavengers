using UnityEngine;

[CreateAssetMenu(fileName = "Unit Upgrade Line", menuName = "Unit/Unit Upgrade Line")]
public class UnitUpgradeLineData : ScriptableObject
{
    public UnitUpgradeStatType statType;
    public string displayName;
    public Sprite icon;
    public UnitUpgradeTier[] tiers;
}
