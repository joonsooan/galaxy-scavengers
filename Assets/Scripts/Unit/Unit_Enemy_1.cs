using UnityEngine;

public class Unit_Enemy_1 : EnemyUnitBase
{
    [Header("Ranged Attack Settings")]
    [SerializeField] private string bulletPoolTag = "EnemyBullet";
    [SerializeField] private float bulletSpeed = 8f;

    protected override bool RequiresLineOfSight() => true;

    protected override bool HasLineOfSightToTarget(Damageable building, UnitBase unit)
    {
        Transform targetT = building != null ? building.transform : (unit != null ? unit.transform : null);
        if (targetT == null || BuildingManager.Instance?.grid == null) return false;

        Vector3 from = transform.position;
        Vector3 to = targetT.position;
        Grid grid = BuildingManager.Instance.grid;

        Vector3Int startCell = grid.WorldToCell(from);
        Vector3Int endCell = grid.WorldToCell(to);
        int dx = Mathf.Abs(endCell.x - startCell.x);
        int dy = Mathf.Abs(endCell.y - startCell.y);
        int steps = Mathf.Max(dx, dy, 1);

        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            Vector3 point = Vector3.Lerp(from, to, t);
            Vector3Int cell = grid.WorldToCell(point);
            if (BuildingManager.Instance.IsTerrainCell(cell)) return false;
            if (BuildingManager.Instance.IsResourceTile(cell)) return false;
        }
        return true;
    }

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