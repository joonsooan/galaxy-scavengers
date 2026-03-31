public interface IElectricityConsumer
{
    int ElectricityConsumptionPerSecond { get; }
    void OnElectricityUnavailable();
    void OnElectricityAvailable();
    bool IsOperational { get; }
}
