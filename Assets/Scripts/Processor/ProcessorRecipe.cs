using UnityEngine;

[System.Serializable]
public class ProcessorRecipe
{
    public ResourceType resourceType;
    public Sprite recipeIcon;
    public ResourceCost[] ingredients;
    public float processingTime;
    public int produceAmount;
    public int priority;
}