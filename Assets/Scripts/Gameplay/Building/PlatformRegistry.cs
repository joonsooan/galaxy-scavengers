using System.Collections.Generic;
using UnityEngine;

public static class PlatformRegistry
{
    private static readonly List<Platform> Platforms = new List<Platform>();

    public static void Register(Platform platform)
    {
        if (platform == null || Platforms.Contains(platform))
        {
            return;
        }
        Platforms.Add(platform);
    }

    public static void Unregister(Platform platform)
    {
        if (platform == null)
        {
            return;
        }
        Platforms.Remove(platform);
    }

    public static IReadOnlyList<Platform> GetAllPlatforms()
    {
        return Platforms;
    }

    public static Platform GetPlatformAtCell(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) return null;

        for (int i = 0; i < Platforms.Count; i++)
        {
            Platform platform = Platforms[i];
            if (platform == null) continue;

            IReadOnlyList<Vector3Int> occupied = platform.OccupiedCells;
            if (occupied != null && occupied.Count > 0)
            {
                for (int j = 0; j < occupied.Count; j++)
                {
                    if (occupied[j] == cell)
                    {
                        return platform;
                    }
                }
            }
            else
            {
                Vector3Int rootCell = BuildingManager.Instance.grid.WorldToCell(platform.transform.position);
                if (rootCell == cell)
                {
                    return platform;
                }
            }
        }

        return null;
    }

    public static bool IsCellOnConnectedPlatform(Vector3Int cell)
    {
        Platform platform = GetPlatformAtCell(cell);
        if (platform == null) return false;

        return IsConnectedToMainStructure(platform);
    }

    public static bool IsUnitOnConnectedPlatform(Component unit)
    {
        if (unit == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return false;
        }

        Vector3Int cell = BuildingManager.Instance.grid.WorldToCell(unit.transform.position);
        return IsCellOnConnectedPlatform(cell);
    }

    public static bool IsBuildingOnConnectedPlatform(Component building)
    {
        if (building == null || BuildingManager.Instance == null)
        {
            return false;
        }

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(building.transform, out _, out List<Vector3Int> occupiedCells) ||
            occupiedCells == null || occupiedCells.Count == 0)
        {
            if (BuildingManager.Instance.grid != null)
            {
                Vector3Int cell = BuildingManager.Instance.grid.WorldToCell(building.transform.position);
                return IsCellOnConnectedPlatform(cell);
            }
            return false;
        }

        for (int i = 0; i < occupiedCells.Count; i++)
        {
            if (!IsCellOnConnectedPlatform(occupiedCells[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsConnectedToMainStructure(Platform platform)
    {
        if (platform == null || BuildingManager.Instance == null)
        {
            return false;
        }

        MainStructure mainStructure = Object.FindFirstObjectByType<MainStructure>();
        if (mainStructure == null)
        {
            return false;
        }

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(mainStructure.transform, out _, out List<Vector3Int> mainCells) ||
            mainCells == null || mainCells.Count == 0)
        {
            return false;
        }

        Dictionary<Vector3Int, Platform> cellToPlatformMap = GetCellToPlatformMap();

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Platform> visitedPlatforms = new HashSet<Platform>();

        Vector3Int[] directions = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        for (int i = 0; i < mainCells.Count; i++)
        {
            Vector3Int mainCell = mainCells[i];
            for (int d = 0; d < directions.Length; d++)
            {
                Vector3Int neighbor = mainCell + directions[d];
                if (cellToPlatformMap.TryGetValue(neighbor, out Platform adjPlatform))
                {
                    if (visitedPlatforms.Add(adjPlatform))
                    {
                        IReadOnlyList<Vector3Int> occupied = adjPlatform.OccupiedCells;
                        if (occupied != null && occupied.Count > 0)
                        {
                            for (int j = 0; j < occupied.Count; j++)
                            {
                                queue.Enqueue(occupied[j]);
                            }
                        }
                        else
                        {
                            Vector3Int rootCell = BuildingManager.Instance.grid.WorldToCell(adjPlatform.transform.position);
                            queue.Enqueue(rootCell);
                        }
                    }
                }
            }
        }

        if (visitedPlatforms.Contains(platform))
        {
            return true;
        }

        while (queue.Count > 0)
        {
            Vector3Int currentCell = queue.Dequeue();

            for (int d = 0; d < directions.Length; d++)
            {
                Vector3Int neighbor = currentCell + directions[d];
                if (cellToPlatformMap.TryGetValue(neighbor, out Platform nextPlatform))
                {
                    if (visitedPlatforms.Add(nextPlatform))
                    {
                        if (nextPlatform == platform)
                        {
                            return true;
                        }

                        IReadOnlyList<Vector3Int> occupied = nextPlatform.OccupiedCells;
                        if (occupied != null && occupied.Count > 0)
                        {
                            for (int j = 0; j < occupied.Count; j++)
                            {
                                queue.Enqueue(occupied[j]);
                            }
                        }
                        else
                        {
                            Vector3Int rootCell = BuildingManager.Instance.grid.WorldToCell(nextPlatform.transform.position);
                            queue.Enqueue(rootCell);
                        }
                    }
                }
            }
        }

        return visitedPlatforms.Contains(platform);
    }

    private static Dictionary<Vector3Int, Platform> GetCellToPlatformMap()
    {
        Dictionary<Vector3Int, Platform> map = new Dictionary<Vector3Int, Platform>();
        if (BuildingManager.Instance == null) return map;

        for (int i = 0; i < Platforms.Count; i++)
        {
            Platform platform = Platforms[i];
            if (platform == null) continue;

            IReadOnlyList<Vector3Int> occupied = platform.OccupiedCells;
            if (occupied != null && occupied.Count > 0)
            {
                for (int j = 0; j < occupied.Count; j++)
                {
                    map[occupied[j]] = platform;
                }
            }
            else
            {
                Vector3Int rootCell = BuildingManager.Instance.grid.WorldToCell(platform.transform.position);
                map[rootCell] = platform;
            }
        }

        return map;
    }
}
