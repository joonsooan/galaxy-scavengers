public class Battery : BaseStorage
{
    public override bool TryAddResource(ResourceType type, int amount)
    {
        if (type != ResourceType.Aether) return false;
        return base.TryAddResource(type, amount);
    }

    public override bool TryWithdrawResource(ResourceType type, int amountToWithdraw, out int amountWithdrawn)
    {
        if (type != ResourceType.Aether)
        {
            amountWithdrawn = 0;
            return false;
        }
        return base.TryWithdrawResource(type, amountToWithdraw, out amountWithdrawn);
    }

    public override bool HasEnoughResources(ResourceCost[] costs)
    {
        foreach (var cost in costs)
        {
            if (cost.resourceType != ResourceType.Aether) return false;
        }
        return base.HasEnoughResources(costs);
    }
}