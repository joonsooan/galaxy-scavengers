public interface IAetherConsumer
{
    int AetherConsumptionPerSecond { get; }
    void OnAetherUnavailable();
    void OnAetherAvailable();
    bool IsOperational { get; }
}
