using UnityEngine;

public abstract class ModuleEffect : ScriptableObject
{
    [Header("UI Settings")]
    [SerializeField] protected string descriptionFormat = "효과: {0}";
    
    public abstract string GetDescription();
    public abstract void Apply(GameObject target);
}