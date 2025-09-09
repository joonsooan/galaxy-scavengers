using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Unit_Lifter : UnitBase
{
    [Header("References")]
    [SerializeField] private UnitMovement unitMovement;
    [SerializeField] private UnitMining unitMining;

    [Header("Cargo")]
    public int maxCarryAmount = 50;
    [SerializeField] private float resourceSearchInterval = 2.0f;
    public ResourceType[] mineableResourceTypes;

    [Header("VFX")]
    [SerializeField] private string canvasName = "ObejectUI Canvas";
    [SerializeField] private bool showFloatingText;

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

    private void FixedUpdate()
    {
        if (currentState is UnitState.Moving or UnitState.ReturningToStorage) {
            unitMovement.MoveToTarget();
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
            foreach (KeyValuePair<ResourceType, int> pair in _currentCarryAmounts.Where(p => p.Value > 0)) {
                _targetStorage.TryAddResource(pair.Key, pair.Value);
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
        IStorage closestStorage = null;
        float minDistance = float.MaxValue;

        IEnumerable<IStorage> availableStorages = ResourceManager.Instance.GetAllStorages()
            .Where(s => s != null && s.GetTotalCurrentAmount() < s.GetMaxCapacity());

        foreach (IStorage storage in availableStorages) {
            float distance = Vector2.Distance(transform.position, storage.GetPosition());
            if (distance < minDistance) {
                minDistance = distance;
                closestStorage = storage;
            }
        }

        _targetStorage = closestStorage;
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

        IEnumerable<IStorage> availableStorages = ResourceManager.Instance.GetAllStorages()
            .Where(s => s != null && s.GetTotalCurrentAmount() < s.GetMaxCapacity());

        Vector3Int[] neighborOffsets = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (IStorage storage in availableStorages) {
            Vector3 storagePosition = (storage as Component).transform.position;
            Vector3Int storageCell = grid.WorldToCell(storagePosition);

            foreach (Vector3Int offset in neighborOffsets) {
                Vector3Int neighborCell = storageCell + offset;
                if (BuildingManager.Instance.CanPlaceBuilding(neighborCell)) {
                    float distance = Vector3.Distance(transform.position, grid.GetCellCenterWorld(neighborCell));
                    if (distance < bestTarget.distance) {
                        bestTarget.storage = storage;
                        bestTarget.unloadCell = neighborCell;
                        bestTarget.distance = distance;
                    }
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

        if (textObj != null)
        {
            FloatingNumText floatingText = textObj.GetComponent<FloatingNumText>();
            if (floatingText != null)
            {
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
        if (_targetStorage == null || !TryGetComponent<UnitSpriteController>(out var spriteController)) return;

        var grid = BuildingManager.Instance.grid;
        Vector3Int unitCell = grid.WorldToCell(transform.position);
        
        Vector3Int storageCell = grid.WorldToCell((_targetStorage as Component).transform.position);

        Vector3Int relativePosition = storageCell - unitCell;

        Vector2 targetDirection = Vector2.zero;

        if (relativePosition.x > 0)
        {
            targetDirection = Vector2.right;
        }
        else if (relativePosition.x < 0)
        {
            targetDirection = Vector2.left;
        }
        else if (relativePosition.y > 0)
        {
            targetDirection = Vector2.up;
        }
        else if (relativePosition.y < 0)
        {
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
