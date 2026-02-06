using UnityEngine;

public class Unit_Enemy_1 : EnemyUnitBase
{
    [Header("Ranged Attack Settings")]
    [SerializeField] private string bulletPoolTag = "EnemyBullet";
    [SerializeField] private float bulletSpeed = 8f;

    protected override void PerformAttackLogic(Damageable building, UnitBase unit)
    {
        Transform targetT = building != null ? building.transform : (unit != null ? unit.transform : null);
        if (targetT == null) return;

        Vector2 fireDirection = (targetT.position - transform.position).normalized;
        Vector3 spawnPosition = transform.position + (Vector3)fireDirection * 0.5f;

        float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        GameObject bulletObj = ObjectPooler.Instance.SpawnFromPool(bulletPoolTag, spawnPosition, rotation);

        if (bulletObj != null) {
            Enemy_Bullet bulletScript = bulletObj.GetComponent<Enemy_Bullet>();
            bulletScript.Initialize(attackDamage, fireDirection, bulletSpeed);
        }
    }
}