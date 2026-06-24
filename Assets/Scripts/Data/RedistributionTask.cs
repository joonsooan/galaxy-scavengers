/// <summary>
/// Represents a pending unit work order to move a quantity of a specific resource
/// from one storage (Source) to a higher-priority storage (Destination).
/// </summary>
public class RedistributionTask
{
    public IStorage Source;
    public IStorage Destination;
    public ResourceType ResourceType;
    public int Amount;
    public bool IsClaimed;
}
