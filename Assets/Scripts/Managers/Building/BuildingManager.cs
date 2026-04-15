using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using FMODUnity;

public class BuildingManager : MonoBehaviour
{
    [Header("References")]
    public Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap buildingTilemap;
    [SerializeField] private TileBase temporaryTile;
    [SerializeField] private Transform buildingParentTransform;
    [SerializeField] private Transform resourceParentTransform;
    [Header("Audio")]
    [SerializeField] private EventReference constructionSiteCreateSound;
    private readonly Dictionary<BuildingPieceType, TileBase> _buildingPieceTypeToTileCache = new Dictionary<BuildingPieceType, TileBase>();

    private readonly Dictionary<Vector3Int, BuildingStructure> _buildingStructuresByAnchor = new Dictionary<Vector3Int, BuildingStructure>();
    private readonly Dictionary<Vector3Int, BuildingStructure> _cellToStructureMap = new Dictionary<Vector3Int, BuildingStructure>();
    private readonly HashSet<Vector3Int> _mainStructureCells = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, MainStructure> _mainStructureInstanceByAnchor = new Dictionary<Vector3Int, MainStructure>();
    private readonly Dictionary<Vector3Int, BuildingPiece> _placedPieces = new Dictionary<Vector3Int, BuildingPiece>();
    private readonly List<Processor> _processors = new List<Processor>();
    private readonly HashSet<Vector3Int> _resourceCellCache = new HashSet<Vector3Int>();

    private readonly HashSet<Vector3Int> _temporaryTiles = new HashSet<Vector3Int>();

    private readonly HashSet<Vector3Int> _walkableCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> _terrainCells = new HashSet<Vector3Int>();

    [Header("Buildings")]
    private List<BuildingData> _buildingDataList;

    private MapGenerator _cachedMapGenerator;

    public Tilemap GroundTilemap {
        get {
            return groundTilemap;
        }
    }

    public Transform BuildingParentTransform {
        get {
            return buildingParentTransform;
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

        LoadAllBuildings();
        CacheAllBuildingPieces();
        CacheMapGenerator();
    }

    private void OnEnable()
    {
        LaunchUIController.OnLaunchSequenceStarted += ResetAllProcessors;
    }

    private void OnDisable()
    {
        LaunchUIController.OnLaunchSequenceStarted -= ResetAllProcessors;
    }

    private void Start()
    {
        InitializeResourceCache();
    }

    private void OnDestroy()
    {
        MapObjectSpawner.OnAllObjectsSpawned -= RegisterExistingBuildings;
        ResourceManager.OnResourceNodeAdded -= OnResourceNodeAdded;
        ResourceManager.OnResourceNodeRemoved -= OnResourceNodeRemoved;
    }

    public static bool IsBuildingProperlyPlaced(Transform buildingTransform)
    {
        if (Instance == null) return false;

        Transform buildingParent = Instance.BuildingParentTransform;
        if (buildingParent == null) return false;

        Transform current = buildingTransform;
        while (current != null) {
            if (current == buildingParent) {
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    private void InitializeResourceCache()
    {
        MapObjectSpawner.OnAllObjectsSpawned += RegisterExistingBuildings;
        ResourceManager.OnResourceNodeAdded += OnResourceNodeAdded;
        ResourceManager.OnResourceNodeRemoved += OnResourceNodeRemoved;

        if (ResourceManager.Instance != null) {
            RebuildResourceCache();
        }
    }

    private void RebuildResourceCache()
    {
        _resourceCellCache.Clear();
        if (ResourceManager.Instance != null) {
            List<ResourceNode> resources = ResourceManager.Instance.GetAllResources();
            foreach (ResourceNode node in resources) {
                if (node != null) {
                    if (node.cellPosition == Vector3Int.zero && grid != null) {
                        node.cellPosition = grid.WorldToCell(node.transform.position);
                    }
                    _resourceCellCache.Add(node.cellPosition);
                    _walkableCells.Remove(node.cellPosition);
                }
            }
        }
    }

    private void OnResourceNodeAdded(ResourceNode node)
    {
        if (node != null) {
            if (node.cellPosition == Vector3Int.zero && grid != null) {
                node.cellPosition = grid.WorldToCell(node.transform.position);
            }
            _resourceCellCache.Add(node.cellPosition);
            _walkableCells.Remove(node.cellPosition);
        }
    }

    private void OnResourceNodeRemoved(ResourceNode node)
    {
        if (node != null) {
            _resourceCellCache.Remove(node.cellPosition);
            TryAddCellToWalkable(node.cellPosition);
        }
    }

    private void TryAddCellToWalkable(Vector3Int cell)
    {
        if (_terrainCells.Contains(cell) || _mainStructureCells.Contains(cell)) return;
        if (_cellToStructureMap.ContainsKey(cell) || _resourceCellCache.Contains(cell)) return;
        if (groundTilemap == null || !groundTilemap.HasTile(cell)) return;
        _walkableCells.Add(cell);
    }

    private void RegisterExistingBuildings()
    {
        RegisterExistingProcessors();
        RegisterExistingStorages();
        RegisterExistingResourceNodes();
        RegisterExistingMainStructure();
    }

    private void RegisterExistingProcessors()
    {
        Processor[] existingProcessors = buildingParentTransform.GetComponentsInChildren<Processor>(true);

        foreach (Processor processor in existingProcessors) {
            if (processor != null && !_processors.Contains(processor)) {
                RegisterProcessor(processor);
            }
        }
    }

    private void RegisterExistingStorages()
    {
        if (ResourceManager.Instance == null) return;

        IStorage[] existingStorages = buildingParentTransform.GetComponentsInChildren<IStorage>(true);

        foreach (IStorage storage in existingStorages) {
            if (storage == null) continue;

            bool alreadyRegistered = false;
            foreach (IStorage registeredStorage in ResourceManager.Instance.GetAllStorages()) {
                if (registeredStorage == storage) {
                    alreadyRegistered = true;
                    break;
                }
            }

            if (!alreadyRegistered) {
                ResourceManager.Instance.AddStorage(storage);

                if (storage is MainStructure mainStructure) {
                    ResourceManager.Instance.RegisterMainStructure(mainStructure);
                }
            }
        }
    }

    private void RegisterExistingResourceNodes()
    {
        if (ResourceManager.Instance == null) return;

        ResourceNode[] existingResources = resourceParentTransform.GetComponentsInChildren<ResourceNode>(true);

        foreach (ResourceNode node in existingResources) {
            if (node != null) {
                ResourceManager.Instance.AddResourceNode(node);
            }
        }
    }

    private void RegisterExistingMainStructure()
    {
        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();

        if (mainStructure != null) {
            if (ResourceManager.Instance != null) {
                ResourceManager.Instance.RegisterMainStructure(mainStructure);
                ResourceManager.Instance.AddStorage(mainStructure);
            }

            Vector3Int centerCell = grid.WorldToCell(mainStructure.transform.position);
            Vector3Int anchorCell = centerCell - new Vector3Int(1, 1, 0);

            RegisterMainStructure(anchorCell, new Vector2Int(3, 3), mainStructure);
        }
    }

    public static event Action<Vector3Int> OnTilemapChanged;
    public static event Action<BuildingData> OnBuildingConstructed;

    private void CacheMapGenerator()
    {
        _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
    }

    private MapGenerator GetMapGenerator()
    {
        if (_cachedMapGenerator == null) {
            _cachedMapGenerator = FindFirstObjectByType<MapGenerator>();
        }
        return _cachedMapGenerator;
    }

    private void LoadAllBuildings()
    {
        _buildingDataList = new List<BuildingData>(
            Resources.LoadAll<BuildingData>("Buildings"));
    }

    private void CacheAllBuildingPieces()
    {
        BuildingPieceData[] allData = Resources.LoadAll<BuildingPieceData>("Building Pieces");

        foreach (BuildingPieceData data in allData) {
            if (data.buildingPieceType != BuildingPieceType.None && data.buildingPieceTile != null) {
                if (!_buildingPieceTypeToTileCache.ContainsKey(data.buildingPieceType)) {
                    _buildingPieceTypeToTileCache[data.buildingPieceType] = data.buildingPieceTile;
                }
            }
        }
    }

    public bool IsResourceTile(Vector3Int cellPosition)
    {
        return _resourceCellCache.Contains(cellPosition);
    }

    public bool IsBuildingTile(Vector3Int cellPosition)
    {
        if (buildingTilemap == null) return false;
        return buildingTilemap.HasTile(cellPosition);
    }

    public bool IsTerrainCell(Vector3Int cellPosition)
    {
        if (_terrainCells.Count > 0) {
            return _terrainCells.Contains(cellPosition);
        }

        MapGenerator mapGenerator = GetMapGenerator();
        if (mapGenerator != null) {
            if (mapGenerator.IsTerrainCell(cellPosition)) {
                return true;
            }

            if (groundTilemap != null) {
                TileBase tile = groundTilemap.GetTile(cellPosition);
                if (tile != null && mapGenerator.IsTerrainTile(tile)) {
                    return true;
                }
            }
        }

        return false;
    }

    public bool CanPlaceBuilding(Vector3Int cellPosition)
    {
        if (grid == null || groundTilemap == null ||
            buildingTilemap == null) {
            return false;
        }

        if (!groundTilemap.HasTile(cellPosition)) return false;
        if (IsTerrainCell(cellPosition)) return false;
        if (IsResourceTile(cellPosition)) return false;

        if (buildingTilemap.HasTile(cellPosition)) return false;
        if (IsTemporaryTile(cellPosition)) return false;
        if (GetPieceAt(cellPosition) != null) return false;
        if (IsMainStructureCell(cellPosition)) return false;

        if (GetBuildingAt(cellPosition, out _)) {
            return false;
        }

        if (FogOfWarManager.Instance != null && !FogOfWarManager.Instance.CanPlaceBuilding(cellPosition)) {
            return false;
        }

        return true;
    }

    public bool IsTemporaryTile(Vector3Int cellPosition)
    {
        return _temporaryTiles.Contains(cellPosition);
    }

    public bool IsMainStructureCell(Vector3Int cellPosition)
    {
        return _mainStructureCells.Contains(cellPosition);
    }

    public void ClearWalkableCellCache()
    {
        _walkableCells.Clear();
        _terrainCells.Clear();
    }

    public void InitializeWalkableCellCache(BoundsInt mapBounds)
    {
        _walkableCells.Clear();
        _terrainCells.Clear();

        MapGenerator mapGenerator = GetMapGenerator();
        if (mapGenerator == null || groundTilemap == null) return;

        foreach (Vector3Int pos in mapBounds.allPositionsWithin) {
            if (!groundTilemap.HasTile(pos)) continue;

            bool isTerrain = mapGenerator.IsTerrainCell(pos);
            if (!isTerrain && groundTilemap != null) {
                TileBase tile = groundTilemap.GetTile(pos);
                if (tile != null && mapGenerator.IsTerrainTile(tile)) {
                    isTerrain = true;
                }
            }

            if (isTerrain) {
                _terrainCells.Add(pos);
            }
            else {
                _walkableCells.Add(pos);
            }
        }
    }

    public bool IsCellWalkable(Vector3Int cell, bool allowTemporaryForConstruct = false)
    {
        if (_walkableCells.Count > 0) {
            if (_walkableCells.Contains(cell)) return true;
            if (allowTemporaryForConstruct && _temporaryTiles.Contains(cell)) return true;
            return false;
        }

        if (IsBuildingTile(cell)) {
            if (!(allowTemporaryForConstruct && IsTemporaryTile(cell))) return false;
        }
        else if (IsResourceTile(cell)) return false;
        else if (IsTerrainCell(cell)) return false;

        if (IsMainStructureCell(cell) || GetBuildingAt(cell, out _)) return false;

        return true;
    }

    public void CreateConstructionSite(
        BuildingData buildingData,
        Vector3Int anchorCellPosition
    )
    {
        if (buildingData == null || buildingData.recipe == null || buildingData.recipe.Count == 0) {
            return;
        }

        foreach (BuildingData.BuildingPiece piece in buildingData.recipe) {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (!CanPlaceBuilding(cellPos)) {
                return;
            }
        }

        if (buildingTilemap != null) {
            buildingTilemap.gameObject.SetActive(true);
        }
        else {
            return;
        }

        foreach (BuildingData.BuildingPiece piece in buildingData.recipe) {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (temporaryTile != null && buildingTilemap != null) {
                buildingTilemap.SetTile(cellPos, temporaryTile);
                _temporaryTiles.Add(cellPos);
                _walkableCells.Remove(cellPos);
                OnTilemapChanged?.Invoke(cellPos);
            }
        }

        if (buildingTilemap != null) {
            buildingTilemap.RefreshAllTiles();
        }

        Vector3 worldPosition = grid.GetCellCenterWorld(anchorCellPosition);
        GameObject siteObject = new GameObject($"ConstructionSite_{anchorCellPosition}");
        siteObject.transform.position = worldPosition;
        siteObject.transform.SetParent(buildingParentTransform);

        if (!constructionSiteCreateSound.IsNull) {
            RuntimeManager.PlayOneShot(constructionSiteCreateSound);
        }

        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.buildingData = buildingData;
        site.cellPosition = anchorCellPosition;

        site.CalculateCosts();


        if (ConstructionManager.Instance == null) {
            ConstructionManager existingManager = FindFirstObjectByType<ConstructionManager>();
            if (existingManager == null) {
                GameObject managerObj = new GameObject("ConstructionManager");
                managerObj.AddComponent<ConstructionManager>();
            }
        }

        if (ConstructionManager.Instance != null) {
            ConstructionManager.Instance.RegisterConstructionSite(site);
        }

        if (TutorialManager.Instance != null && buildingData != null) {
            string buildingTypeName = buildingData.buildingType.ToString();
            if (buildingTypeName == "Storage") {
                TutorialManager.Instance.OnBuildingPlaced("Storage");
            }
            else if (buildingTypeName == "MainStructure") {
                TutorialManager.Instance.OnBuildingPlaced("MainStructure");
            }
            else if (buildingTypeName == "Smelter") {
                TutorialManager.Instance.OnBuildingPlaced("Smelter");
            }
        }
    }

    public void PlaceBuildingPieceAtCell(BuildingData buildingData, Vector3Int pieceCell, Vector3Int anchorCellPosition)
    {
        if (buildingData == null || buildingData.recipe == null) return;

        foreach (BuildingData.BuildingPiece piece in buildingData.recipe) {
            Vector3Int cellPos = anchorCellPosition + piece.relativePosition;
            if (cellPos == pieceCell) {
                BuildingPieceData[] allData = Resources.LoadAll<BuildingPieceData>("Building Pieces");
                foreach (BuildingPieceData data in allData) {
                    if (data.buildingPieceType == piece.buildingPieceType) {
                        if (_temporaryTiles.Contains(pieceCell)) {
                            _temporaryTiles.Remove(pieceCell);
                            if (buildingTilemap.HasTile(pieceCell) && buildingTilemap.GetTile(pieceCell) == temporaryTile) {
                                buildingTilemap.SetTile(pieceCell, null);
                                OnTilemapChanged?.Invoke(pieceCell);
                            }
                        }

                        PlaceBuildingPiecePrefabOnly(data, pieceCell);
                        CheckForBuildings(pieceCell);
                        return;
                    }
                }
            }
        }
    }

    private void PlaceBuildingPiecePrefabOnly(BuildingPieceData buildingPieceData, Vector3Int cellPosition)
    {
        Vector3 worldPosition = grid.GetCellCenterWorld(cellPosition);
        GameObject newPieceObject =
            Instantiate(buildingPieceData.buildingPiecePrefab, worldPosition, Quaternion.identity, buildingParentTransform);

        BuildingPiece pieceComponent = newPieceObject.GetComponent<BuildingPiece>();
        if (pieceComponent != null) {
            pieceComponent.buildingPieceType = buildingPieceData.buildingPieceType;
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

    public void CreateBuildingFromConstruction(Vector3Int originPos, BuildingData data)
    {
        List<Vector3Int> recipePositions = new List<Vector3Int>();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (BuildingData.BuildingPiece piece in data.recipe) {
            Vector3Int relativePos = piece.relativePosition;
            recipePositions.Add(originPos + relativePos);

            minX = Mathf.Min(minX, relativePos.x);
            minY = Mathf.Min(minY, relativePos.y);
            maxX = Mathf.Max(maxX, relativePos.x);
            maxY = Mathf.Max(maxY, relativePos.y);
        }

        Vector2Int size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);

        BuildingStructure structure = new BuildingStructure {
            anchor = originPos,
            size = size
        };

        foreach (Vector3Int targetPos in recipePositions) {
            RemoveBuildingPieceAtPosition(targetPos);
            structure.occupiedCells.Add(targetPos);
        }

        RegisterBuildingStructure(structure);

        Vector3 worldPos = grid.GetCellCenterWorld(originPos);
        GameObject newPieceObject = Instantiate(data.buildingPrefab, worldPos, Quaternion.identity, buildingParentTransform);
        newPieceObject.SetActive(false);

        BuildingPiece builtMainPiece = newPieceObject.GetComponent<BuildingPiece>();
        if (builtMainPiece == null) {
            builtMainPiece = newPieceObject.AddComponent<BuildingPiece>();
        }

        builtMainPiece.cellPosition = originPos;

        foreach (Vector3Int targetPos in recipePositions) {
            _placedPieces[targetPos] = builtMainPiece;
        }

        newPieceObject.SetActive(true);

        HandleBuildingLogic(newPieceObject, data);

        if (NoiseManager.Instance != null)
        {
            Damageable damageable = newPieceObject.GetComponent<Damageable>();
            if (damageable != null)
            {
                NoiseManager.Instance.RegisterBuilding(damageable);
            }
        }

        OnBuildingConstructed?.Invoke(data);
    }

    private void CheckForBuildings(Vector3Int placedPos)
    {
        if (_buildingDataList == null || _buildingDataList.Count == 0) return;

        foreach (BuildingData data in _buildingDataList) {
            if (CheckPatternAround(placedPos, data)) {
                return;
            }
        }
    }

    private bool CheckPatternAround(Vector3Int placedPos, BuildingData data)
    {
        foreach (BuildingData.BuildingPiece piece in data.recipe) {
            Vector3Int originPosCandidate = placedPos - piece.relativePosition;

            if (CheckPattern(originPosCandidate, data)) {
                CreateBuilding(originPosCandidate, data);
                return true;
            }
        }
        return false;
    }

    private bool CheckPattern(Vector3Int originPos, BuildingData data)
    {
        foreach (BuildingData.BuildingPiece piece in data.recipe) {
            Vector3Int targetPos = originPos + piece.relativePosition;
            TileBase targetTile = buildingTilemap.GetTile(targetPos);

            if (!_buildingPieceTypeToTileCache.TryGetValue(piece.buildingPieceType, out TileBase requiredTile) ||
                targetTile != requiredTile) {
                return false;
            }
        }
        return true;
    }

    private void CreateBuilding(Vector3Int originPos, BuildingData data)
    {
        List<Vector3Int> recipePositions = new List<Vector3Int>();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (BuildingData.BuildingPiece piece in data.recipe) {
            Vector3Int relativePos = piece.relativePosition;
            recipePositions.Add(originPos + relativePos);

            minX = Mathf.Min(minX, relativePos.x);
            minY = Mathf.Min(minY, relativePos.y);
            maxX = Mathf.Max(maxX, relativePos.x);
            maxY = Mathf.Max(maxY, relativePos.y);
        }

        Vector2Int size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);

        BuildingStructure structure = new BuildingStructure {
            anchor = originPos, // originPos : 좌하단 기준점
            size = size
        };

        foreach (Vector3Int targetPos in recipePositions) {
            RemoveBuildingPieceAtPosition(targetPos);
            structure.occupiedCells.Add(targetPos);
        }

        RegisterBuildingStructure(structure);

        Vector3 worldPos = grid.GetCellCenterWorld(originPos);
        GameObject pieceObject = Instantiate(data.buildingPrefab, worldPos, Quaternion.identity, buildingParentTransform);
        pieceObject.SetActive(false);

        BuildingPiece mainPiece = pieceObject.GetComponent<BuildingPiece>();
        if (mainPiece == null) {
            mainPiece = pieceObject.AddComponent<BuildingPiece>();
        }

        mainPiece.cellPosition = originPos;

        foreach (Vector3Int targetPos in recipePositions) {
            _placedPieces[targetPos] = mainPiece;
        }

        foreach (Vector3Int targetPos in recipePositions) {
            if (data.buildingTile != null) {
                buildingTilemap.SetTile(targetPos, data.buildingTile);
            }
        }

        pieceObject.SetActive(true);

        HandleBuildingLogic(pieceObject, data);

        if (NoiseManager.Instance != null)
        {
            Damageable damageable = pieceObject.GetComponent<Damageable>();
            if (damageable != null)
            {
                NoiseManager.Instance.RegisterBuilding(damageable);
            }
        }

        foreach (Vector3Int targetPos in recipePositions) {
            OnTilemapChanged?.Invoke(targetPos);
        }

        Debug.Log($"Building '{data.displayName}' Created");

        OnBuildingConstructed?.Invoke(data);
    }

    private void HandleBuildingLogic(GameObject obj, BuildingData data)
    {
        if (obj == null || ResourceManager.Instance == null) {
            return;
        }

        BuildingDataHolder dataHolder = obj.GetComponent<BuildingDataHolder>();
        if (dataHolder == null) {
            dataHolder = obj.AddComponent<BuildingDataHolder>();
        }
        dataHolder.SetBuildingData(data);

        if (obj.GetComponent<BuildingHoverTrigger>() == null) {
            obj.AddComponent<BuildingHoverTrigger>();
        }

        switch (data.buildingType) {
        case BuildingType.Storage:
            IStorage storage = obj.GetComponent<IStorage>();
            if (storage != null) {
                ResourceManager.Instance.AddStorage(storage);
            }
            break;
        case BuildingType.Battery:
            IStorage battery = obj.GetComponent<IStorage>();
            if (battery != null) {
                ResourceManager.Instance.AddStorage(battery);
            }
            break;
        case BuildingType.Generator:
            ResourceGenerator generator = obj.GetComponent<ResourceGenerator>();
            if (generator != null) {
                generator.SetConstructed();
            }
            break;
        case BuildingType.Turret:
        case BuildingType.Radar:
            break;
        case BuildingType.PowerReceiver:
            PowerReceiver powerReceiver = obj.GetComponent<PowerReceiver>();
            if (powerReceiver == null) {
                powerReceiver = obj.GetComponentInChildren<PowerReceiver>(true);
            }
            powerReceiver?.SetConstructed();
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
            TryAddCellToWalkable(cellPosition);
        }
    }

    public BuildingPiece GetPieceAt(Vector3Int cellPosition)
    {
        _placedPieces.TryGetValue(cellPosition, out BuildingPiece piece);
        return piece;
    }

    public void ClearConstructionSiteData(Vector3Int anchorCell, BuildingData buildingData)
    {
        if (buildingData == null || buildingData.recipe == null) return;

        HashSet<Vector3Int> structuresDestroyed = new HashSet<Vector3Int>();

        foreach (BuildingData.BuildingPiece piece in buildingData.recipe) {
            Vector3Int cellPos = anchorCell + piece.relativePosition;

            if (_temporaryTiles.Contains(cellPos)) {
                _temporaryTiles.Remove(cellPos);
                if (buildingTilemap != null && buildingTilemap.HasTile(cellPos)) {
                    buildingTilemap.SetTile(cellPos, null);
                    OnTilemapChanged?.Invoke(cellPos);
                }
                TryAddCellToWalkable(cellPos);
            }

            if (_placedPieces.TryGetValue(cellPos, out BuildingPiece pieceObj)) {
                if (_cellToStructureMap.TryGetValue(cellPos, out BuildingStructure structure)) {
                    if (!structuresDestroyed.Contains(structure.anchor)) {
                        structuresDestroyed.Add(structure.anchor);
                        ClearBuildingDataAt(structure.anchor);
                    }
                }
                else {
                    if (pieceObj != null) {
                        Destroy(pieceObj.gameObject);
                    }
                    _placedPieces.Remove(cellPos);

                    if (buildingTilemap != null && buildingTilemap.HasTile(cellPos)) {
                        buildingTilemap.SetTile(cellPos, null);
                        OnTilemapChanged?.Invoke(cellPos);
                    }
                }
            }
        }

        if (buildingTilemap != null) {
            buildingTilemap.RefreshAllTiles();
        }
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

    public bool TryGetBuildingAnchor(Vector3Int cellInBuildingFootprint, out Vector3Int anchor)
    {
        anchor = default;
        if (_cellToStructureMap.TryGetValue(cellInBuildingFootprint, out BuildingStructure structure)) {
            anchor = structure.anchor;
            return true;
        }
        return false;
    }

    public bool TryGetBuildingAnchorCells(Transform buildingTransform, out Vector3Int anchor, out List<Vector3Int> occupiedCells)
    {
        anchor = default;
        occupiedCells = null;
        if (grid == null || buildingTransform == null) {
            return false;
        }

        MainStructure mainStructure = buildingTransform.GetComponent<MainStructure>() ??
                                      buildingTransform.GetComponentInParent<MainStructure>(true);
        if (mainStructure != null && TryGetFootprintForRegisteredMainStructure(mainStructure, out anchor, out occupiedCells)) {
            return true;
        }

        BuildingPiece piece = buildingTransform.GetComponent<BuildingPiece>();
        if (piece == null) {
            piece = buildingTransform.GetComponentInParent<BuildingPiece>(true);
        }

        Vector3Int lookupCell;
        if (piece != null) {
            lookupCell = piece.cellPosition;
            if (_cellToStructureMap.TryGetValue(lookupCell, out BuildingStructure structure)) {
                anchor = structure.anchor;
                occupiedCells = structure.occupiedCells;
                return true;
            }
            foreach (KeyValuePair<Vector3Int, BuildingPiece> kvp in _placedPieces) {
                if (kvp.Value != piece) {
                    continue;
                }
                if (_cellToStructureMap.TryGetValue(kvp.Key, out structure)) {
                    anchor = structure.anchor;
                    occupiedCells = structure.occupiedCells;
                    return true;
                }
            }
        }
        else {
            lookupCell = grid.WorldToCell(buildingTransform.position);
            if (_cellToStructureMap.TryGetValue(lookupCell, out BuildingStructure structure)) {
                anchor = structure.anchor;
                occupiedCells = structure.occupiedCells;
                return true;
            }
        }

        Vector3Int worldCell = grid.WorldToCell(buildingTransform.position);
        if (worldCell != lookupCell && _cellToStructureMap.TryGetValue(worldCell, out BuildingStructure structureWorld)) {
            anchor = structureWorld.anchor;
            occupiedCells = structureWorld.occupiedCells;
            return true;
        }

        return false;
    }

    private bool TryGetFootprintForRegisteredMainStructure(MainStructure mainStructure, out Vector3Int anchor,
        out List<Vector3Int> occupiedCells)
    {
        anchor = default;
        occupiedCells = null;
        if (mainStructure == null) {
            return false;
        }
        foreach (KeyValuePair<Vector3Int, MainStructure> kvp in _mainStructureInstanceByAnchor) {
            if (kvp.Value != mainStructure) {
                continue;
            }
            anchor = kvp.Key;
            if (_buildingStructuresByAnchor.TryGetValue(anchor, out BuildingStructure structure)) {
                occupiedCells = structure.occupiedCells;
                return true;
            }
        }
        return false;
    }

    public BuildingData GetBuildingDataAt(Vector3Int cell)
    {
        if (_buildingDataList == null || _buildingDataList.Count == 0) return null;
        if (buildingTilemap == null) return null;

        if (_cellToStructureMap.TryGetValue(cell, out BuildingStructure structure)) {
            Vector3Int anchor = structure.anchor;

            TileBase anchorTile = buildingTilemap.GetTile(anchor);
            if (anchorTile != null) {
                foreach (BuildingData data in _buildingDataList) {
                    if (data.buildingTile == anchorTile) {
                        if (CheckPattern(anchor, data)) {
                            return data;
                        }
                    }
                }
            }

            foreach (BuildingData data in _buildingDataList) {
                if (CheckPattern(anchor, data)) {
                    return data;
                }
            }
        }

        return null;
    }

    private void RegisterBuildingStructure(BuildingStructure structure)
    {
        if (_buildingStructuresByAnchor.ContainsKey(structure.anchor)) {
            UnregisterBuildingStructure(structure.anchor);
        }

        _buildingStructuresByAnchor[structure.anchor] = structure;
        foreach (Vector3Int cell in structure.occupiedCells) {
            _cellToStructureMap[cell] = structure;
            _walkableCells.Remove(cell);
        }
    }

    public void RegisterMainStructure(Vector3Int anchorCell, Vector2Int size, MainStructure mainStructureInstance = null)
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

        if (mainStructureInstance != null) {
            _mainStructureInstanceByAnchor[anchorCell] = mainStructureInstance;
        }

        foreach (Vector3Int cell in structure.occupiedCells) {
            OnTilemapChanged?.Invoke(cell);
        }
    }

    public bool TryGetMainStructureAtCell(Vector3Int cell, out MainStructure mainStructure)
    {
        mainStructure = null;
        if (!_cellToStructureMap.TryGetValue(cell, out BuildingStructure structure)) {
            return false;
        }
        return _mainStructureInstanceByAnchor.TryGetValue(structure.anchor, out mainStructure);
    }

    private void UnregisterBuildingStructure(Vector3Int anchor)
    {
        _mainStructureInstanceByAnchor.Remove(anchor);
        if (_buildingStructuresByAnchor.Remove(anchor, out BuildingStructure structure)) {
            foreach (Vector3Int cell in structure.occupiedCells) {
                _cellToStructureMap.Remove(cell);
                _mainStructureCells.Remove(cell);
                TryAddCellToWalkable(cell);
            }
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

    private void ResetAllProcessors()
    {
        Processor[] allProcessors = FindObjectsByType<Processor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Processor processor in allProcessors) {
            if (processor != null) {
                processor.ResetAllWork();
            }
        }
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

        if (buildingPiece.buildingPieceType != BuildingPieceType.None &&
            _buildingPieceTypeToTileCache.TryGetValue(buildingPiece.buildingPieceType, out TileBase tile)) {
            if (buildingTilemap != null && !buildingTilemap.HasTile(cellPosition)) {
                buildingTilemap.SetTile(cellPosition, tile);
                OnTilemapChanged?.Invoke(cellPosition);
            }
        }

        Debug.Log($"[BuildingManager] Registered pre-placed building at {cellPosition} (Type: {buildingPiece.buildingPieceType})");
    }

    private class BuildingStructure
    {
        public readonly List<Vector3Int> occupiedCells = new List<Vector3Int>();
        public Vector3Int anchor;
        public Vector2Int size;
    }
}
