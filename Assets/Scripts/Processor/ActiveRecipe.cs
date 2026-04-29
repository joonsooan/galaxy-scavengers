public class ActiveRecipe
{
    public Unit_Processor assignedDrone;
    public bool isProcessing;
    public float processingProgress;
    public readonly ProcessorRecipe recipeData;
    public int maxProductionLimit;
    
    private readonly Processor _processor;

    public ActiveRecipe(ProcessorRecipe data, Processor processor)
    {
        recipeData = data;
        _processor = processor;
        isProcessing = false;
        maxProductionLimit = 0;
    }
    
    public void SetProductionLimit(int newLimit)
    {
        maxProductionLimit = newLimit;
        _processor.CheckProductionLimits(this);
    }
}
