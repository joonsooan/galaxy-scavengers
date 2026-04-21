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

    public string PlanetId => planetId;
    public string PlanetName => planetName;
    public string DescriptionText => descriptionText;
    public Sprite PlanetImage => planetImage;
    public Sprite PlanetThumbnail => planetThumbnail;
    public IReadOnlyList<ResourceType> ObtainableDataTypes => obtainableDataTypes;
    public int ExpeditionDataAmount => expeditionDataAmount;
    public string TargetSceneName => targetSceneName;
    public bool IsUnlockedByDefault => isUnlockedByDefault;
    public int DisplayOrder => displayOrder;
    public string MapButtonName => mapButtonName;

    public string GetDataTypeDisplayText()
    {
        if (obtainableDataTypes == null || obtainableDataTypes.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", obtainableDataTypes);
    }
}
