using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class BaseCarryOverManager
{
    private static bool _hasCarriedOverData = false;
    private static List<SavedBuildingData> _savedBuildings = new List<SavedBuildingData>();
    private static Dictionary<ResourceType, int> _savedPendingResources = null;

    public static bool HasCarriedOverData => _hasCarriedOverData;

    public static void Clear()
    {
        _savedBuildings.Clear();
        _savedPendingResources = null;
        _hasCarriedOverData = false;
    }

    public static void SaveBaseState()
    {
        Clear();

        MainStructure mainStructure = Object.FindFirstObjectByType<MainStructure>();
        if (mainStructure == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return;
        }

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(mainStructure.transform, out Vector3Int mainAnchor, out _))
        {
            return;
        }

        if (ResourceTransferManager.Instance != null && ResourceTransferManager.Instance.PendingResources != null)
        {
            _savedPendingResources = new Dictionary<ResourceType, int>(ResourceTransferManager.Instance.PendingResources);
        }

        BuildingPiece[] pieces = Object.FindObjectsByType<BuildingPiece>(FindObjectsSortMode.None);
        Debug.Log("[SaveBaseState] Found pieces total in scene: " + pieces.Length + ", mainAnchor: " + mainAnchor);

        foreach (BuildingPiece piece in pieces)
        {
            if (piece == null || piece.GetComponent<MainStructure>() != null)
            {
                continue;
            }

            bool isPlatform = piece.GetComponent<Platform>() != null;
            bool isConnected = false;

            if (isPlatform)
            {
                Platform plat = piece.GetComponent<Platform>();
                isConnected = plat != null && plat.IsConnectedToMainStructure();
            }
            else
            {
                isConnected = PlatformRegistry.IsBuildingOnConnectedPlatform(piece);
            }

            if (!isConnected)
            {
                continue;
            }

            if (!BuildingManager.Instance.TryGetBuildingAnchorCells(piece.transform, out Vector3Int buildingAnchor, out _))
            {
                buildingAnchor = piece.cellPosition;
            }

            Vector3Int relativeAnchor = buildingAnchor - mainAnchor;

            BuildingDataHolder holder = piece.GetComponent<BuildingDataHolder>();
            if (holder == null || holder.buildingData == null)
            {
                Debug.Log("[SaveBaseState] Holder or buildingData null for piece: " + piece.name);
                continue;
            }

            SavedBuildingData saved = new SavedBuildingData
            {
                buildingType = holder.buildingData.buildingType,
                relativeAnchor = relativeAnchor,
                currentHealth = piece.GetComponent<Damageable>() != null ? piece.GetComponent<Damageable>().currentHealth : 100
            };

            BaseStorage storage = piece.GetComponent<BaseStorage>() ?? piece.GetComponentInChildren<BaseStorage>(true);
            if (storage != null)
            {
                saved.storedResources = new Dictionary<ResourceType, int>(storage.GetStoredResources());
            }

            Processor proc = piece.GetComponent<Processor>() ?? piece.GetComponentInChildren<Processor>(true);
            if (proc != null)
            {
                saved.selectedOutputResource = proc.SelectedOutputResource;
                saved.maxProductionLimit = 0;
                if (proc.SelectedOutputResource.HasValue)
                {
                    ActiveRecipe recipe = proc.GetActiveRecipeForResource(proc.SelectedOutputResource.Value);
                    if (recipe != null)
                    {
                        saved.maxProductionLimit = recipe.maxProductionLimit;
                    }
                }

                saved.processorIngredients = new Dictionary<ResourceType, int>();
                var currentIngs = proc.GetCurrentIngredients();
                if (currentIngs != null)
                {
                    foreach (var kvp in currentIngs)
                    {
                        saved.processorIngredients[kvp.Key] = kvp.Value;
                    }
                }
            }

            _savedBuildings.Add(saved);
            Debug.Log("[SaveBaseState] Saved building: " + saved.buildingType + ", relativeAnchor: " + saved.relativeAnchor);
        }

        _hasCarriedOverData = _savedBuildings.Count > 0 || _savedPendingResources != null;
        Debug.Log("[SaveBaseState] Finished. Saved buildings count: " + _savedBuildings.Count + ", hasCarriedOverData: " + _hasCarriedOverData);
    }

    public static void RestoreBaseState()
    {
        if (!_hasCarriedOverData)
        {
            return;
        }

        _savedBuildings.Sort((a, b) =>
        {
            bool aIsPlatform = a.buildingType == BuildingType.Platform;
            bool bIsPlatform = b.buildingType == BuildingType.Platform;
            if (aIsPlatform && !bIsPlatform) return -1;
            if (!aIsPlatform && bIsPlatform) return 1;
            return 0;
        });

        MainStructure mainStructure = Object.FindFirstObjectByType<MainStructure>();
        if (mainStructure == null || BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return;
        }

        if (!BuildingManager.Instance.TryGetBuildingAnchorCells(mainStructure.transform, out Vector3Int mainAnchor, out _))
        {
            return;
        }

        MapGenerator mapGen = Object.FindFirstObjectByType<MapGenerator>();
        Tilemap lowWallTilemap = null;
        Tilemap highWallTilemap = null;
        Tilemap groundTilemap = null;
        Tilemap lowGroundTilemap = null;
        Tilemap enemyHomeTilemap = null;
        Tilemap enemyTerritoryTilemap = null;
        TileBase groundTile = null;

        if (mapGen != null)
        {
            var lowField = typeof(MapGenerator).GetField("lowWallTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var highField = typeof(MapGenerator).GetField("highWallTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var groundField = typeof(MapGenerator).GetField("groundTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lowGroundField = typeof(MapGenerator).GetField("lowGroundTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enemyHomeField = typeof(MapGenerator).GetField("enemyHomeTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enemyTerritoryField = typeof(MapGenerator).GetField("enemyTerritoryTilemap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var groundTileField = typeof(MapGenerator).GetField("groundTile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            lowWallTilemap = lowField?.GetValue(mapGen) as Tilemap;
            highWallTilemap = highField?.GetValue(mapGen) as Tilemap;
            groundTilemap = groundField?.GetValue(mapGen) as Tilemap;
            lowGroundTilemap = lowGroundField?.GetValue(mapGen) as Tilemap;
            enemyHomeTilemap = enemyHomeField?.GetValue(mapGen) as Tilemap;
            enemyTerritoryTilemap = enemyTerritoryField?.GetValue(mapGen) as Tilemap;
            groundTile = groundTileField?.GetValue(mapGen) as TileBase;
        }

        foreach (SavedBuildingData saved in _savedBuildings)
        {
            Vector3Int absoluteAnchor = mainAnchor + saved.relativeAnchor;

            BuildingData data = BuildingManager.Instance.GetBuildingDataByType(saved.buildingType);
            if (data == null)
            {
                continue;
            }

            if (saved.buildingType == BuildingType.Platform && PlatformRegistry.GetPlatformAtCell(absoluteAnchor) != null)
            {
                Platform existingPlat = PlatformRegistry.GetPlatformAtCell(absoluteAnchor);
                if (existingPlat != null)
                {
                    Damageable damageable = existingPlat.GetComponent<Damageable>();
                    if (damageable != null)
                    {
                        damageable.currentHealth = saved.currentHealth;
                    }
                }
                continue;
            }

            if (data.recipe != null)
            {
                foreach (var pieceRecipe in data.recipe)
                {
                    Vector3Int targetPos = absoluteAnchor + pieceRecipe.relativePosition;

                    if (lowWallTilemap != null && lowWallTilemap.HasTile(targetPos)) lowWallTilemap.SetTile(targetPos, null);
                    if (highWallTilemap != null && highWallTilemap.HasTile(targetPos)) highWallTilemap.SetTile(targetPos, null);
                    if (enemyHomeTilemap != null && enemyHomeTilemap.HasTile(targetPos)) enemyHomeTilemap.SetTile(targetPos, null);
                    if (enemyTerritoryTilemap != null && enemyTerritoryTilemap.HasTile(targetPos)) enemyTerritoryTilemap.SetTile(targetPos, null);
                    if (groundTilemap != null && groundTile != null && !groundTilemap.HasTile(targetPos)) groundTilemap.SetTile(targetPos, groundTile);

                    var resources = new List<ResourceNode>(ResourceManager.Instance.GetAllResources());
                    foreach (ResourceNode node in resources)
                    {
                        if (node != null && node.cellPosition == targetPos)
                        {
                            Object.Destroy(node.gameObject);
                        }
                    }
                }
            }

            BuildingManager.Instance.CreateBuilding(absoluteAnchor, data);

            BuildingPiece piece = BuildingManager.Instance.GetPieceAt(absoluteAnchor);
            if (piece != null)
            {
                GameObject obj = piece.gameObject;

                Damageable damageable = obj.GetComponent<Damageable>();
                if (damageable != null)
                {
                    damageable.currentHealth = saved.currentHealth;
                }

                BaseStorage storage = obj.GetComponent<BaseStorage>() ?? obj.GetComponentInChildren<BaseStorage>(true);
                if (storage != null && saved.storedResources != null)
                {
                    foreach (var kvp in saved.storedResources)
                    {
                        storage.ForceAddResource(kvp.Key, kvp.Value);
                    }
                }

                Processor proc = obj.GetComponent<Processor>() ?? obj.GetComponentInChildren<Processor>(true);
                if (proc != null)
                {
                    proc.SetSelectedOutputResource(saved.selectedOutputResource);
                    if (saved.selectedOutputResource.HasValue)
                    {
                        ActiveRecipe recipe = proc.GetActiveRecipeForResource(saved.selectedOutputResource.Value);
                        if (recipe != null)
                        {
                            recipe.maxProductionLimit = saved.maxProductionLimit;
                        }
                    }
                    if (saved.processorIngredients != null)
                    {
                        proc.RestoreIngredients(saved.processorIngredients);
                    }
                }
            }
        }

        if (_savedPendingResources != null)
        {
            foreach (var kvp in _savedPendingResources)
            {
                if (kvp.Value > 0)
                {
                    mainStructure.ForceAddResource(kvp.Key, kvp.Value);
                }
            }
            ResourceDataManager.Instance.RecalculateResourceCountsFromStorages();
            mainStructure.UpdateStorageUI();
        }

        Clear();
    }
}

[System.Serializable]
public class SavedBuildingData
{
    public BuildingType buildingType;
    public Vector3Int relativeAnchor;
    public int currentHealth;
    public Dictionary<ResourceType, int> storedResources;
    public ResourceType? selectedOutputResource;
    public int maxProductionLimit;
    public Dictionary<ResourceType, int> processorIngredients;
}
