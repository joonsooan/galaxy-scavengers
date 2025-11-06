using UnityEngine;

public enum ProductionMode
{
    Infinite, // 무한 자동 생산
    Capped // 최대 생산량 제한
}

public class ActiveRecipe
{
    public Unit_Drone assignedDrone;
    public bool isProcessing;
    public float processingProgress;
    public ProcessorRecipe recipeData;
    public int maxProductionLimit;
    
    private ResourceProcessor _processor;

    public ActiveRecipe(ProcessorRecipe data, ResourceProcessor processor)
    {
        recipeData = data;
        _processor = processor;
        isProcessing = false;
        maxProductionLimit = 0;
    }
    
    public void SetProductionLimit(int newLimit)
    {
        int old = maxProductionLimit;
        maxProductionLimit = newLimit;
        
        if (old != newLimit)
        {
            Debug.Log($"[ActiveRecipe:{recipeData.resourceType}] maxProductionLimit changed {old} -> {newLimit}");
        }
        
        _processor.CheckProductionLimits(this);
    }
}
