using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlanetData", menuName = "Game Data/Planet Data")]
public class PlanetData : ScriptableObject
{
    [SerializeField] private string planetId;
    [SerializeField] private string planetName;
    [SerializeField] [TextArea] private string descriptionText;
    [SerializeField] private Sprite planetImage;
    [SerializeField] private Sprite planetThumbnail;
    [SerializeField] private List<ResourceType> obtainableDataTypes = new();
    [SerializeField] private int expeditionDataAmount;
    [SerializeField] private string targetSceneName = "GameScene";
    [SerializeField] private bool isUnlockedByDefault;
    [SerializeField] private int displayOrder;
    [SerializeField] private string mapButtonName;

    [Header("Localization")]
    [SerializeField] private string localizationTable = "GameData";
    [SerializeField] private string planetNameKey;
    [SerializeField] private string descriptionKey;

    public string PlanetId => planetId;
    public string PlanetName => GetDisplayName();
    public string DescriptionText => GetDescription();
    public Sprite PlanetImage => planetImage;
    public Sprite PlanetThumbnail => planetThumbnail;
    public IReadOnlyList<ResourceType> ObtainableDataTypes => obtainableDataTypes;
    public int ExpeditionDataAmount => expeditionDataAmount;
    public string TargetSceneName => targetSceneName;
    public bool IsUnlockedByDefault => isUnlockedByDefault;
    public int DisplayOrder => displayOrder;
    public string MapButtonName => mapButtonName;

    public string GetDisplayName()
    {
        string fallback = string.IsNullOrEmpty(planetName) ? name : planetName;
        if (string.IsNullOrWhiteSpace(planetNameKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, planetNameKey, fallback);
    }

    public string GetDescription()
    {
        string fallback = descriptionText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(descriptionKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, descriptionKey, fallback);
    }

    public string GetDataTypeDisplayText()
    {
        if (obtainableDataTypes == null || obtainableDataTypes.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", obtainableDataTypes);
    }
}
