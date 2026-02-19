using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class HitAudioRouter : MonoBehaviour
{
    [SerializeField] private EventReference hitEvent;
    [SerializeField] private string targetTypeParameter = "TargetType";
    [SerializeField] private string attackTypeParameter = "AttackType";
    [SerializeField] private string attackerFactionParameter = "AttackerFaction";

    private static HitAudioRouter _instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public static void PlayHit(Damageable target, DamageContext context)
    {
        HitAudioRouter router = _instance;
        if (router == null)
        {
            router = FindFirstObjectByType<HitAudioRouter>();
            _instance = router;
        }

        if (router == null || router.hitEvent.IsNull || target == null)
        {
            return;
        }

        EventInstance instance = RuntimeManager.CreateInstance(router.hitEvent);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(target.gameObject));
        instance.setParameterByNameWithLabel(router.targetTypeParameter, router.GetTargetTypeLabel(target));
        instance.setParameterByNameWithLabel(router.attackTypeParameter, router.GetAttackTypeLabel(context.AttackType));
        instance.setParameterByNameWithLabel(router.attackerFactionParameter, router.GetAttackerFactionLabel(context.AttackerFaction));
        instance.start();
        instance.release();
    }

    private string GetTargetTypeLabel(Damageable target)
    {
        UnitBase unit = target as UnitBase;
        if (unit == null)
        {
            return "Building";
        }

        return unit.unitType == UnitBase.UnitType.Ally ? "UnitAlly" : "UnitEnemy";
    }

    private string GetAttackTypeLabel(DamageAttackType attackType)
    {
        if (attackType == DamageAttackType.Melee)
        {
            return "Melee";
        }

        if (attackType == DamageAttackType.Projectile)
        {
            return "Projectile";
        }

        return "Unknown";
    }

    private string GetAttackerFactionLabel(DamageAttackerFaction attackerFaction)
    {
        if (attackerFaction == DamageAttackerFaction.Ally)
        {
            return "Ally";
        }

        if (attackerFaction == DamageAttackerFaction.Enemy)
        {
            return "Enemy";
        }

        return "Neutral";
    }
}
