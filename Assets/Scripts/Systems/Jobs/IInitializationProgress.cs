namespace Systems.Jobs
{
    public interface IInitializationProgress
    {
        void UpdateProgress(float progress, string stage, string stageKey = null);
    }
}
