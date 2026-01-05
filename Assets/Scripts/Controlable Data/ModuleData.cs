using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Module Data", menuName = "Module/Module Data")]
public class ModuleData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string stationName;
    [SerializeField] [TextArea(3, 10)] private string stationInfo;
    
    [Header("Module Station Settings")]
    [SerializeField] private int maxModuleStorage = 10;
    
    [Header("Recipes")]
    [SerializeField] private List<ModuleRecipe> recipes;
    
    public string StationName => stationName;
    public string StationInfo => stationInfo;
    public int MaxModuleStorage => maxModuleStorage;
    public List<ModuleRecipe> Recipes => recipes;
}

