using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Processor Data", menuName = "Building/Processor Data")]
public class ProcessorData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string processorName;
    [SerializeField] [TextArea(3, 10)] private string processorInfo;

    [Header("Localization")]
    [SerializeField] private string localizationTable = "GameData";
    [SerializeField] private string processorNameKey;
    [SerializeField] private string processorInfoKey;
    
    [Header("Processor Settings")]
    [SerializeField] private int maxIngredientStorage = 100;
    [SerializeField] private int maxAssignedDrones = 2;

    [Header("Recipes")]
    [SerializeField] private List<ProcessorRecipe> recipes;

    public string ProcessorName => GetProcessorName();
    public string ProcessorInfo => GetProcessorInfo();
    public int MaxIngredientStorage => maxIngredientStorage;
    public int MaxAssignedDrones => maxAssignedDrones;
    public List<ProcessorRecipe> Recipes => recipes;

    public string GetProcessorName()
    {
        string fallback = string.IsNullOrEmpty(processorName) ? name : processorName;
        if (string.IsNullOrWhiteSpace(processorNameKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, processorNameKey, fallback);
    }

    public string GetProcessorInfo()
    {
        string fallback = processorInfo ?? string.Empty;
        if (string.IsNullOrWhiteSpace(processorInfoKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, processorInfoKey, fallback);
    }
}