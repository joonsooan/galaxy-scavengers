using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Module Data", menuName = "Module/Module Data")]
public class ModuleData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string stationName;
    [SerializeField] [TextArea(3, 10)] private string stationInfo;

    [Header("Localization")]
    [SerializeField] private string localizationTable = "GameData";
    [SerializeField] private string stationNameKey;
    [SerializeField] private string stationInfoKey;
    
    [Header("Module Station Settings")]
    [SerializeField] private int maxModuleStorage = 10;
    
    [Header("Recipes")]
    [SerializeField] private List<ModuleRecipe> recipes;
    
    public string StationName => GetStationName();
    public string StationInfo => GetStationInfo();
    public int MaxModuleStorage => maxModuleStorage;
    public List<ModuleRecipe> Recipes => recipes;

    public string GetStationName()
    {
        string fallback = string.IsNullOrEmpty(stationName) ? name : stationName;
        if (string.IsNullOrWhiteSpace(stationNameKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, stationNameKey, fallback);
    }

    public string GetStationInfo()
    {
        string fallback = stationInfo ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stationInfoKey))
        {
            return fallback;
        }

        return GameLocalization.GetOrDefault(localizationTable, stationInfoKey, fallback);
    }
}

