using System.Collections.Generic;
using UnityEngine;

public static class PlanetResourceCellsRenderer
{
    public static void ClearSpawnedCells(List<BaseInventoryCell> spawnedCells)
    {
        for (int i = 0; i < spawnedCells.Count; i++)
        {
            BaseInventoryCell cell = spawnedCells[i];
            if (cell != null)
            {
                Object.Destroy(cell.gameObject);
            }
        }

        spawnedCells.Clear();
    }

    public static void RenderCells(Transform dataCellRoot, BaseInventoryCell dataCellPrefab, PlanetData planetData,
        List<BaseInventoryCell> spawnedCells)
    {
        ClearSpawnedCells(spawnedCells);
        if (dataCellRoot == null || dataCellPrefab == null || planetData == null)
        {
            return;
        }

        IReadOnlyList<ResourceType> dataTypes = planetData.ObtainableDataTypes;
        if (dataTypes == null || dataTypes.Count == 0)
        {
            return;
        }

        for (int i = 0; i < dataTypes.Count; i++)
        {
            BaseInventoryCell cell = Object.Instantiate(dataCellPrefab, dataCellRoot);
            cell.Initialize(null);
            cell.SetResource(dataTypes[i], planetData.ExpeditionDataAmount);
            spawnedCells.Add(cell);
        }
    }
}
