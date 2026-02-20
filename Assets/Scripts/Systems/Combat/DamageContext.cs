using UnityEngine;

public enum DamageAttackType
{
    Unknown,
    Melee,
    Projectile
}

public enum DamageAttackerFaction
{
    Neutral,
    Ally,
    Enemy
}

public readonly struct DamageContext
{
    public static readonly DamageContext Default = new DamageContext(DamageAttackType.Unknown, DamageAttackerFaction.Neutral, null);

    public DamageAttackType AttackType { get; }
    public DamageAttackerFaction AttackerFaction { get; }
    public GameObject Attacker { get; }

    public DamageContext(DamageAttackType attackType, DamageAttackerFaction attackerFaction, GameObject attacker)
    {
        AttackType = attackType;
        AttackerFaction = attackerFaction;
        Attacker = attacker;
    }

    public static DamageContext From(DamageAttackType attackType, DamageAttackerFaction attackerFaction, GameObject attacker = null)
    {
        return new DamageContext(attackType, attackerFaction, attacker);
    }

    public static DamageContext From(UnitBase.UnitType attackerType, DamageAttackType attackType, GameObject attacker = null)
    {
        DamageAttackerFaction faction = attackerType == UnitBase.UnitType.Ally ? DamageAttackerFaction.Ally : DamageAttackerFaction.Enemy;
        return new DamageContext(attackType, faction, attacker);
    }
}
