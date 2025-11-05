using UnityEngine;

[System.Serializable]
public class ProcessorRecipe
{
    public string recipeName;
    public Sprite recipeIcon;
    public ResourceCost[] ingredients;
    public ResourceCost product;
    public float processingTime;
    public int priority;
}