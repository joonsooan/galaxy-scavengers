using System;
using UnityEngine;

[Serializable]
public class UnitUpgradeTier
{
    public int tokenCost;
    public float upgradeTime;
    [TextArea(2, 8)]
    public string description;
    public float floatStatBonus;
    public int intStatBonus;
}
