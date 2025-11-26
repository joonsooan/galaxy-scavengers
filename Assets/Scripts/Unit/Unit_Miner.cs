using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Unit_Miner : UnitBase
{
    [Header("References")]
    [SerializeField] private UnitMovement unitMovement;
    [SerializeField] private UnitMining unitMining;

    [Header("Cargo")]
    public int maxCarryAmount = 50;
    [SerializeField] private float resourceSearchInterval = 2.0f;
    public ResourceType[] mineableResourceTypes;

    [Header("VFX")]
    [SerializeField] private string canvasName = "ObjectUI Canvas";
    [SerializeField] private bool showFloatingText;
    [SerializeField] private ParticleSystem miningParticleSystem;

    private readonly Dictionary<ResourceType, int> _currentCarryAmounts = new Dictionary<ResourceType, int>();
    private Canvas _canvas;
    private Coroutine _findResourceCoroutine;
    private WaitForSeconds _searchWait;

    private Vector3Int _targetMiningCell;
    private ResourceNode _targetResourceNode;
    private IStorage _targetStorage;
    private Vector3Int _targetUnloadCell;

    protected override void Awake()
    {
        base.Awake();
        _canvas = GameObject.Find(canvasName)?.GetComponent<Canvas>();
        _searchWait = new WaitForSeconds(resourceSearchInterval);
        InitializeCarryAmounts();
    }

    private void Start()
    {
        mineableResourceTypes = UnitManager.Instance != null
            ? UnitManager.Instance.CurrentMineableTypes.ToArray()
            : (ResourceType[])Enum.GetValues(typeof(ResourceType));
    }

    private void Update()
    {
        DecideNextAction();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SubscribeEvents();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        if (_targetResourceNode != null && _targetResourceNode.IsReserved && _targetResourceNode.GetReservedUnit() == this) {
            _targetResourceNode.Unreserve();
        }
    }

    private void DecideNextAction()
    {
        switch (currentState) {
        case UnitState.Idle:
            if (_findResourceCoroutine == null) {
                TryStartActions();
            }
            break;

        case UnitState.Moving:
            OnMoving();
            break;

        case UnitState.Mining:
            OnMining();
            break;

        case UnitState.ReturningToStorage:
            OnReturnToStorage();
            break;
        }
    }

    private void OnMoving()
    {
        if (_targetResourceNode == null || _targetResourceNode.IsDepleted) {
            HandleTargetLoss();
            return;
        }

        Vector3 targetPosition = BuildingManager.Instance.grid.GetCellCenterWorld(_targetMiningCell);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f) {
            StartMiningAction();
        }
    }

    private void OnMining()
    {
        if (_targetResourceNode == null || _targetResourceNode.IsDepleted) {
            HandleTargetLoss();
        }
    }

    private void OnReturnToStorage()
    {
        if (_targetStorage == null || _targetStorage.GetTotalCurrentAmount() >= _targetStorage.GetMaxCapacity()) {
            HandleStorageLoss();
            return;
        }

        Vector3 targetPosition = BuildingManager.Instance.grid.GetCellCenterWorld(_targetUnloadCell);
        if (Vector3.Distance(transform.position, targetPosition) < 0.2f) {
            StartUnloadingAction();
        }
    }

    private void StartMiningAction()
    {
        currentState = UnitState.Mining;
        unitMovement.StopMovement();

        AdjustSpriteDirectionForMining();
        unitMining.StartMining(_targetResourceNode);
    }

    private void StartUnloadingAction()
    {
        currentState = UnitState.Unloading;

        AdjustSpriteDirectionForUnloading();
        StartCoroutine(UnloadResourceCoroutine());
    }

    public void TryStartActions()
    {
        if (_findResourceCoroutine != null) {
            StopCoroutine(_findResourceCoroutine);
        }
        _findResourceCoroutine = StartCoroutine(FindNearestResourceCoroutine());
    }

    private IEnumerator FindNearestResourceCoroutine()
    {
        while (true) {
            if (currentState == UnitState.Idle) {
                FindAndSetTarget();
            }
            yield return _searchWait;
        }
    }

    private IEnumerator UnloadResourceCoroutine()
    {
        unitMovement.StopMovement();
        yield return new WaitForSeconds(1f);

        if (_targetStorage != null) {
            // Track resources before unloading to calculate what was actually accepted
            Dictionary<ResourceType, int> resourcesBefore = new Dictionary<ResourceType, int>();
            Dictionary<ResourceType, int> resourcesToUnload = new Dictionary<ResourceType, int>();

            // Record current storage state and what we want to unload
            foreach (KeyValuePair<ResourceType, int> pair in _currentCarryAmounts.Where(p => p.Value > 0)) {
                resourcesBefore[pair.Key] = _targetStorage.GetCurrentResourceAmount(pair.Key);
                resourcesToUnload[pair.Key] = pair.Value;
            }

            // Try to unload each resource
            foreach (KeyValuePair<ResourceType, int> pair in resourcesToUnload) {
                _targetStorage.TryAddResource(pair.Key, pair.Value);
            }

            // Calculate how much was actually accepted by comparing before/after
            Dictionary<ResourceType, int> remainingResources = new Dictionary<ResourceType, int>();
            foreach (KeyValuePair<ResourceType, int> pair in resourcesToUnload) {
                int amountBefore = resourcesBefore[pair.Key];
                int amountAfter = _targetStorage.GetCurrentResourceAmount(pair.Key);
                int amountAccepted = amountAfter - amountBefore;
                int amountRemaining = pair.Value - amountAccepted;

                if (amountRemaining > 0) {
                    remainingResources[pair.Key] = amountRemaining;
                }
            }

            // Update carry amounts - remove only the amount that was actually accepted
            foreach (KeyValuePair<ResourceType, int> pair in resourcesToUnload) {
                int amountBefore = resourcesBefore[pair.Key];
                int amountAfter = _targetStorage.GetCurrentResourceAmount(pair.Key);
                int amountAccepted = amountAfter - amountBefore;

                _currentCarryAmounts[pair.Key] -= amountAccepted;
                if (_currentCarryAmounts[pair.Key] < 0) {
                    _currentCarryAmounts[pair.Key] = 0;
                }
            }

            // If there are remaining resources, find another storage
            if (remainingResources.Count > 0 && remainingResources.Values.Sum() > 0) {
                _targetStorage = null;
                GoToStorage();
                yield break;
            }
        }

        InitializeCarryAmounts();
        _targetResourceNode = null;
        _targetStorage = null;

        currentState = UnitState.Idle;
    }

    private void FindAndSetTarget()
    {
        if (_currentCarryAmounts.Values.Sum() > 0) {
            GoToStorage();
            return;
        }

        MiningTarget bestTarget = FindClosestMineablePosition();

        if (bestTarget.resourceNode != null) {
            if (_targetResourceNode != null && _targetResourceNode != bestTarget.resourceNode) {
                _targetResourceNode.Unreserve();
            }

            _targetResourceNode = bestTarget.resourceNode;
            _targetMiningCell = bestTarget.miningCell;

            if (_targetResourceNode.Reserve(this)) {
                if (unitMovement.SetNewTarget(BuildingManager.Instance.grid.GetCellCenterWorld(_targetMiningCell))) {
                    currentState = UnitState.Moving;
                }
                else {
                    _targetResourceNode.Unreserve();
                    _targetResourceNode = null;
                    currentState = UnitState.Idle;
                }
            }
            else {
                _targetResourceNode = null;
                currentState = UnitState.Idle;
            }
        }
        else {
            _targetResourceNode?.Unreserve();
            _targetResourceNode = null;
            currentState = UnitState.Idle;
        }
    }

    private MiningTarget FindClosestMineablePosition()
    {
        MiningTarget bestTarget = new MiningTarget { distance = float.MaxValue };

        IEnumerable<ResourceNode> availableResources = ResourceManager.Instance.GetAllResources().Where(r =>
            r != null && !r.IsDepleted && r.gameObject.activeInHierarchy &&
            mineableResourceTypes.Contains(r.resourceType) &&
            (!r.IsReserved || r.GetReservedUnit() == this)
        );

        Vector3Int[] neighborOffsets = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (ResourceNode resourceNode in availableResources) {
            foreach (Vector3Int offset in neighborOffsets) {
                Vector3Int neighborCell = resourceNode.cellPosition + offset;

                if (BuildingManager.Instance.CanPlaceBuilding(neighborCell)) {
                    float distance = Vector3.Distance(transform.position, BuildingManager.Instance.grid.GetCellCenterWorld(neighborCell));
                    if (distance < bestTarget.distance) {
                        bestTarget.resourceNode = resourceNode;
                        bestTarget.miningCell = neighborCell;
                        bestTarget.distance = distance;
                    }
                }
            }
        }
        return bestTarget;
    }

    private void FindAndSetStorage()
    {
        // Use the same logic as FindClosestUnloadPosition but get the storage directly
        UnloadTarget bestTarget = FindClosestUnloadPosition();
        _targetStorage = bestTarget.storage;
    }

    private void GoToStorage()
    {
        UnloadTarget bestTarget = FindClosestUnloadPosition();

        if (bestTarget.storage != null) {
            _targetStorage = bestTarget.storage;
            _targetUnloadCell = bestTarget.unloadCell;

            currentState = UnitState.ReturningToStorage;
            unitMovement.SetNewTarget(BuildingManager.Instance.grid.GetCellCenterWorld(_targetUnloadCell));
        }
        else {
            currentState = UnitState.Idle;
            Debug.Log("모든 저장소가 가득 찼거나 접근할 수 없습니다. 대기합니다.");
        }
    }

    private UnloadTarget FindClosestUnloadPosition()
    {
        UnloadTarget bestTarget = new UnloadTarget { distance = float.MaxValue };
        Grid grid = BuildingManager.Instance.grid;

        // Check what resources the lifter is carrying
        bool hasAether = _currentCarryAmounts.ContainsKey(ResourceType.Aether) &&
            _currentCarryAmounts[ResourceType.Aether] > 0;
        bool hasNonAether = _currentCarryAmounts.Any(p => p.Key != ResourceType.Aether && p.Value > 0);

        IEnumerable<IStorage> allStorages = ResourceManager.Instance.GetAllStorages()
            .Where(s => s != null && s.GetTotalCurrentAmount() < s.GetMaxCapacity());

        // If lifter has NO Aether, exclude Battery objects completely
        if (!hasAether) {
            // Only search through non-Battery storages
            allStorages = allStorages.Where(s => !(s is Battery));
        }
        else {
            // Lifter has Aether - prioritize Battery for Aether, but also need regular storage if has other resources
            if (hasNonAether) {
                // Has both Aether and other resources
                // First, try to find Battery to unload Aether
                List<IStorage> batteryStorages = allStorages.Where(s => s is Battery).ToList();
                UnloadTarget batteryTarget = FindClosestStorageInList(batteryStorages, grid);

                if (batteryTarget.storage != null) {
                    // Found a Battery - use it first (will unload Aether, other resources will remain)
                    return batteryTarget;
                }

                // No Battery available or all full - use regular storage for all resources
                allStorages = allStorages.Where(s => !(s is Battery));
            }
            else {
                // Only has Aether - prioritize Battery, but can use regular storage if Battery is full
                List<IStorage> batteryStorages = allStorages.Where(s => s is Battery).ToList();
                UnloadTarget batteryTarget = FindClosestStorageInList(batteryStorages, grid);

                if (batteryTarget.storage != null) {
                    return batteryTarget;
                }

                // No Battery available - use regular storage
                allStorages = allStorages.Where(s => !(s is Battery));
            }
        }

        // Search through the filtered storages
        return FindClosestStorageInList(allStorages, grid);
    }

    private UnloadTarget FindClosestStorageInList(IEnumerable<IStorage> storages, Grid grid)
    {
        UnloadTarget bestTarget = new UnloadTarget { distance = float.MaxValue };
        Vector3Int[] neighborOffsets = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (IStorage storage in storages) {
            Vector3 storagePosition = (storage as Component).transform.position;
            Vector3Int storageCell = grid.WorldToCell(storagePosition);

            // Get all occupied cells if this is a registered building (handles 3x3 MainStructure)
            List<Vector3Int> occupiedCells = null;
            if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(storageCell, out List<Vector3Int> cells)) {
                occupiedCells = cells;
            }
            else {
                // Not a registered building, treat as single cell
                occupiedCells = new List<Vector3Int> { storageCell };
            }

            // Find all valid interaction cells around the building
            HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
            HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);

            foreach (Vector3Int occupiedCell in occupiedSet) {
                foreach (Vector3Int offset in neighborOffsets) {
                    Vector3Int neighborCell = occupiedCell + offset;

                    // Check if this neighbor is not part of the building and is walkable
                    if (!occupiedSet.Contains(neighborCell) &&
                        BuildingManager.Instance != null &&
                        BuildingManager.Instance.CanPlaceBuilding(neighborCell)) {
                        interactionCells.Add(neighborCell);
                    }
                }
            }

            // Find the closest interaction cell for this storage
            foreach (Vector3Int interactionCell in interactionCells) {
                float distance = Vector3.Distance(transform.position, grid.GetCellCenterWorld(interactionCell));
                if (distance < bestTarget.distance) {
                    bestTarget.storage = storage;
                    bestTarget.unloadCell = interactionCell;
                    bestTarget.distance = distance;
                }
            }
        }
        return bestTarget;
    }

    private void HandleTargetLoss()
    {
        unitMining?.StopMining();
        _targetResourceNode?.Unreserve();
        _targetResourceNode = null;
        currentState = UnitState.Idle;
        unitMovement.StopMovement();
    }

    private void HandleStorageLoss()
    {
        FindAndSetStorage();
        GoToStorage();
    }

    private void SubscribeEvents()
    {
        if (unitMining != null) {
            unitMining.OnResourceMined += HandleResourceMined;
        }
        ResourceManager.OnNewStorageAdded += HandleNewStorageAdded;
        ResourceManager.OnStorageRemoved += HandleStorageRemoved;
        UnitManager.OnMineableTypesChanged += HandleMineableTypesChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void UnsubscribeEvents()
    {
        if (unitMining != null) {
            unitMining.OnResourceMined -= HandleResourceMined;
        }
        ResourceManager.OnNewStorageAdded -= HandleNewStorageAdded;
        ResourceManager.OnStorageRemoved -= HandleStorageRemoved;
        UnitManager.OnMineableTypesChanged -= HandleMineableTypesChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void HandleResourceMined(ResourceType type, int amount)
    {
        if (currentState != UnitState.Mining) return;

        _currentCarryAmounts[type] += amount;
        ShowResourceText(amount);

        if (_currentCarryAmounts.Values.Sum() >= maxCarryAmount) {
            unitMining.StopMining();
            _targetResourceNode?.Unreserve();
            _targetResourceNode = null;

            GoToStorage();
        }
    }

    private void HandleNewStorageAdded()
    {
        if (_currentCarryAmounts.Values.Sum() > 0 && currentState is UnitState.Idle or UnitState.ReturningToStorage) {
            GoToStorage();
        }
    }

    private void HandleStorageRemoved(IStorage storage)
    {
        if (_targetStorage == storage) {
            _targetStorage = null;
            if (currentState is UnitState.ReturningToStorage or UnitState.Unloading) {
                GoToStorage();
            }
        }
    }

    private void HandleMineableTypesChanged(ResourceType[] newTypes)
    {
        mineableResourceTypes = newTypes;

        if ((currentState == UnitState.Mining || currentState == UnitState.Moving) &&
            _targetResourceNode != null && !mineableResourceTypes.Contains(_targetResourceNode.resourceType)) {
            HandleTargetLoss();
        }
        else if (currentState == UnitState.Idle) {
            FindAndSetTarget();
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (_targetStorage != null && (_targetStorage as Component)?.gameObject.scene == scene) {
            _targetStorage = null;
        }
    }

    private void InitializeCarryAmounts()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            _currentCarryAmounts[type] = 0;
        }
    }

    private void ShowResourceText(int amount)
    {
        if (_canvas == null || !showFloatingText) return;

        GameObject textObj = ObjectPooler.Instance.SpawnFromPool(
            "ResourceText", transform.position, Quaternion.identity);

        if (textObj != null) {
            FloatingNumText floatingText = textObj.GetComponent<FloatingNumText>();
            if (floatingText != null) {
                floatingText.Play($"+{amount}", Color.white);
            }
        }
    }

    private void AdjustSpriteDirectionForMining()
    {
        if (_targetResourceNode == null || unitMovement == null) return;

        Vector3Int unitCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        Vector3Int resourceCell = _targetResourceNode.cellPosition;

        Vector3Int relativePosition = resourceCell - unitCell;

        Vector3 targetDirection = Vector3.zero;

        if (relativePosition.x > 0) {
            targetDirection = Vector3.right;
        }
        else if (relativePosition.x < 0) {
            targetDirection = Vector3.left;
        }
        else if (relativePosition.y > 0) {
            targetDirection = Vector3.up;
        }
        else if (relativePosition.y < 0) {
            targetDirection = Vector3.down;
        }

        unitMovement.GetComponent<UnitSpriteController>()?.UpdateSpriteDirection(targetDirection);
    }

    private void AdjustSpriteDirectionForUnloading()
    {
        if (_targetStorage == null || !TryGetComponent<UnitSpriteController>(out UnitSpriteController spriteController)) return;

        Grid grid = BuildingManager.Instance.grid;
        Vector3Int unitCell = grid.WorldToCell(transform.position);

        Vector3Int storageCell = grid.WorldToCell((_targetStorage as Component).transform.position);

        // For large buildings (like 3x3 MainStructure), find the nearest occupied cell
        Vector3Int targetCell = storageCell;
        if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(storageCell, out List<Vector3Int> occupiedCells)) {
            // Find the closest occupied cell to the unit
            float minDistance = float.MaxValue;
            foreach (Vector3Int cell in occupiedCells) {
                float distance = Vector3.Distance(transform.position, grid.GetCellCenterWorld(cell));
                if (distance < minDistance) {
                    minDistance = distance;
                    targetCell = cell;
                }
            }
        }

        Vector3Int relativePosition = targetCell - unitCell;

        Vector2 targetDirection = Vector2.zero;

        if (relativePosition.x > 0) {
            targetDirection = Vector2.right;
        }
        else if (relativePosition.x < 0) {
            targetDirection = Vector2.left;
        }
        else if (relativePosition.y > 0) {
            targetDirection = Vector2.up;
        }
        else if (relativePosition.y < 0) {
            targetDirection = Vector2.down;
        }

        spriteController.UpdateSpriteDirection(targetDirection);
    }

    private struct MiningTarget
    {
        public ResourceNode resourceNode;
        public Vector3Int miningCell;
        public float distance;
    }

    private struct UnloadTarget
    {
        public IStorage storage;
        public Vector3Int unloadCell;
        public float distance;
    }
}
