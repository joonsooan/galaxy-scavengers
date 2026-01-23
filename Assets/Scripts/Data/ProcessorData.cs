using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Processor Data", menuName = "Building/Processor Data")]
public class ProcessorData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string processorName;
    [SerializeField] [TextArea(3, 10)] private string processorInfo;
    
    [Header("Processor Settings")]
    [SerializeField] private int maxIngredientStorage = 100;
    [SerializeField] private int maxAssignedDrones = 2;

    [Header("Recipes")]
    [SerializeField] private List<ProcessorRecipe> recipes;

    public string ProcessorName => processorName;
    public string ProcessorInfo => processorInfo;
    public int MaxIngredientStorage => maxIngredientStorage;
    public int MaxAssignedDrones => maxAssignedDrones;
    public List<ProcessorRecipe> Recipes => recipes;
}