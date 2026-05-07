using UnityEngine;

public abstract class ModuleEffect : ScriptableObject
{
    [Header("UI Settings")]
    [SerializeField] protected string descriptionFormat = "효과: {0}";

    protected string GetLocalizedDescriptionFormat()
    {
        return GameLocalization.GetOrDefault("UI_Common", "label.effectFormat", descriptionFormat);
    }

    public abstract string GetDescription();
    public abstract void Apply(GameObject target);
}
