using System;
using UnityEngine;

[Serializable]
public class UnitUpgradeTier
{
    public ResourceCost[] costs;
    public float upgradeTime;
    [TextArea(2, 8)]
    public string description;
    public float floatStatBonus;
    public int intStatBonus;
}
