using UnityEngine;

public abstract class DisplayableData : ScriptableObject
{
    [Header("Common Display Info")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Localization")]
    [SerializeField] private string localizationTable = "GameData";
    [SerializeField] private string displayNameKey;
    [SerializeField] private string descriptionKey;

    public string GetDisplayName()
    {
        string fallback = string.IsNullOrEmpty(displayName) ? name : displayName;
        if (string.IsNullOrWhiteSpace(displayNameKey))
        {
            return fallback;
        }
        return GameLocalization.GetOrDefault(localizationTable, displayNameKey, fallback);
    }

    public string GetDescription()
    {
        string fallback = description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(descriptionKey))
        {
            return fallback;
        }
        return GameLocalization.GetOrDefault(localizationTable, descriptionKey, fallback);
    }
}