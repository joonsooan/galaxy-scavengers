using UnityEngine;

[CreateAssetMenu(fileName = "Unit Upgrade Catalog", menuName = "Unit/Unit Upgrade Catalog")]
public class UnitUpgradeCatalog : ScriptableObject
{
    public UnitUpgradeLineData moveSpeedLine;
    public UnitUpgradeLineData workSpeedLine;
    public UnitUpgradeLineData storageLine;
    public UnitUpgradeLineData maxPopulationLine;

    public UnitUpgradeLineData GetLine(UnitUpgradeStatType type)
    {
        switch (type) {
        case UnitUpgradeStatType.MoveSpeed:
            return moveSpeedLine;
        case UnitUpgradeStatType.WorkSpeed:
            return workSpeedLine;
        case UnitUpgradeStatType.Storage:
            return storageLine;
        case UnitUpgradeStatType.MaxPopulation:
            return maxPopulationLine;
        default:
            return null;
        }
    }
}
