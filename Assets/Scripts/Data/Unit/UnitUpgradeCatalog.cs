using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Unit Upgrade Catalog", menuName = "Unit/Unit Upgrade Catalog")]
public class UnitUpgradeCatalog : ScriptableObject
{
    [SerializeField] private List<UnitUpgradeLineData> lines = new List<UnitUpgradeLineData>();

    public IReadOnlyList<UnitUpgradeLineData> Lines => lines;

    public UnitUpgradeLineData GetLine(UnitUpgradeStatType type)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            UnitUpgradeLineData line = lines[i];
            if (line != null && line.statType == type)
            {
                return line;
            }
        }

        return null;
    }

    public IEnumerable<UnitUpgradeLineData> GetLinesInDisplayOrder()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            UnitUpgradeLineData line = lines[i];
            if (line != null)
            {
                yield return line;
            }
        }
    }
}
