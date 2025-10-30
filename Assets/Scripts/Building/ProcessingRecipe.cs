[System.Serializable]
public class ProcessingRecipe
{
    public string recipeName;
    public CardCost[] ingredients;
    public CardCost product;
    public float processingTime;
    public int priority;
}