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
    [SerializeField] private TileBase temporaryTile; // Tile for construction sites
    [SerializeField] private Transform parentTransform;
    
    [Header("Combo Buildings")]
    private List<ComboCardData> _comboCardDataList;
    private readonly Dictionary<GadgetType, TileBase> _gadgetTypeToTileCache = new ();
    private readonly Dictionary<Vector3Int, BuildingPiece> _placedPieces = new ();
    
    private readonly Dictionary<Vector3Int, BuildingStructure> _buildingStructuresByAnchor = new ();
    private readonly Dictionary<Vector3Int, BuildingStructure> _cellToStructureMap = new ();
    private readonly HashSet<Vector3Int> _temporaryTiles = new HashSet<Vector3Int>();
   
    private readonly List<Processor> _processors = new List<Processor>();
    
    public static event Action<Vector3Int> OnTilemapChanged;
    
    public static BuildingManager Instance { get; private set; }
    
    public class BuildingStructure
    {
        public Vector3Int anchor;
        public Vector2Int size;
        public readonly List<Vector3Int> occupiedCells = new ();
    }

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }

        if (parentTransform == null)
        {
            parentTransform = transform;
        }

        LoadAllComboCards();
        CacheAllGadgetTiles();
    }
    
    private void LoadAllComboCards()
    {
        _comboCardDataList = new List<ComboCardData>(
            Resources.LoadAll<ComboCardData>("Combo Cards"));
    }
    
    private void CacheAllGadgetTiles()
    {
        CardData[] allCards = Resources.LoadAll<CardData>("Cards"); 
        
        foreach (var card in allCards)
        {
            if (card.gadgetType != GadgetType.None && card.gadgetTile != null)
            {
                if (!_gadgetTypeToTileCache.ContainsKey(card.gadgetType))
                {
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

    public bool CanPlaceBuilding(Vector3Int cellPosition)
    {
        if (grid == null || groundTilemap == null || 
            resourceTilemap == null || buildingTilemap == null) {
            Debug.LogError("BuildingManager: Missing references.");
            return false;
        }

        if (!groundTilemap.HasTile(cellPosition)) return false;
        if (resourceTilemap.HasTile(cellPosition)) return false;
        if (buildingTilemap.HasTile(cellPosition)) return false;
        if (IsTemporaryTile(cellPosition)) return false; // Can't place on construction site
        if (GetPieceAt(cellPosition) != null) return false; // Can't place where a building piece already exists

        return true;
    }
    
    public bool IsTemporaryTile(Vector3Int cellPosition)
    {
        return _temporaryTiles.Contains(cellPosition);
    }
    
    public ConstructionSite CreateComboConstructionSite(ComboCardData comboCardData, Vector3Int anchorCellPosition)
    {
        if (comboCardData == null || comboCardData.recipe == null || comboCardData.recipe.Count == 0)
        {
            Debug.LogWarning($"[BuildingManager] Invalid ComboCardData for construction site");
            return null;
        }
        
        // Check if all cells in the recipe pattern can be placed
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (!CanPlaceBuilding(cellPos))
            {
                Debug.LogWarning($"[BuildingManager] Cannot create combo construction site - cell {cellPos} is not available");
                return null;
            }
        }
        
        // Ensure building tilemap is active and visible
        if (buildingTilemap != null)
        {
            buildingTilemap.gameObject.SetActive(true);
            
            if (temporaryTile == null)
            {
                Debug.LogWarning("[BuildingManager] Temporary tile is not assigned! Construction tiles will not be visible.");
            }
        }
        else
        {
            Debug.LogError("[BuildingManager] Building tilemap is null! Cannot place temporary tiles.");
            return null;
        }
        
        // Create temporary tiles for all cells in the pattern
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (temporaryTile != null && buildingTilemap != null)
            {
                buildingTilemap.SetTile(cellPos, temporaryTile);
                _temporaryTiles.Add(cellPos);
                OnTilemapChanged?.Invoke(cellPos);
                
                Debug.Log($"[BuildingManager] Placed temporary tile at {cellPos}");
            }
        }
        
        // Refresh the tilemap to ensure tiles are visible
        if (buildingTilemap != null)
        {
            buildingTilemap.RefreshAllTiles();
            Debug.Log($"[BuildingManager] Refreshed tilemap after placing {comboCardData.recipe.Count} temporary tiles");
        }
        
        // Create construction site GameObject at the anchor position
        Vector3 worldPosition = grid.GetCellCenterWorld(anchorCellPosition);
        GameObject siteObject = new GameObject($"ComboConstructionSite_{anchorCellPosition}");
        siteObject.transform.position = worldPosition;
        siteObject.transform.SetParent(parentTransform);
        
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.comboCardData = comboCardData;
        site.cellPosition = anchorCellPosition;
        
        // Calculate costs immediately after setting comboCardData
        // This ensures _requiredResources is populated before registration
        site.CalculateComboCosts();
        
        // Ensure ConstructionManager exists before registering
        if (ConstructionManager.Instance == null)
        {
            // Try to find it in the scene
            ConstructionManager existingManager = FindFirstObjectByType<ConstructionManager>();
            if (existingManager == null)
            {
                // Auto-create ConstructionManager if it doesn't exist
                GameObject managerObj = new GameObject("ConstructionManager");
                managerObj.AddComponent<ConstructionManager>();
                Debug.LogWarning("[BuildingManager] Auto-created ConstructionManager GameObject");
            }
        }
        
        // Register with ConstructionManager
        if (ConstructionManager.Instance != null)
        {
            ConstructionManager.Instance.RegisterConstructionSite(site);
            Debug.Log($"[BuildingManager] Registered construction site for {comboCardData.displayName} at {anchorCellPosition}");
        }
        else
        {
            Debug.LogError($"[BuildingManager] Failed to register construction site: ConstructionManager.Instance is null");
        }
        return site;
    }
    
    public void PlaceBuildingPiece(CardData cardData, Vector3Int cellPosition)
    {
        // Check if this is a temporary tile location
        bool wasTemporaryTile = _temporaryTiles.Contains(cellPosition);
        
        // Remove temporary tile if it exists
        if (wasTemporaryTile)
        {
            _temporaryTiles.Remove(cellPosition);
            if (buildingTilemap.HasTile(cellPosition) && buildingTilemap.GetTile(cellPosition) == temporaryTile)
            {
                buildingTilemap.SetTile(cellPosition, null);
                OnTilemapChanged?.Invoke(cellPosition);
            }
        }
        
        // Place the actual building (allow placing on temporary tile locations or empty cells)
        bool canPlace = (!buildingTilemap.HasTile(cellPosition) || wasTemporaryTile) &&
                        groundTilemap.HasTile(cellPosition) &&
                        !resourceTilemap.HasTile(cellPosition);
        
        if (canPlace)
        {
            Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
            GameObject newPieceObject = 
                Instantiate(cardData.buildingPrefab, worldPosition, Quaternion.identity, parentTransform);

            BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
            if (pieceComponent != null)
            {
                pieceComponent.gadgetType = cardData.gadgetType;
                pieceComponent.cellPosition = cellPosition;
                _placedPieces[cellPosition] = pieceComponent;
            }
            
            BuildingStructure structure = new BuildingStructure
            {
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
        
        // Find which piece this cell corresponds to
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (cellPos == pieceCell)
            {
                // Load CardData for this gadget type
                CardData[] allCards = Resources.LoadAll<CardData>("Cards");
                foreach (var card in allCards)
                {
                    if (card.gadgetType == piece.gadgetType)
                    {
                        // Remove temporary tile if it exists
                        if (_temporaryTiles.Contains(pieceCell))
                        {
                            _temporaryTiles.Remove(pieceCell);
                            if (buildingTilemap.HasTile(pieceCell) && buildingTilemap.GetTile(pieceCell) == temporaryTile)
                            {
                                buildingTilemap.SetTile(pieceCell, null);
                                OnTilemapChanged?.Invoke(pieceCell);
                            }
                        }
                        
                        // Place the building piece prefab (no tile)
                        PlaceBuildingPiecePrefabOnly(card, pieceCell);
                        
                        // Check for combo building after placing this piece
                        CheckForComboBuildings(pieceCell);
                        return;
                    }
                }
            }
        }
    }
    
    private void PlaceBuildingPiecePrefabOnly(CardData cardData, Vector3Int cellPosition)
    {
        // Place only the prefab, no tile
        Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
        GameObject newPieceObject = 
            Instantiate(cardData.buildingPrefab, worldPosition, Quaternion.identity, parentTransform);

        BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
        if (pieceComponent != null)
        {
            pieceComponent.gadgetType = cardData.gadgetType;
            pieceComponent.cellPosition = cellPosition;
            _placedPieces[cellPosition] = pieceComponent;
        }
        
        BuildingStructure structure = new BuildingStructure
        {
            anchor = cellPosition,
            size = Vector2Int.one
        };
        structure.occupiedCells.Add(cellPosition);
        RegisterBuildingStructure(structure);
        
        // Note: Not placing tile here - only prefab
    }
    
    public void CreateComboBuildingFromConstruction(Vector3Int originPos, ComboCardData comboData)
    {
        // Similar to CreateComboBuilding but only spawns prefab, no tiles
        List<Vector3Int> recipePositions = new List<Vector3Int>();
        
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var piece in comboData.recipe)
        {
            Vector3Int relativePos = piece.relativePosition;
            recipePositions.Add(originPos + relativePos);
            
            minX = Mathf.Min(minX, relativePos.x);
            minY = Mathf.Min(minY, relativePos.y);
            maxX = Mathf.Max(maxX, relativePos.x);
            maxY = Mathf.Max(maxY, relativePos.y);
        }
        
        Vector2Int size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        
        BuildingStructure comboStructure = new BuildingStructure
        {
            anchor = originPos,
            size = size
        };

        foreach (var targetPos in recipePositions)
        {
            RemoveBuildingPieceAtPosition(targetPos);
            comboStructure.occupiedCells.Add(targetPos);
        }
        
        RegisterBuildingStructure(comboStructure);
        
        Vector3 worldPos = grid.GetCellCenterWorld(originPos);
        GameObject newComboPieceObject = Instantiate(comboData.comboPrefab, worldPos, Quaternion.identity, parentTransform);
        
        BuildingPiece mainPiece = newComboPieceObject.GetComponent<BuildingPiece>();
        if (mainPiece == null)
        {
            mainPiece = newComboPieceObject.AddComponent<BuildingPiece>();
        }

        mainPiece.cellPosition = originPos;

        foreach (var targetPos in recipePositions)
        {
            _placedPieces[targetPos] = mainPiece;
            // Note: Not placing combo tiles here - only prefab
        }
        
        HandleComboBuildingLogic(newComboPieceObject, comboData);

        Debug.Log($"Combo Building '{comboData.displayName}' Created (prefab only, no tiles)");
    }
    
    public void PlaceComboBuilding(ComboCardData comboCardData, Vector3Int anchorCellPosition)
    {
        if (comboCardData == null || comboCardData.recipe == null) return;
        
        // Remove temporary tiles for all cells in the pattern
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (_temporaryTiles.Contains(cellPos))
            {
                _temporaryTiles.Remove(cellPos);
                if (buildingTilemap.HasTile(cellPos) && buildingTilemap.GetTile(cellPos) == temporaryTile)
                {
                    buildingTilemap.SetTile(cellPos, null);
                    OnTilemapChanged?.Invoke(cellPos);
                }
            }
        }
        
        // Refresh the tilemap after removing temporary tiles
        if (buildingTilemap != null)
        {
            buildingTilemap.RefreshAllTiles();
        }
        
        // Load all CardData to find the correct card for each gadget type
        CardData[] allCards = Resources.LoadAll<CardData>("Cards");
        Dictionary<GadgetType, CardData> gadgetToCardMap = new Dictionary<GadgetType, CardData>();
        
        foreach (var card in allCards)
        {
            if (card.gadgetType != GadgetType.None && !gadgetToCardMap.ContainsKey(card.gadgetType))
            {
                gadgetToCardMap[card.gadgetType] = card;
            }
        }
        
        // Place all pieces in the recipe pattern
        foreach (var piece in comboCardData.recipe)
        {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            
            if (gadgetToCardMap.TryGetValue(piece.gadgetType, out CardData pieceCard))
            {
                // Place the building piece
                PlaceBuildingPiece(pieceCard, cellPos);
            }
        }
        
        // The combo building will be automatically created by CheckForComboBuildings when the last piece is placed
    }

    public void PlaceBuilding(CardData cardData, Vector3Int cellPosition)
    {
        if (CanPlaceBuilding(cellPosition)) {
            Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
            GameObject newPieceObject = 
                Instantiate(cardData.buildingPrefab, worldPosition, Quaternion.identity, parentTransform);

            BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
            if (pieceComponent != null)
            {
                pieceComponent.gadgetType = cardData.gadgetType;
                pieceComponent.cellPosition = cellPosition;
                _placedPieces[cellPosition] = pieceComponent;
            }
            
            BuildingStructure structure = new BuildingStructure
            {
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
    
    private void CheckForComboBuildings(Vector3Int placedPos)
    {
        if (_comboCardDataList == null || _comboCardDataList.Count == 0) return;

        foreach (var comboData in _comboCardDataList) {
            if (CheckPatternAround(placedPos, comboData)) {
                return;
            }
        }
    }

    private bool CheckPatternAround(Vector3Int placedPos, ComboCardData comboData)
    {
        foreach (var piece in comboData.recipe) {
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
        foreach (var piece in comboData.recipe) {
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
        
        foreach (var piece in comboData.recipe)
        {
            Vector3Int relativePos = piece.relativePosition;
            recipePositions.Add(originPos + relativePos);
            
            minX = Mathf.Min(minX, relativePos.x);
            minY = Mathf.Min(minY, relativePos.y);
            maxX = Mathf.Max(maxX, relativePos.x);
            maxY = Mathf.Max(maxY, relativePos.y);
        }
        
        Vector2Int size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        
        BuildingStructure comboStructure = new BuildingStructure
        {
            anchor = originPos, // originPos : 좌하단 기준점
            size = size
        };

        foreach (var targetPos in recipePositions)
        {
            RemoveBuildingPieceAtPosition(targetPos);
            comboStructure.occupiedCells.Add(targetPos);
        }
        
        RegisterBuildingStructure(comboStructure);
        
        Vector3 worldPos = grid.GetCellCenterWorld(originPos);
        GameObject newComboPieceObject = Instantiate(comboData.comboPrefab, worldPos, Quaternion.identity, parentTransform);
        
        BuildingPiece mainPiece = newComboPieceObject.GetComponent<BuildingPiece>();
        if (mainPiece == null)
        {
            mainPiece = newComboPieceObject.AddComponent<BuildingPiece>();
        }

        mainPiece.cellPosition = originPos;

        foreach (var targetPos in recipePositions)
        {
            _placedPieces[targetPos] = mainPiece; 
            
            if (comboData.comboTile != null)
            {
                buildingTilemap.SetTile(targetPos, comboData.comboTile);
            }
        }
        
        HandleComboBuildingLogic(newComboPieceObject, comboData);

        foreach (var targetPos in recipePositions)
        {
            OnTilemapChanged?.Invoke(targetPos);
        }

        Debug.Log($"Combo Building '{comboData.displayName}' Created");
    }

    private void HandleComboBuildingLogic(GameObject comboObject, ComboCardData comboData)
    {
        if (comboObject == null || ResourceManager.Instance == null)
        {
            return;
        }

        switch (comboData.comboType)
        {
            case ComboType.Storage:
                IStorage storage = comboObject.GetComponent<IStorage>();
                if (storage != null)
                {
                    ResourceManager.Instance.AddStorage(storage);
                }
                break;
            case ComboType.Battery:
                IStorage battery = comboObject.GetComponent<IStorage>();
                if (battery != null)
                {
                    ResourceManager.Instance.AddStorage(battery);
                }
                break;
            case ComboType.Generator:
                // 예: ResourceGenerator generator = comboObject.GetComponent<ResourceGenerator>();
                // generator?.Activate();
                break;
            case ComboType.Turret:
                // 터렛 로직 (Turret 컴포넌트가 활성화되므로 별도 로직 필요 없을 수 있음)
                break;
            case ComboType.Radar:
                // 레이더 로직 (Radar 컴포넌트가 활성화되므로 별도 로직 필요 없을 수 있음)
                break;
            // 다른 콤보 타입 로직 추가
        }
    }
    
    public void ClearBuildingDataAt(Vector3Int cellPosition)
    {
        if (buildingTilemap == null) return;
        
        if (_cellToStructureMap.TryGetValue(cellPosition, out BuildingStructure structure))
        {
            Vector3Int anchor = structure.anchor;
            List<Vector3Int> cellsToClear = new List<Vector3Int>(structure.occupiedCells); 

            UnregisterBuildingStructure(anchor);

            foreach (var cell in cellsToClear)
            {
                if (_placedPieces.Remove(cell, out BuildingPiece piece))
                {
                    if (piece != null) Destroy(piece.gameObject);
                }
                
                if (buildingTilemap.HasTile(cell))
                {
                    buildingTilemap.SetTile(cell, null);
                    OnTilemapChanged?.Invoke(cell);
                }
            }
        }
        else if (buildingTilemap.HasTile(cellPosition))
        {
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
        
        if (_placedPieces.Remove(cellPos, out BuildingPiece piece))
        {
            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        }
        buildingTilemap.SetTile(cellPos, null);
    }
    
    public bool GetBuildingAt(Vector3Int cell, out List<Vector3Int> occupiedCells)
    {
        if (_cellToStructureMap.TryGetValue(cell, out BuildingStructure structure))
        {
            occupiedCells = structure.occupiedCells; 
            return true;
        }

        occupiedCells = null;
        return false;
    }
    
    private void RegisterBuildingStructure(BuildingStructure structure)
    {
        if (_buildingStructuresByAnchor.ContainsKey(structure.anchor))
        {
            Debug.LogWarning($"Building structure already exists at anchor {structure.anchor}. Overwriting.");
            UnregisterBuildingStructure(structure.anchor);
        }

        _buildingStructuresByAnchor[structure.anchor] = structure;
        foreach (Vector3Int cell in structure.occupiedCells)
        {
            _cellToStructureMap[cell] = structure;
        }
    }
    
    public void RegisterMainStructure(Vector3Int anchorCell, Vector2Int size)
    {
        BuildingStructure structure = new BuildingStructure
        {
            anchor = anchorCell,
            size = size
        };
        
        // Calculate all occupied cells for the building
        // Don't place tiles - the main structure tiles should already be in the tilemap
        // or the building should exist as a GameObject without tilemap tiles
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector3Int cell = anchorCell + new Vector3Int(x, y, 0);
                structure.occupiedCells.Add(cell);
            }
        }
        
        RegisterBuildingStructure(structure);
        
        // Notify tilemap changes for all cells (if tiles already exist, they'll be refreshed)
        foreach (Vector3Int cell in structure.occupiedCells)
        {
            OnTilemapChanged?.Invoke(cell);
        }
    }
    
    private void UnregisterBuildingStructure(Vector3Int anchor)
    {
        if (_buildingStructuresByAnchor.Remove(anchor, out BuildingStructure structure))
        {
            foreach (Vector3Int cell in structure.occupiedCells)
            {
                _cellToStructureMap.Remove(cell);
            }
        }
    }
    
    public void RemoveResourceTile(Vector3Int cellPosition)
    {
        if (resourceTilemap != null)
        {
            resourceTilemap.SetTile(cellPosition, null);
        }
    }

    public void RegisterProcessor(Processor processor)
    {
        if (!_processors.Contains(processor))
        {
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

        foreach (var processor in _processors)
        {
            if (processor.IsFull) continue;

            float dist = Vector3.Distance(position, processor.GetPosition());
            if (dist < minDistance)
            {
                minDistance = dist;
                closestProcessor = processor;
            }
        }
        return closestProcessor;
    }
}