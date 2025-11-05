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

    public ActiveRecipe(ProcessorRecipe data)
    {
        recipeData = data;

        Mode = ProductionMode.Infinite;
        MaxProductionLimit = 100;
        assignedDrone = null;
        processingProgress = 0f;
        isProcessing = false;
    }

    public ProductionMode Mode { get; set; } = ProductionMode.Infinite;
    public int MaxProductionLimit { get; set; } = 100;
}
