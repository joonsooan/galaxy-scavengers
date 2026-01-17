using UnityEngine;

[System.Serializable]
public class QuestCheckData
{
    [Header("Check Type")]
    public QuestCheckType checkType;
    
    [Header("Target Information")]
    [Tooltip("Building name, unit name, location ID, etc.")]
    public string targetId;
    
    [Header("Requirements")]
    [Tooltip("For multiple constructions/productions")]
    public int requiredCount = 1;
    
    [Header("Location (for ScoutEnteredLocation)")]
    [Tooltip("Target location for scout location checks")]
    public Vector3 targetLocation;
    
    [Tooltip("Tolerance distance for location checks")]
    public float locationTolerance = 1.0f;
    
    // Progress tracking (runtime only, not serialized)
    [System.NonSerialized]
    public int currentCount = 0;
    
    [System.NonSerialized]
    public bool isCompleted = false;
    
    public bool IsRequirementMet()
    {
        if (isCompleted) return true;
        
        switch (checkType)
        {
            case QuestCheckType.ResourceRequirement:
                // Resource requirements are checked separately in QuestDataManager
                return true;
                
            case QuestCheckType.BuildingConstructed:
            case QuestCheckType.UnitProduced:
            case QuestCheckType.BeaconPlacedForScout:
                return currentCount >= requiredCount;
                
            case QuestCheckType.ScoutEnteredLocation:
            case QuestCheckType.ModulePlacedOnCore:
                return isCompleted;
                
            default:
                return false;
        }
    }
    
    public void ResetProgress()
    {
        currentCount = 0;
        isCompleted = false;
    }
}
