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
}