using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlanetUnlockProvider : MonoBehaviour
{
    [SerializeField] private List<PlanetData> planets = new();
    [SerializeField] private List<string> unlockedPlanetIds = new();

    public List<PlanetData> GetUnlockedPlanets()
    {
        List<PlanetData> unlocked = new();
        HashSet<string> unlockedSet = new(unlockedPlanetIds);

        foreach (PlanetData planet in planets)
        {
            if (planet == null)
            {
                continue;
            }

            bool isUnlocked = planet.IsUnlockedByDefault || unlockedSet.Contains(planet.PlanetId);
            if (isUnlocked)
            {
                unlocked.Add(planet);
            }
        }

        return unlocked.OrderBy(p => p.DisplayOrder).ThenBy(p => p.PlanetName).ToList();
    }
}
