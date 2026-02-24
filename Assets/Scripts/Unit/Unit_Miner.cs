using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class Unit_Miner : UnitBase
{
    private static readonly Dictionary<Vector3Int, Unit_Miner> ReservedMiningCells = new Dictionary<Vector3Int, Unit_Miner>();
    [Header("References")]
    [SerializeField] private UnitMovement unitMovement;

    [Header("Audio")]
    [SerializeField] private EventReference miningSound;

    [Header("Gathering Stats")]
    [SerializeField] private float unloadingTime = 1f;
    public int mineAmountPerAction = 1; // 한번 채굴 시 캐는 양

    [Header("Cargo")]
    public int maxCarryAmount = 50;
    [SerializeField] private float resourceSearchInterval = 2.0f;
    [SerializeField] private float maxSearchRadius = 50f;
    public ResourceType[] mineableResourceTypes;

    [Header("VFX")]
    [SerializeField] private string canvasName = "ObjectUI Canvas";
    [SerializeField] private float resourceImageSpawnInterval = 0.5f;
    [SerializeField] private ParticleSystem miningParticleSystem;
    [SerializeField] private float particleOffsetDistance = 0.5f;
    [SerializeField] private float yOffset;

    [Header("Mining Animation")]
    [SerializeField] private float vibrationRadius = 0.1f;
    [SerializeField] private float vibrationSpeed = 2f;

    private readonly Dictionary<ResourceType, int> _currentCarryAmounts = new Dictionary<ResourceType, int>();

    private Canvas _canvas;
    private Vector3 _currentMiningDirection;
    private Coroutine _findResourceCoroutine;

    private Coroutine _mineCoroutine;
    private WaitForSeconds _miningDelay;
    private EventInstance _miningSoundInstance;
    private Tween _miningVibrationTween;
    private bool _noResourceAlertActive;
    private bool _mineTypeAllOffAlertActive;
    private bool _isFullAlertActive;
    private WaitForSeconds _searchWait;
    private WaitForSeconds _resourceImageSpawnWait;
    private Vector3 _spriteBaseLocalPosition;
    private UnitSpriteController _spriteController;
    private Vector3Int _targetMiningCell;
    private ResourceNode _targetResourceNode;
    private IStorage _targetStorage;
    private Vector3Int _targetUnloadCell;
    private Sequence _vibrationSequence;
    private bool _hasReservedMiningCell;
    private Vector3Int _reservedMiningCell;
    private int _reservedStorageAmount;

    protected override void Awake()
    {
        base.Awake();
        _canvas = GameManager.Instance?.uiManager?.GetObjectUICanvas() ?? GameObject.Find(canvasName)?.GetComponent<Canvas>();
        _searchWait = CoroutineCache.GetWaitForSeconds(resourceSearchInterval);
        _resourceImageSpawnWait = CoroutineCache.GetWaitForSeconds(resourceImageSpawnInterval);
        InitializeCarryAmounts();
    }

    protected void Start()
    {
        mineableResourceTypes = UnitManager.Instance != null
            ? UnitManager.Instance.CurrentMineableTypes.ToArray()
            : (ResourceType[])Enum.GetValues(typeof(ResourceType));
        _spriteController = GetComponentInChildren<UnitSpriteController>();
    }

    private void Update()
    {
        DecideNextAction();
        UpdateAnimationState();
        UpdateUnitLightAlpha();

        if (currentState == UnitState.Mining) {
            UpdateParticlePosition();
        }
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
        ReleaseMiningCellReservation();
        ReleaseStorageReservation();
        ClearMinerAlerts();
    }

    protected override void OnDestroy()
    {
        ReleaseMiningCellReservation();
        ClearMinerAlerts();
        StopMiningVibration();
        StopMiningParticles();
        StopMiningSound();
        if (_targetResourceNode != null && _targetResourceNode.IsReserved && _targetResourceNode.GetReservedUnit() == this) {
            _targetResourceNode.Unreserve();
        }
    }

    public event Action<ResourceType, int> OnResourceMined;

    private void DecideNextAction()
    {
        switch (currentState) {
        case UnitState.Idle:
            if (_findResourceCoroutine == null) {
                if (GameManager.Instance != null && SceneManager.GetActiveScene().name == "GameScene" && !GameManager.IsGameplayReady) {
                    return;
                }
                TryStartActions();
            }
            UpdateIdleRoam();
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

    private void UpdateAnimationState()
    {
        if (_spriteController != null) {
            bool isMining = currentState == UnitState.Mining;
            _spriteController.UpdateAnimationState(currentState, isMining);
        }
        if (currentState == UnitState.Moving || currentState == UnitState.ReturningToStorage) {
            Vector3 moveDir = unitMovement.GetMoveDirection();
            _spriteController?.UpdateSpriteDirection(moveDir);
            _spriteController?.ClearTarget();
        }
        else if (currentState == UnitState.Mining && _targetResourceNode != null) {
            _spriteController?.SetTargetTransform(_targetResourceNode.transform);
        }
        else if (currentState == UnitState.Unloading && _targetStorage != null) {
            _spriteController?.SetTargetTransform(((Component)_targetStorage).transform);
        }
        else if (currentState == UnitState.Idle) {
            if (_targetResourceNode != null) {
                _spriteController?.SetTargetTransform(_targetResourceNode.transform);
            }
            else {
                _spriteController?.ClearTarget();
            }
        }

        int currentTotal = _currentCarryAmounts.Values.Sum();
        _spriteController?.UpdateCargoFill(currentTotal, maxCarryAmount);
    }

    private void OnMoving()
    {
        if (_targetResourceNode == null || _targetResourceNode.IsDepleted) {
            HandleTargetLoss();
            return;
        }

        if (!mineableResourceTypes.Contains(_targetResourceNode.resourceType)) {
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
        StartMining(_targetResourceNode);
        StartMiningVibration();
        StartMiningParticles();
        StartMiningSound();
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

        // Show progress bar during unloading
        ShowProgressBar();
        float elapsedTime = 0f;

        while (elapsedTime < unloadingTime) {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / unloadingTime;
            UpdateProgressBar(progress);
            yield return null;
        }

        HideProgressBar();

        if (_targetStorage != null) {
            // Check if trying to unload aether and capacity is full
            bool hasAether = _currentCarryAmounts.ContainsKey(ResourceType.Aether) &&
                _currentCarryAmounts[ResourceType.Aether] > 0;

            if (hasAether) {
                AetherConsumptionManager aetherManager = FindFirstObjectByType<AetherConsumptionManager>();
                if (aetherManager != null && aetherManager.IsAetherCapacityFull) {
                    ReleaseStorageReservation();
                    InitializeCarryAmounts();
                    _targetResourceNode = null;
                    _targetStorage = null;
                    currentState = UnitState.Idle;
                    yield break;
                }
            }
            Dictionary<ResourceType, int> resourcesBefore = new Dictionary<ResourceType, int>();
            Dictionary<ResourceType, int> resourcesToUnload = new Dictionary<ResourceType, int>();

            foreach (KeyValuePair<ResourceType, int> pair in _currentCarryAmounts.Where(p => p.Value > 0)) {
                resourcesBefore[pair.Key] = _targetStorage.GetCurrentResourceAmount(pair.Key);
                resourcesToUnload[pair.Key] = pair.Value;
            }

            List<ResourceType> resourceTypesToShow = resourcesToUnload.Keys.ToList();
            if (_targetStorage != null) {
                Vector3 storagePosition = ((Component)_targetStorage).transform.position;
                StartCoroutine(ShowResourceImages(resourceTypesToShow, storagePosition));
            }

            foreach (KeyValuePair<ResourceType, int> pair in resourcesToUnload) {
                _targetStorage.TryAddResource(pair.Key, pair.Value);
            }

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

            foreach (KeyValuePair<ResourceType, int> pair in resourcesToUnload) {
                int amountBefore = resourcesBefore[pair.Key];
                int amountAfter = _targetStorage.GetCurrentResourceAmount(pair.Key);
                int amountAccepted = amountAfter - amountBefore;

                _currentCarryAmounts[pair.Key] -= amountAccepted;
                if (_currentCarryAmounts[pair.Key] < 0) {
                    _currentCarryAmounts[pair.Key] = 0;
                }
            }

            if (remainingResources.Count > 0 && remainingResources.Values.Sum() > 0) {
                ReleaseStorageReservation();
                _targetStorage = null;
                GoToStorage();
                yield break;
            }

            ReleaseStorageReservation();
        }

        InitializeCarryAmounts();
        _targetResourceNode = null;
        _targetStorage = null;

        currentState = UnitState.Idle;
    }

    private void FindAndSetTarget()
    {
        if (_currentCarryAmounts.Values.Sum() > 0) {
            SetMineTypeAllOffAlert(!HasMineableTypesEnabled());
            SetMinerNoResourceAlert(false);
            GoToStorage();
            return;
        }

        MiningTarget bestTarget = FindClosestMineablePosition();

        if (bestTarget.resourceNode != null) {
            if (_targetResourceNode != null && _targetResourceNode != bestTarget.resourceNode) {
                _targetResourceNode.Unreserve();
            }
            ReleaseMiningCellReservation();

            _targetResourceNode = bestTarget.resourceNode;
            _targetMiningCell = bestTarget.miningCell;

            if (!TryReserveMiningCell(_targetMiningCell)) {
                _targetResourceNode = null;
                currentState = UnitState.Idle;
            }
            else if (_targetResourceNode.Reserve(this)) {
                if (unitMovement.SetNewTarget(BuildingManager.Instance.grid.GetCellCenterWorld(_targetMiningCell))) {
                    currentState = UnitState.Moving;
                    ResetIdleRoam();
                    SetMineTypeAllOffAlert(false);
                    SetMinerNoResourceAlert(false);
                }
                else {
                    _targetResourceNode.Unreserve();
                    ReleaseMiningCellReservation();
                    _targetResourceNode = null;
                    currentState = UnitState.Idle;
                }
            }
            else {
                ReleaseMiningCellReservation();
                _targetResourceNode = null;
                currentState = UnitState.Idle;
            }
        }
        else {
            _targetResourceNode?.Unreserve();
            ReleaseMiningCellReservation();
            _targetResourceNode = null;
            currentState = UnitState.Idle;
            if (!HasMineableTypesEnabled()) {
                SetMineTypeAllOffAlert(true);
                SetMinerNoResourceAlert(false);
            }
            else {
                SetMineTypeAllOffAlert(false);
                SetMinerNoResourceAlert(true);
            }
        }
    }

    private MiningTarget FindClosestMineablePosition()
    {
        MiningTarget bestTarget = new MiningTarget { distance = float.MaxValue };

        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) {
            return bestTarget;
        }

        List<ResourceNode> availableResources = ResourceManager.Instance.GetAllResources()
            .Where(r =>
                r != null && !r.IsDepleted && r.gameObject.activeInHierarchy &&
                mineableResourceTypes.Contains(r.resourceType) &&
                (!r.IsReserved || r.GetReservedUnit() == this)
            )
            .ToList();

        if (availableResources.Count == 0) {
            return bestTarget;
        }

        Vector3 unitPosition = transform.position;
        List<(ResourceNode node, float distance)> resourcesWithDistance = new List<(ResourceNode, float)>();

        foreach (ResourceNode resourceNode in availableResources) {
            Vector3 resourceWorldPos = BuildingManager.Instance.grid.GetCellCenterWorld(resourceNode.cellPosition);
            float distanceToResource = Vector3.Distance(unitPosition, resourceWorldPos);

            if (distanceToResource > maxSearchRadius) {
                continue;
            }

            resourcesWithDistance.Add((resourceNode, distanceToResource));
        }

        resourcesWithDistance.Sort((a, b) => a.distance.CompareTo(b.distance));

        Vector3Int[] neighborOffsets = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        Grid grid = BuildingManager.Instance.grid;

        foreach ((ResourceNode resourceNode, float resourceDistance) in resourcesWithDistance) {
            if (bestTarget.resourceNode != null && resourceDistance > bestTarget.distance + 2f) {
                break; // All remaining resources are further away
            }

            foreach (Vector3Int offset in neighborOffsets) {
                Vector3Int neighborCell = resourceNode.cellPosition + offset;

                Vector3 neighborWorldPos = grid.GetCellCenterWorld(neighborCell);
                float distanceToNeighbor = Vector3.Distance(unitPosition, neighborWorldPos);

                if (distanceToNeighbor >= bestTarget.distance) {
                    continue;
                }

                if (BuildingManager.Instance.CanPlaceBuilding(neighborCell) &&
                    !UnitMovement.IsCellAssigned(neighborCell) &&
                    !IsMiningCellReservedByOther(neighborCell)) {
                    if (distanceToNeighbor < bestTarget.distance) {
                        bestTarget.resourceNode = resourceNode;
                        bestTarget.miningCell = neighborCell;
                        bestTarget.distance = distanceToNeighbor;

                        if (distanceToNeighbor < 2f) {
                            return bestTarget;
                        }
                    }
                }
            }
        }

        return bestTarget;
    }

    private void FindAndSetStorage()
    {
        UnloadTarget bestTarget = FindClosestUnloadPosition();
        _targetStorage = bestTarget.storage;
    }

    private void GoToStorage()
    {
        ReleaseStorageReservation();

        UnloadTarget bestTarget = FindClosestUnloadPosition();

        if (bestTarget.storage != null) {
            int carryAmount = _currentCarryAmounts.Values.Sum();
            ResourceManager.Instance.ReserveStorageCapacity(bestTarget.storage, carryAmount);
            _reservedStorageAmount = carryAmount;

            _targetStorage = bestTarget.storage;
            _targetUnloadCell = bestTarget.unloadCell;
            SetMinerIsFullAlert(false);

            currentState = UnitState.ReturningToStorage;
            unitMovement.SetNewTarget(BuildingManager.Instance.grid.GetCellCenterWorld(_targetUnloadCell));
        }
        else {
            currentState = UnitState.Idle;
            SetMinerIsFullAlert(_currentCarryAmounts.Values.Sum() > 0);
            Debug.Log("모든 저장소가 가득 찼거나 접근할 수 없습니다. 대기합니다.");
        }
    }

    private UnloadTarget FindClosestUnloadPosition()
    {
        Grid grid = BuildingManager.Instance.grid;

        bool hasAether = _currentCarryAmounts.ContainsKey(ResourceType.Aether) &&
            _currentCarryAmounts[ResourceType.Aether] > 0;
        bool hasNonAether = _currentCarryAmounts.Any(p => p.Key != ResourceType.Aether && p.Value > 0);

        // If trying to unload aether and capacity is full, don't find storage
        if (hasAether && !hasNonAether) {
            AetherConsumptionManager aetherManager = FindFirstObjectByType<AetherConsumptionManager>();
            if (aetherManager != null && aetherManager.IsAetherCapacityFull) {
                return new UnloadTarget { distance = float.MaxValue };
            }
        }

        int carryAmount = _currentCarryAmounts.Values.Sum();
        IEnumerable<IStorage> allStorages = ResourceManager.Instance.GetAllStorages()
            .Where(s => s != null && ResourceManager.Instance.GetAvailableStorageCapacity(s) >= carryAmount);

        if (!hasAether) {
            allStorages = allStorages.Where(s => !(s is Battery));
        }
        else {
            if (hasNonAether) {
                List<IStorage> batteryStorages = allStorages.Where(s => s is Battery).ToList();
                UnloadTarget batteryTarget = FindClosestStorageInList(batteryStorages, grid);

                if (batteryTarget.storage != null) {
                    return batteryTarget;
                }

                allStorages = allStorages.Where(s => !(s is Battery));
            }
            else {
                List<IStorage> batteryStorages = allStorages.Where(s => s is Battery).ToList();
                UnloadTarget batteryTarget = FindClosestStorageInList(batteryStorages, grid);

                if (batteryTarget.storage != null) {
                    return batteryTarget;
                }

                allStorages = allStorages.Where(s => !(s is Battery));
            }
        }

        return FindClosestStorageInList(allStorages, grid);
    }

    private UnloadTarget FindClosestStorageInList(IEnumerable<IStorage> storages, Grid grid)
    {
        UnloadTarget bestTarget = new UnloadTarget { distance = float.MaxValue };
        Vector3Int[] neighborOffsets = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (IStorage storage in storages) {
            Vector3 storagePosition = (storage as Component).transform.position;
            Vector3Int storageCell = grid.WorldToCell(storagePosition);

            List<Vector3Int> occupiedCells = null;
            if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(storageCell, out List<Vector3Int> cells)) {
                occupiedCells = cells;
            }
            else {
                occupiedCells = new List<Vector3Int> { storageCell };
            }

            HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
            HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);

            foreach (Vector3Int occupiedCell in occupiedSet) {
                foreach (Vector3Int offset in neighborOffsets) {
                    Vector3Int neighborCell = occupiedCell + offset;

                    if (!occupiedSet.Contains(neighborCell) &&
                        BuildingManager.Instance != null &&
                        BuildingManager.Instance.CanPlaceBuilding(neighborCell)) {
                        interactionCells.Add(neighborCell);
                    }
                }
            }

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
        StopMining();
        StopMiningVibration();
        StopMiningParticles();
        StopMiningSound();
        _targetResourceNode?.Unreserve();
        ReleaseMiningCellReservation();
        _targetResourceNode = null;
        currentState = UnitState.Idle;
        unitMovement.StopMovement();
    }

    private void ReleaseStorageReservation()
    {
        if (_targetStorage != null && _reservedStorageAmount > 0 && ResourceManager.Instance != null) {
            ResourceManager.Instance.ReleaseStorageCapacity(_targetStorage, _reservedStorageAmount);
            _reservedStorageAmount = 0;
        }
    }

    private void HandleStorageLoss()
    {
        ReleaseStorageReservation();
        FindAndSetStorage();
        GoToStorage();
    }

    private void SubscribeEvents()
    {
        OnResourceMined += HandleResourceMined;
        ResourceManager.OnNewStorageAdded += HandleNewStorageAdded;
        ResourceManager.OnStorageSpaceFreed += HandleStorageSpaceFreed;
        ResourceManager.OnStorageRemoved += HandleStorageRemoved;
        UnitManager.OnMineableTypesChanged += HandleMineableTypesChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        GameManager.OnPauseStateChanged += HandlePauseStateChanged;
    }

    private void UnsubscribeEvents()
    {
        OnResourceMined -= HandleResourceMined;
        ResourceManager.OnNewStorageAdded -= HandleNewStorageAdded;
        ResourceManager.OnStorageSpaceFreed -= HandleStorageSpaceFreed;
        ResourceManager.OnStorageRemoved -= HandleStorageRemoved;
        UnitManager.OnMineableTypesChanged -= HandleMineableTypesChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        GameManager.OnPauseStateChanged -= HandlePauseStateChanged;
    }

    private void HandlePauseStateChanged(bool isPaused)
    {
        if (_miningSoundInstance.isValid())
        {
            _miningSoundInstance.setPaused(isPaused);
        }
    }

    private void HandleResourceMined(ResourceType type, int amount)
    {
        if (currentState != UnitState.Mining) return;

        _currentCarryAmounts[type] += amount;

        if (_currentCarryAmounts.Values.Sum() >= maxCarryAmount) {
            StopMining();
            StopMiningVibration();
            StopMiningParticles();
            StopMiningSound();
            _targetResourceNode?.Unreserve();
            ReleaseMiningCellReservation();
            _targetResourceNode = null;

            GoToStorage();
        }
    }

    private void HandleNewStorageAdded()
    {
        SetMinerIsFullAlert(false);
        if (_currentCarryAmounts.Values.Sum() > 0 && currentState is UnitState.Idle or UnitState.ReturningToStorage) {
            GoToStorage();
        }
    }

    private void HandleStorageSpaceFreed(IStorage storage, int availableCapacity)
    {
        SetMinerIsFullAlert(false);
        if (_currentCarryAmounts.Values.Sum() > 0 && currentState is UnitState.Idle or UnitState.ReturningToStorage) {
            GoToStorage();
        }
    }

    private void HandleStorageRemoved(IStorage storage)
    {
        if (_targetStorage == storage) {
            ReleaseStorageReservation();
            _targetStorage = null;
            if (currentState is UnitState.ReturningToStorage or UnitState.Unloading) {
                GoToStorage();
            }
        }
    }

    private void HandleMineableTypesChanged(ResourceType[] newTypes)
    {
        mineableResourceTypes = newTypes;
        bool hasMineableTypes = HasMineableTypesEnabled();
        SetMineTypeAllOffAlert(!hasMineableTypes);
        if (!hasMineableTypes) {
            SetMinerNoResourceAlert(false);
        }

        if ((currentState == UnitState.Mining || currentState == UnitState.Moving) &&
            _targetResourceNode != null && !mineableResourceTypes.Contains(_targetResourceNode.resourceType)) {
            if (currentState == UnitState.Moving) {
                Vector3 targetPosition = BuildingManager.Instance.grid.GetCellCenterWorld(_targetMiningCell);
                float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

                if (distanceToTarget > 1.0f) {
                    HandleTargetLoss();
                }
            }
            else if (currentState == UnitState.Mining) {
                HandleTargetLoss();
            }
        }

        if (currentState == UnitState.Idle) {
            if (_findResourceCoroutine == null) {
                TryStartActions();
            }
        }
    }

    private void ClearMinerAlerts()
    {
        SetMinerNoResourceAlert(false);
        SetMineTypeAllOffAlert(false);
        SetMinerIsFullAlert(false);
    }

    private bool HasMineableTypesEnabled()
    {
        return mineableResourceTypes != null && mineableResourceTypes.Length > 0;
    }

    private void SetMinerNoResourceAlert(bool shouldEnable)
    {
        if (shouldEnable == _noResourceAlertActive) return;
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (shouldEnable) {
            alertManager?.RegisterAlert(GameAlertType.MinerNoResource, this);
        }
        else {
            alertManager?.UnregisterAlert(GameAlertType.MinerNoResource, this);
        }
        _noResourceAlertActive = shouldEnable;
    }

    private void SetMineTypeAllOffAlert(bool shouldEnable)
    {
        if (shouldEnable == _mineTypeAllOffAlertActive) return;
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (shouldEnable) {
            alertManager?.RegisterAlert(GameAlertType.MineTypeAllOff, this);
        }
        else {
            alertManager?.UnregisterAlert(GameAlertType.MineTypeAllOff, this);
        }
        _mineTypeAllOffAlertActive = shouldEnable;
    }

    private void SetMinerIsFullAlert(bool shouldEnable)
    {
        if (shouldEnable == _isFullAlertActive) return;
        GameAlertUIManager alertManager = FindFirstObjectByType<GameAlertUIManager>();
        if (shouldEnable) {
            alertManager?.RegisterAlert(GameAlertType.MinerIsFull, this);
        }
        else {
            alertManager?.UnregisterAlert(GameAlertType.MinerIsFull, this);
        }
        _isFullAlertActive = shouldEnable;
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

    private IEnumerator ShowResourceImages(List<ResourceType> resourceTypes, Vector3 spawnPosition)
    {
        if (_canvas == null || resourceTypes == null || resourceTypes.Count == 0) yield break;
        Vector3 offset = new Vector3(0f, 0.5f, 0f);

        foreach (ResourceType resourceType in resourceTypes) {
            GameObject imageObj = ObjectPooler.Instance.SpawnFromPool(
                "ResourceImage", spawnPosition + offset, Quaternion.identity);

            if (imageObj != null) {
                FloatingResourceImage floatingImage = imageObj.GetComponent<FloatingResourceImage>();
                if (floatingImage != null) {
                    floatingImage.Play(resourceType);
                }
            }

            yield return _resourceImageSpawnWait;
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

        _currentMiningDirection = targetDirection;
        unitMovement.GetComponent<UnitSpriteController>()?.UpdateSpriteDirection(targetDirection);
        UpdateParticlePosition();
    }

    private void AdjustSpriteDirectionForUnloading()
    {
        if (_targetStorage == null || !TryGetComponent(out UnitSpriteController spriteController)) return;

        Grid grid = BuildingManager.Instance.grid;
        Vector3Int unitCell = grid.WorldToCell(transform.position);

        Vector3Int storageCell = grid.WorldToCell((_targetStorage as Component).transform.position);

        Vector3Int targetCell = storageCell;
        if (BuildingManager.Instance != null && BuildingManager.Instance.GetBuildingAt(storageCell, out List<Vector3Int> occupiedCells)) {
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

    private void StartMiningVibration()
    {
        StopMiningVibration();

        if (_spriteController != null) {
            _spriteBaseLocalPosition = _spriteController.transform.localPosition;
        }

        CreateNextCircleMotion();
    }

    private void CreateNextCircleMotion()
    {
        if (currentState != UnitState.Mining) return;
        if (_spriteController == null) return;

        if (_vibrationSequence != null && _vibrationSequence.IsActive()) {
            _vibrationSequence.Kill();
        }

        Vector3 circleCenter = _spriteBaseLocalPosition;
        Vector3 currentLocalPos = _spriteController.transform.localPosition;
        Vector3 toCenter = circleCenter - currentLocalPos;

        float distanceFromCenter = toCenter.magnitude;
        if (distanceFromCenter > vibrationRadius * 1.5f) {
            Vector3 closerPos = circleCenter + toCenter.normalized * (vibrationRadius * 0.8f);
            _vibrationSequence = DOTween.Sequence();
            _vibrationSequence.Append(_spriteController.transform.DOLocalMove(closerPos, 0.1f).SetEase(Ease.OutQuad));
        }
        else {
            _vibrationSequence = DOTween.Sequence();
        }

        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        int circlePoints = 8;
        float angleStep = Mathf.PI * 2f / circlePoints;
        float circleDuration = 1f / vibrationSpeed;
        float pointDuration = circleDuration / circlePoints;

        for (int i = 0; i <= circlePoints; i++) {
            float angle = randomAngle + i * angleStep + Random.Range(-0.2f, 0.2f);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * vibrationRadius,
                Mathf.Sin(angle) * vibrationRadius,
                0f
            );

            Vector3 targetLocalPos = circleCenter + offset;
            _vibrationSequence.Append(_spriteController.transform.DOLocalMove(targetLocalPos, pointDuration).SetEase(Ease.Linear));
        }

        _vibrationSequence.OnComplete(() => {
            if (currentState == UnitState.Mining) {
                CreateNextCircleMotion();
            }
        });
    }

    private void StartMiningParticles()
    {
        if (miningParticleSystem != null) {
            UpdateParticlePosition();
            if (!miningParticleSystem.isPlaying) {
                miningParticleSystem.Play();
            }
        }
    }

    private void UpdateParticlePosition()
    {
        if (miningParticleSystem == null) return;

        Transform particleTransform = miningParticleSystem.transform;
        Vector3 offsetPosition = transform.position + _currentMiningDirection * particleOffsetDistance - new Vector3(0, yOffset, 0);
        particleTransform.position = offsetPosition;
    }

    private void StopMiningParticles()
    {
        if (miningParticleSystem != null && miningParticleSystem.isPlaying) {
            miningParticleSystem.Stop();
        }
    }

    private void StartMiningSound()
    {
        StopMiningSound();
        if (!miningSound.IsNull)
        {
            _miningSoundInstance = RuntimeManager.CreateInstance(miningSound);
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                RuntimeManager.AttachInstanceToGameObject(_miningSoundInstance, gameObject, rb);
            }
            else
            {
                RuntimeManager.AttachInstanceToGameObject(_miningSoundInstance, gameObject);
            }
            _miningSoundInstance.start();
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                _miningSoundInstance.setPaused(true);
            }
        }
    }

    private void StopMiningSound()
    {
        if (_miningSoundInstance.isValid())
        {
            RuntimeManager.DetachInstanceFromGameObject(_miningSoundInstance);
            _miningSoundInstance.stop(STOP_MODE.ALLOWFADEOUT);
            _miningSoundInstance.release();
            _miningSoundInstance = default;
        }
    }

    private void StopMiningVibration()
    {
        if (_vibrationSequence != null && _vibrationSequence.IsActive()) {
            _vibrationSequence.Kill();
            _vibrationSequence = null;
        }

        if (_miningVibrationTween != null && _miningVibrationTween.IsActive()) {
            _miningVibrationTween.Kill();
            _miningVibrationTween = null;
        }

        if (_spriteController != null && currentState == UnitState.Mining) {
            _spriteController.transform.DOLocalMove(_spriteBaseLocalPosition, 0.15f).SetEase(Ease.OutQuad);
        }
        else if (currentState != UnitState.Mining) {
            _spriteBaseLocalPosition = Vector3.zero;
        }
    }

    private void StartMining(ResourceNode target)
    {
        _targetResourceNode = target;

        _miningDelay = CoroutineCache.GetWaitForSeconds(target.timeToMinePerUnit);

        if (_mineCoroutine == null) {
            _mineCoroutine = StartCoroutine(MineResourceCoroutine());
        }
    }

    private void StopMining()
    {
        if (_mineCoroutine != null) {
            StopCoroutine(_mineCoroutine);
            _mineCoroutine = null;
        }
    }

    private IEnumerator MineResourceCoroutine()
    {
        yield return _miningDelay;

        while (true) {
            if (_targetResourceNode != null && !_targetResourceNode.IsDepleted) {
                int minedAmount = _targetResourceNode.Mine(mineAmountPerAction);
                OnResourceMined?.Invoke(_targetResourceNode.resourceType, minedAmount);
            }
            else {
                StopMining();
                yield break;
            }
            yield return _miningDelay;
        }
    }

    private bool TryReserveMiningCell(Vector3Int cell)
    {
        if (ReservedMiningCells.TryGetValue(cell, out Unit_Miner reservedMiner) && reservedMiner != null && reservedMiner != this) {
            return false;
        }

        ReservedMiningCells[cell] = this;
        _reservedMiningCell = cell;
        _hasReservedMiningCell = true;
        return true;
    }

    private void ReleaseMiningCellReservation()
    {
        if (!_hasReservedMiningCell) {
            return;
        }

        if (ReservedMiningCells.TryGetValue(_reservedMiningCell, out Unit_Miner reservedMiner) && reservedMiner == this) {
            ReservedMiningCells.Remove(_reservedMiningCell);
        }

        _reservedMiningCell = default;
        _hasReservedMiningCell = false;
    }

    private bool IsMiningCellReservedByOther(Vector3Int cell)
    {
        if (!ReservedMiningCells.TryGetValue(cell, out Unit_Miner reservedMiner)) {
            return false;
        }

        return reservedMiner != null && reservedMiner != this;
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
