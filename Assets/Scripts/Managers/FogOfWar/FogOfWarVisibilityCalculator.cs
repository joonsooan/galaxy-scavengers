using System;
using System.Collections.Generic;
using UnityEngine;

public class FogOfWarVisibilityCalculator
{
    private readonly HashSet<Vector3Int> _exploredTiles;
    private readonly Grid _grid;
    private readonly Dictionary<IVisionProvider, HashSet<Vector3Int>> _providerAffectedTiles;

    public FogOfWarVisibilityCalculator(
        Grid grid, Dictionary<IVisionProvider, HashSet<Vector3Int>> providerAffectedTiles,
        HashSet<Vector3Int> exploredTiles
    )
    {
        _grid = grid;
        _providerAffectedTiles = providerAffectedTiles;
        _exploredTiles = exploredTiles;
    }

    public Dictionary<IVisionProvider, HashSet<Vector3Int>> CalculateVisionProviderTiles(List<IVisionProvider> visionProviders)
    {
        Dictionary<IVisionProvider, HashSet<Vector3Int>> newAffectedTiles = new Dictionary<IVisionProvider, HashSet<Vector3Int>>();

        foreach (IVisionProvider provider in visionProviders) {
            if (provider == null || !provider.CheckIsActive()) continue;

            try {
                Vector3 worldPos = provider.GetPosition();
                float visionRange = provider.GetVisionRange();

                if (visionRange <= 0) continue;

                Vector3Int centerCell = _grid.WorldToCell(worldPos);
                int rangeInCells = Mathf.CeilToInt(visionRange);

                HashSet<Vector3Int> affectedTiles = CalculateAffectedTiles(centerCell, worldPos, visionRange, rangeInCells);
                newAffectedTiles[provider] = affectedTiles;
            }
            catch (Exception e) {
                Debug.LogWarning($"[FogOfWarManager] Error processing vision provider {provider.GetType().Name}: {e.Message}");
            }
        }

        return newAffectedTiles;
    }

    public Dictionary<Vector3Int, FogOfWarState> CalculateVisibilityStates(
        HashSet<Vector3Int> tilesToCheck,
        Dictionary<IVisionProvider, HashSet<Vector3Int>> newAffectedTiles
    )
    {
        Dictionary<Vector3Int, FogOfWarState> newVisibility = new Dictionary<Vector3Int, FogOfWarState>();

        foreach (Vector3Int tile in tilesToCheck) {
            FogOfWarState state = _exploredTiles.Contains(tile) ? FogOfWarState.PartlyVisible : FogOfWarState.Invisible;

            foreach (KeyValuePair<IVisionProvider, HashSet<Vector3Int>> kvp in newAffectedTiles) {
                if (kvp.Value.Contains(tile)) {
                    state = FogOfWarState.FullyVisible;

                    if (!_exploredTiles.Contains(tile)) {
                        _exploredTiles.Add(tile);
                    }
                    break;
                }
            }

            newVisibility[tile] = state;
        }

        return newVisibility;
    }

    public HashSet<Vector3Int> CalculateAffectedTiles(Vector3Int centerCell, Vector3 worldPos, float visionRange, int rangeInCells)
    {
        HashSet<Vector3Int> affectedTiles = new HashSet<Vector3Int>();

        for (int x = -rangeInCells; x <= rangeInCells; x++) {
            for (int y = -rangeInCells; y <= rangeInCells; y++) {
                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                Vector3 cellWorldPos = _grid.GetCellCenterWorld(cell);
                float distance = Vector3.Distance(worldPos, cellWorldPos);

                if (distance <= visionRange) {
                    affectedTiles.Add(cell);
                }
            }
        }

        return affectedTiles;
    }
}
