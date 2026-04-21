using System;

public static class PlanetSelectionState
{
    private const string DefaultSceneName = "GameScene";

    public static PlanetData SelectedPlanet { get; private set; }

    public static event Action<PlanetData> SelectedPlanetChanged;

    public static string GetSelectedSceneName()
    {
        if (SelectedPlanet == null || string.IsNullOrWhiteSpace(SelectedPlanet.TargetSceneName))
        {
            return DefaultSceneName;
        }

        return SelectedPlanet.TargetSceneName;
    }

    public static void SetSelectedPlanet(PlanetData planetData)
    {
        SelectedPlanet = planetData;
        SelectedPlanetChanged?.Invoke(planetData);
    }

    public static void ClearSelection()
    {
        SelectedPlanet = null;
        SelectedPlanetChanged?.Invoke(null);
    }
}
