using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BuildingManager : MonoBehaviour
{
    [Header("References")]
    public Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap resourceTilemap;
    [SerializeField] private Tilemap buildingTilemap;
    [SerializeField] private TileBase mainStructureTile;
    [SerializeField] private TileBase temporaryTile;
    [SerializeField] private Transform parentTransform;

    private readonly Dictionary<Vector3Int, BuildingStructure> _buildingStructuresByAnchor = new Dictionary<Vector3Int, BuildingStructure>();
    private readonly Dictionary<Vector3Int, BuildingStructure> _cellToStructureMap = new Dictionary<Vector3Int, BuildingStructure>();
    private readonly Dictionary<GadgetType, TileBase> _gadgetTypeToTileCache = new Dictionary<GadgetType, TileBase>();
    private readonly HashSet<Vector3Int> _mainStructureCells = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, BuildingPiece> _placedPieces = new Dictionary<Vector3Int, BuildingPiece>();

    private readonly List<Processor> _processors = new List<Processor>();
    private readonly HashSet<Vector3Int> _temporaryTiles = new HashSet<Vector3Int>();

    private MapGenerator _cachedMapGenerator;

    [Header("Combo Buildings")]
    private List<ComboCardData> _comboCardDataList;

    public Tilemap GroundTilemap {
        get {
            return groundTilemap;
        }
    }

    public static BuildingManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }

        if (parentTransform == null) {
            parentTransform = transform;
        }

        LoadAllComboCards();
        CacheAllGadgetTiles();
        CacheMapGenerator();
    }

    public static event Action<Vector3Int> OnTilemapChanged;

    private void CacheMapGenerator()
    {
        _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
    }

    private MapGenerator GetMapGenerator()
    {
        // Lazy initialization
        if (_cachedMapGenerator == null) {
            _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
        }
        return _cachedMapGenerator;
    }

    private void LoadAllComboCards()
    {
        _comboCardDataList = new List<ComboCardData>(
            Resources.LoadAll<ComboCardData>("Combo Cards"));
    }

    private void CacheAllGadgetTiles()
    {
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");

        foreach (CardData card in allCards) {
            if (card.gadgetType != GadgetType.None && card.gadgetTile != null) {
                if (!_gadgetTypeToTileCache.ContainsKey(card.gadgetType)) {
                    _gadgetTypeToTileCache[card.gadgetType] = card.gadgetTile;
                }
            }
        }
    }

    public bool IsResourceTile(Vector3Int cellPosition)
    {
        if (resourceTilemap == null) return false;
        return resourceTilemap.HasTile(cellPosition);
    }

    public bool IsBuildingTile(Vector3Int cellPosition)
    {
        if (buildingTilemap == null) return false;
        return buildingTilemap.HasTile(cellPosition);
    }

    public bool IsTerrainCell(Vector3Int cellPosition)
    {
        MapGenerator mapGenerator = GetMapGenerator();
        if (mapGenerator != null)
        {
            // First check wall tilemap for terrain tiles (walls block movement)
            if (mapGenerator.IsTerrainCell(cellPosition))
            {
                return true;
            }
            
            // Also check groundTilemap for any terrain tiles (for backward compatibility)
            // In the new system, groundTilemap should only have ground tiles, but check anyway
            if (groundTilemap != null)
            {
                TileBase tile = groundTilemap.GetTile(cellPosition);
                if (tile != null && mapGenerator.IsTerrainTile(tile))
                {
                    return true;
                }
            }
        }

        return false;
    }


    public bool CanPlaceBuilding(Vector3Int cellPosition)
    {
        if (grid == null || groundTilemap == null ||
            resourceTilemap == null || buildingTilemap == null) {
            Debug.LogError("BuildingManager: Missing references.");
            return false;
        }

        if (!groundTilemap.HasTile(cellPosition)) return false;

        if (IsTerrainCell(cellPosition)) return false;
        if (resourceTilemap.HasTile(cellPosition)) return false;
        if (buildingTilemap.HasTile(cellPosition)) return false;
        if (IsTemporaryTile(cellPosition)) return false;
        if (GetPieceAt(cellPosition) != null) return false;
        if (IsMainStructureCell(cellPosition)) return false;

        if (FogOfWarManager.Instance != null && !FogOfWarManager.Instance.CanPlaceBuilding(cellPosition)) {
            return false;
        }

        return true;
    }

    public bool IsTemporaryTile(Vector3Int cellPosition)
    {
        return _temporaryTiles.Contains(cellPosition);
    }

    private bool IsMainStructureCell(Vector3Int cellPosition)
    {
        return _mainStructureCells.Contains(cellPosition);
    }

    public ConstructionSite CreateComboConstructionSite(ComboCardData comboCardData, Vector3Int anchorCellPosition)
    {
        if (comboCardData == null || comboCardData.recipe == null || comboCardData.recipe.Count == 0) {
            Debug.LogWarning("[BuildingManager] Invalid ComboCardData for construction site");
            return null;
        }

        foreach (ComboCardData.ComboPiece piece in comboCardData.recipe) {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (!CanPlaceBuilding(cellPos)) {
                Debug.LogWarning($"[BuildingManager] Cannot create combo construction site - cell {cellPos} is not available");
                return null;
            }
        }

        if (buildingTilemap != null) {
            buildingTilemap.gameObject.SetActive(true);

            if (temporaryTile == null) {
                Debug.LogWarning("[BuildingManager] Temporary tile is not assigned! Construction tiles will not be visible.");
            }
        }
        else {
            Debug.LogError("[BuildingManager] Building tilemap is null! Cannot place temporary tiles.");
            return null;
        }

        foreach (ComboCardData.ComboPiece piece in comboCardData.recipe) {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (temporaryTile != null && buildingTilemap != null) {
                buildingTilemap.SetTile(cellPos, temporaryTile);
                _temporaryTiles.Add(cellPos);
                OnTilemapChanged?.Invoke(cellPos);

                // Debug.Log($"[BuildingManager] Placed temporary tile at {cellPos}");
            }
        }

        if (buildingTilemap != null) {
            buildingTilemap.RefreshAllTiles();
            Debug.Log($"[BuildingManager] Refreshed tilemap after placing {comboCardData.recipe.Count} temporary tiles");
        }

        Vector3 worldPosition = grid.GetCellCenterWorld(anchorCellPosition);
        GameObject siteObject = new GameObject($"ComboConstructionSite_{anchorCellPosition}");
        siteObject.transform.position = worldPosition;
        siteObject.transform.SetParent(parentTransform);

        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.comboCardData = comboCardData;
        site.cellPosition = anchorCellPosition;

        site.CalculateComboCosts();

        if (ConstructionManager.Instance == null) {
            ConstructionManager existingManager = FindFirstObjectByType<ConstructionManager>();
            if (existingManager == null) {
                GameObject managerObj = new GameObject("ConstructionManager");
                managerObj.AddComponent<ConstructionManager>();
                Debug.LogWarning("[BuildingManager] Auto-created ConstructionManager GameObject");
            }
        }

        if (ConstructionManager.Instance != null) {
            ConstructionManager.Instance.RegisterConstructionSite(site);
            Debug.Log($"[BuildingManager] Registered construction site for {comboCardData.displayName} at {anchorCellPosition}");
        }
        else {
            Debug.LogError("[BuildingManager] Failed to register construction site: ConstructionManager.Instance is null");
        }
        return site;
    }

    private void PlaceBuildingPiece(CardData cardData, Vector3Int cellPosition)
    {
        bool wasTemporaryTile = _temporaryTiles.Contains(cellPosition);

        if (wasTemporaryTile) {
            _temporaryTiles.Remove(cellPosition);
            if (buildingTilemap.HasTile(cellPosition) && buildingTilemap.GetTile(cellPosition) == temporaryTile) {
                buildingTilemap.SetTile(cellPosition, null);
                OnTilemapChanged?.Invoke(cellPosition);
            }
        }

        bool canPlace = (!buildingTilemap.HasTile(cellPosition) || wasTemporaryTile) &&
            groundTilemap.HasTile(cellPosition) &&
            !resourceTilemap.HasTile(cellPosition);

        if (canPlace) {
            Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
            GameObject newPieceObject =
                Instantiate(cardData.buildingPrefab, worldPosition, Quaternion.identity, parentTransform);

            BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
            if (pieceComponent != null) {
                pieceComponent.gadgetType = cardData.gadgetType;
                pieceComponent.cellPosition = cellPosition;
                _placedPieces[cellPosition] = pieceComponent;
            }

            BuildingStructure structure = new BuildingStructure {
                anchor = cellPosition,
                size = Vector2Int.one
            };
            structure.occupiedCells.Add(cellPosition);
            RegisterBuildingStructure(structure);

            if (cardData.gadgetTile != null) {
                buildingTilemap.SetTile(cellPosition, cardData.gadgetTile);
                OnTilemapChanged?.Invoke(cellPosition);
            }

            CheckForComboBuildings(cellPosition);
        }
    }

    public void PlaceBuildingPieceAtCell(ComboCardData comboCardData, Vector3Int pieceCell, Vector3Int anchorCellPosition)
    {
        if (comboCardData == null || comboCardData.recipe == null) return;

        foreach (ComboCardData.ComboPiece piece in comboCardData.recipe) {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (cellPos == pieceCell) {
                CardData[] allCards = Resources.LoadAll<CardData>("Cards");
                foreach (CardData card in allCards) {
                    if (card.gadgetType == piece.gadgetType) {
                        if (_temporaryTiles.Contains(pieceCell)) {
                            _temporaryTiles.Remove(pieceCell);
                            if (buildingTilemap.HasTile(pieceCell) && buildingTilemap.GetTile(pieceCell) == temporaryTile) {
                                buildingTilemap.SetTile(pieceCell, null);
                                OnTilemapChanged?.Invoke(pieceCell);
                            }
                        }

                        PlaceBuildingPiecePrefabOnly(card, pieceCell);

                        CheckForComboBuildings(pieceCell);
                        return;
                    }
                }
            }
        }
    }

    private void PlaceBuildingPiecePrefabOnly(CardData cardData, Vector3Int cellPosition)
    {
        Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
        GameObject newPieceObject =
            Instantiate(cardData.buildingPrefab, worldPosition, Quaternion.identity, parentTransform);

        BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
        if (pieceComponent != null) {
            pieceComponent.gadgetType = cardData.gadgetType;
            pieceComponent.cellPosition = cellPosition;
            _placedPieces[cellPosition] = pieceComponent;
        }

        BuildingStructure structure = new BuildingStructure {
            anchor = cellPosition,
            size = Vector2Int.one
        };
        structure.occupiedCells.Add(cellPosition);
        RegisterBuildingStructure(structure);
    }

    public void CreateComboBuildingFromConstruction(Vector3Int originPos, ComboCardData comboData)
    {
        List<Vector3Int> recipePositions = new List<Vector3Int>();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (ComboCardData.ComboPiece piece in comboData.recipe) {
            Vector3Int relativePos = piece.relativePosition;
            recipePositions.Add(originPos + relativePos);

            minX = Mathf.Min(minX, relativePos.x);
            minY = Mathf.Min(minY, relativePos.y);
            maxX = Mathf.Max(maxX, relativePos.x);
            maxY = Mathf.Max(maxY, relativePos.y);
        }

        Vector2Int size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);

        BuildingStructure comboStructure = new BuildingStructure {
            anchor = originPos,
            size = size
        };

        foreach (Vector3Int targetPos in recipePositions) {
            RemoveBuildingPieceAtPosition(targetPos);
            comboStructure.occupiedCells.Add(targetPos);
        }

        RegisterBuildingStructure(comboStructure);

        Vector3 worldPos = grid.GetCellCenterWorld(originPos);
        GameObject newComboPieceObject = Instantiate(comboData.comboPrefab, worldPos, Quaternion.identity, parentTransform);

        BuildingPiece mainPiece = newComboPieceObject.GetComponent<BuildingPiece>();
        if (mainPiece == null) {
            mainPiece = newComboPieceObject.AddComponent<BuildingPiece>();
        }

        mainPiece.cellPosition = originPos;

        foreach (Vector3Int targetPos in recipePositions) {
            _placedPieces[targetPos] = mainPiece;
        }

        HandleComboBuildingLogic(newComboPieceObject, comboData);

        Debug.Log($"Combo Building '{comboData.displayName}' Created (prefab only, no tiles)");
    }

    // public void PlaceComboBuilding(ComboCardData comboCardData, Vector3Int anchorCellPosition)
    // {
    //     if (comboCardData == null || comboCardData.recipe == null) return;
    //
    //     foreach (ComboCardData.ComboPiece piece in comboCardData.recipe) {
    //         Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
    //         if (_temporaryTiles.Contains(cellPos)) {
    //             _temporaryTiles.Remove(cellPos);
    //             if (buildingTilemap.HasTile(cellPos) && buildingTilemap.GetTile(cellPos) == temporaryTile) {
    //                 buildingTilemap.SetTile(cellPos, null);
    //                 OnTilemapChanged?.Invoke(cellPos);
    //             }
    //         }
    //     }
    //
    //     if (buildingTilemap != null) {
    //         buildingTilemap.RefreshAllTiles();
    //     }
    //
    //     CardData[] allCards = Resources.LoadAll<CardData>("Cards");
    //     Dictionary<GadgetType, CardData> gadgetToCardMap = new Dictionary<GadgetType, CardData>();
    //
    //     foreach (CardData card in allCards) {
    //         if (card.gadgetType != GadgetType.None && !gadgetToCardMap.ContainsKey(card.gadgetType)) {
    //             gadgetToCardMap[card.gadgetType] = card;
    //         }
    //     }
    //
    //     foreach (ComboCardData.ComboPiece piece in comboCardData.recipe) {
    //         Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
    //
    //         if (gadgetToCardMap.TryGetValue(piece.gadgetType, out CardData pieceCard)) {
    //             // Place the building piece
    //             PlaceBuildingPiece(pieceCard, cellPos);
    //         }
    //     }
    // }

    // public void PlaceBuilding(CardData cardData, Vector3Int cellPosition)
    // {
    //     if (CanPlaceBuilding(cellPosition)) {
    //         Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
    //         GameObject newPieceObject =
    //             Instantiate(cardData.buildingPrefab, worldPosition, Quaternion.identity, parentTransform);
    //
    //         BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
    //         if (pieceComponent != null) {
    //             pieceComponent.gadgetType = cardData.gadgetType;
    //             pieceComponent.cellPosition = cellPosition;
    //             _placedPieces[cellPosition] = pieceComponent;
    //         }
    //
    //         BuildingStructure structure = new BuildingStructure {
    //             anchor = cellPosition,
    //             size = Vector2Int.one
    //         };
    //         structure.occupiedCells.Add(cellPosition);
    //         RegisterBuildingStructure(structure);
    //
    //         if (cardData.gadgetTile != null) {
    //             buildingTilemap.SetTile(cellPosition, cardData.gadgetTile);
    //             OnTilemapChanged?.Invoke(cellPosition);
    //         }
    //
    //         CheckForComboBuildings(cellPosition);
    //     }
    // }

    private void CheckForComboBuildings(Vector3Int placedPos)
    {
        if (_comboCardDataList == null || _comboCardDataList.Count == 0) return;

        foreach (ComboCardData comboData in _comboCardDataList) {
            if (CheckPatternAround(placedPos, comboData)) {
                return;
            }
        }
    }

    private bool CheckPatternAround(Vector3Int placedPos, ComboCardData comboData)
    {
        foreach (ComboCardData.ComboPiece piece in comboData.recipe) {
            Vector3Int originPosCandidate = placedPos - piece.relativePosition;

            if (CheckPattern(originPosCandidate, comboData)) {
                CreateComboBuilding(originPosCandidate, comboData);
                return true;
            }
        }
        return false;
    }

    private bool CheckPattern(Vector3Int originPos, ComboCardData comboData)
    {
        foreach (ComboCardData.ComboPiece piece in comboData.recipe) {
            Vector3Int targetPos = originPos + piece.relativePosition;
            TileBase targetTile = buildingTilemap.GetTile(targetPos);

            if (!_gadgetTypeToTileCache.TryGetValue(piece.gadgetType, out TileBase requiredTile) ||
                targetTile != requiredTile) {
                return false;
            }
        }
        return true;
    }

    private void CreateComboBuilding(Vector3Int originPos, ComboCardData comboData)
    {
        List<Vector3Int> recipePositions = new List<Vector3Int>();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (ComboCardData.ComboPiece piece in comboData.recipe) {
            Vector3Int relativePos = piece.relativePosition;
            recipePositions.Add(originPos + relativePos);

            minX = Mathf.Min(minX, relativePos.x);
            minY = Mathf.Min(minY, relativePos.y);
            maxX = Mathf.Max(maxX, relativePos.x);
            maxY = Mathf.Max(maxY, relativePos.y);
        }

        Vector2Int size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);

        BuildingStructure comboStructure = new BuildingStructure {
            anchor = originPos, // originPos : 좌하단 기준점
            size = size
        };

        foreach (Vector3Int targetPos in recipePositions) {
            RemoveBuildingPieceAtPosition(targetPos);
            comboStructure.occupiedCells.Add(targetPos);
        }

        RegisterBuildingStructure(comboStructure);

        Vector3 worldPos = grid.GetCellCenterWorld(originPos);
        GameObject newComboPieceObject = Instantiate(comboData.comboPrefab, worldPos, Quaternion.identity, parentTransform);

        BuildingPiece mainPiece = newComboPieceObject.GetComponent<BuildingPiece>();
        if (mainPiece == null) {
            mainPiece = newComboPieceObject.AddComponent<BuildingPiece>();
        }

        mainPiece.cellPosition = originPos;

        foreach (Vector3Int targetPos in recipePositions) {
            _placedPieces[targetPos] = mainPiece;

            if (comboData.comboTile != null) {
                buildingTilemap.SetTile(targetPos, comboData.comboTile);
            }
        }

        HandleComboBuildingLogic(newComboPieceObject, comboData);

        foreach (Vector3Int targetPos in recipePositions) {
            OnTilemapChanged?.Invoke(targetPos);
        }

        Debug.Log($"Combo Building '{comboData.displayName}' Created");
    }

    private void HandleComboBuildingLogic(GameObject comboObject, ComboCardData comboData)
    {
        if (comboObject == null || ResourceManager.Instance == null) {
            return;
        }

        switch (comboData.comboType) {
        case ComboType.Storage:
            IStorage storage = comboObject.GetComponent<IStorage>();
            if (storage != null) {
                ResourceManager.Instance.AddStorage(storage);
            }
            break;
        case ComboType.Battery:
            IStorage battery = comboObject.GetComponent<IStorage>();
            if (battery != null) {
                ResourceManager.Instance.AddStorage(battery);
            }
            break;
        case ComboType.Generator:
            ResourceGenerator generator = comboObject.GetComponent<ResourceGenerator>();
            if (generator != null)
            {
                generator.SetConstructed();
            }
            break;
        case ComboType.Turret:
        case ComboType.Radar:
            break;
        }
    }

    public void ClearBuildingDataAt(Vector3Int cellPosition)
    {
        if (buildingTilemap == null) return;

        if (_cellToStructureMap.TryGetValue(cellPosition, out BuildingStructure structure)) {
            Vector3Int anchor = structure.anchor;
            List<Vector3Int> cellsToClear = new List<Vector3Int>(structure.occupiedCells);

            UnregisterBuildingStructure(anchor);

            foreach (Vector3Int cell in cellsToClear) {
                if (_placedPieces.Remove(cell, out BuildingPiece piece)) {
                    if (piece != null) Destroy(piece.gameObject);
                }

                if (buildingTilemap.HasTile(cell)) {
                    buildingTilemap.SetTile(cell, null);
                    OnTilemapChanged?.Invoke(cell);
                }
            }
        }
        else if (buildingTilemap.HasTile(cellPosition)) {
            buildingTilemap.SetTile(cellPosition, null);
            OnTilemapChanged?.Invoke(cellPosition);
            _placedPieces.Remove(cellPosition);
        }
    }

    public BuildingPiece GetPieceAt(Vector3Int cellPosition)
    {
        _placedPieces.TryGetValue(cellPosition, out BuildingPiece piece);
        return piece;
    }

    private void RemoveBuildingPieceAtPosition(Vector3Int cellPos)
    {
        UnregisterBuildingStructure(cellPos);

        if (_placedPieces.Remove(cellPos, out BuildingPiece piece)) {
            if (piece != null) {
                Destroy(piece.gameObject);
            }
        }
        buildingTilemap.SetTile(cellPos, null);
    }

    public bool GetBuildingAt(Vector3Int cell, out List<Vector3Int> occupiedCells)
    {
        if (_cellToStructureMap.TryGetValue(cell, out BuildingStructure structure)) {
            occupiedCells = structure.occupiedCells;
            return true;
        }

        occupiedCells = null;
        return false;
    }

    private void RegisterBuildingStructure(BuildingStructure structure)
    {
        if (_buildingStructuresByAnchor.ContainsKey(structure.anchor)) {
            Debug.LogWarning($"Building structure already exists at anchor {structure.anchor}. Overwriting.");
            UnregisterBuildingStructure(structure.anchor);
        }

        _buildingStructuresByAnchor[structure.anchor] = structure;
        foreach (Vector3Int cell in structure.occupiedCells) {
            _cellToStructureMap[cell] = structure;
        }
    }

    public void RegisterMainStructure(Vector3Int anchorCell, Vector2Int size)
    {
        BuildingStructure structure = new BuildingStructure {
            anchor = anchorCell,
            size = size
        };

        for (int x = 0; x < size.x; x++) {
            for (int y = 0; y < size.y; y++) {
                Vector3Int cell = anchorCell + new Vector3Int(x, y, 0);
                structure.occupiedCells.Add(cell);
                _mainStructureCells.Add(cell);
            }
        }

        RegisterBuildingStructure(structure);

        foreach (Vector3Int cell in structure.occupiedCells) {
            OnTilemapChanged?.Invoke(cell);
        }
    }

    private void UnregisterBuildingStructure(Vector3Int anchor)
    {
        if (_buildingStructuresByAnchor.Remove(anchor, out BuildingStructure structure)) {
            foreach (Vector3Int cell in structure.occupiedCells) {
                _cellToStructureMap.Remove(cell);
            }
        }
    }

    public void RemoveResourceTile(Vector3Int cellPosition)
    {
        if (resourceTilemap != null) {
            resourceTilemap.SetTile(cellPosition, null);
        }
    }

    public void RegisterProcessor(Processor processor)
    {
        if (!_processors.Contains(processor)) {
            _processors.Add(processor);
        }
    }

    public void UnregisterProcessor(Processor processor)
    {
        _processors.Remove(processor);
    }

    public Processor FindClosestAvailableProcessor(Vector3 position)
    {
        Processor closestProcessor = null;
        float minDistance = float.MaxValue;

        foreach (Processor processor in _processors) {
            if (processor.IsFull) continue;

            float dist = Vector3.Distance(position, processor.GetPosition());
            if (dist < minDistance) {
                minDistance = dist;
                closestProcessor = processor;
            }
        }
        return closestProcessor;
    }

    public void RegisterPrePlacedBuilding(BuildingPiece buildingPiece)
    {
        if (buildingPiece == null || grid == null) return;

        Vector3Int cellPosition = buildingPiece.cellPosition;
        if (cellPosition == Vector3Int.zero && buildingPiece.transform != null) {
            cellPosition = grid.WorldToCell(buildingPiece.transform.position);
            buildingPiece.cellPosition = cellPosition;
        }

        if (_placedPieces.ContainsKey(cellPosition)) {
            return;
        }

        _placedPieces[cellPosition] = buildingPiece;

        BuildingStructure structure = new BuildingStructure {
            anchor = cellPosition,
            size = Vector2Int.one
        };
        structure.occupiedCells.Add(cellPosition);
        RegisterBuildingStructure(structure);

        if (buildingPiece.gadgetType != GadgetType.None &&
            _gadgetTypeToTileCache.TryGetValue(buildingPiece.gadgetType, out TileBase tile)) {
            if (buildingTilemap != null && !buildingTilemap.HasTile(cellPosition)) {
                buildingTilemap.SetTile(cellPosition, tile);
                OnTilemapChanged?.Invoke(cellPosition);
            }
        }

        Debug.Log($"[BuildingManager] Registered pre-placed building at {cellPosition} (Type: {buildingPiece.gadgetType})");
    }

    private class BuildingStructure
    {
        public readonly List<Vector3Int> occupiedCells = new List<Vector3Int>();
        public Vector3Int anchor;
        public Vector2Int size;
    }
}
