using UnityEngine;

[CreateAssetMenu(fileName = "New Tech Data", menuName = "Tech/Tech Data")]
public class TechData : ScriptableObject
{
    [Header("Tech Identification")]
    public int techIndex;

    [Header("Data Source")]
    public bool useExistingData;

    [Header("Existing Data Reference")]
    public DisplayableData existingDisplayableData;
    public UnitData existingUnitData;

    [Header("Manual Data")]
    public string manualName;
    [TextArea] public string manualDescription;
    public Sprite manualIcon;

    [Header("Research Cost")]
    public ResourceCost[] researchCosts;
    public int researchDuration;

    [Header("Tech Tree")]
    public int[] prerequisiteTechIndices;
    public int[] successorTechIndices;

    public string GetTechName()
    {
        if (useExistingData)
        {
            if (existingDisplayableData != null)
            {
                return existingDisplayableData.displayName;
            }
            if (existingUnitData != null)
            {
                return existingUnitData.unitName;
            }
            return name;
        }
        return manualName;
    }

    public string GetTechDescription()
    {
        if (useExistingData)
        {
            if (existingDisplayableData != null)
            {
                return existingDisplayableData.description;
            }
            if (existingUnitData != null)
            {
                return existingUnitData.description;
            }
            return string.Empty;
        }
        return manualDescription;
    }

    public Sprite GetTechIcon()
    {
        if (existingDisplayableData != null)
        {
            return existingDisplayableData.icon;
        }
        if (existingUnitData != null)
        {
            return existingUnitData.unitIcon;
        }
        return manualIcon;
    }
}
