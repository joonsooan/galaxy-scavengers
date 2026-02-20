using System.Collections.Generic;
using UnityEngine;

public class TargetManager : MonoBehaviour
{
    public static TargetManager Instance { get; private set; }

    private readonly List<Damageable> _allTargets = new();
    
    public IReadOnlyList<Damageable> AllTargets => _allTargets;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterTarget(Damageable target)
    {
        if (!_allTargets.Contains(target))
        {
            _allTargets.Add(target);
        }
    }

    public void UnregisterTarget(Damageable target)
    {
        if (_allTargets.Contains(target))
        {
            _allTargets.Remove(target);
        }
    }

    public List<Damageable> GetTargetsInArea(Vector3 center, float radius)
    {
        List<Damageable> results = new List<Damageable>();
        float radiusSquared = radius * radius;

        foreach (Damageable target in _allTargets)
        {
            if (target == null) continue;
            if (target is UnitBase) continue;
            float distSquared = (center - target.transform.position).sqrMagnitude;
            if (distSquared <= radiusSquared)
            {
                results.Add(target);
            }
        }

        return results;
    }
}