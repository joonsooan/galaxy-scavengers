using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class Unit_Miner : UnitBase
{
    [Header("References")]
    [SerializeField] private UnitMovement unitMovement;
    [SerializeField] private UnitMining unitMining;

    [Header("Cargo")]
    public int maxCarryAmount = 50;
    [SerializeField] private float resourceSearchInterval = 2.0f;
    [SerializeField] private float maxSearchRadius = 50f;
    public ResourceType[] mineableResourceTypes;

    [Header("VFX")]
    [SerializeField] private string canvasName = "ObjectUI Canvas";
    [SerializeField] private bool showFloatingText;
    [SerializeField] private ParticleSystem miningParticleSystem;
    [SerializeField] private float particleOffsetDistance = 0.5f;
    
    [Header("Mining Animation")]
    [SerializeField] private float vibrationRadius = 0.1f;
    [SerializeField] private float vibrationSpeed = 2f;
    [SerializeField] private float yOffset = 0f;

    private readonly Dictionary<ResourceType, int> _currentCarryAmounts = new Dictionary<ResourceType, int>();
    private Canvas _canvas;
    private Coroutine _findResourceCoroutine;
    private WaitForSeconds _searchWait;

    private Vector3Int _targetMiningCell;
    private ResourceNode _targetResourceNode;
    private IStorage _targetStorage;
    private Vector3Int _targetUnloadCell;
    private UnitSpriteController _spriteController;
    
    private Vector3 _basePosition;
    private Tween _miningVibrationTween;
    private Sequence _vibrationSequence;
    private Vector3 _currentMiningDirection;
    
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
        _spriteController = unitMovement.GetComponent<UnitSpriteController>(); 
    }

    private void Update()
    {
        DecideNextAction();
        UpdateAnimationState();
        
        if (currentState == UnitState.Mining)
        {
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
    }

    private void OnDestroy()
    {
        StopMiningVibration();
        StopMiningParticles();
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

    private void UpdateAnimationState()
    {
        if (_spriteController != null)
        {
            // Unit_Miner only needs IsMining state, not IsConstructing
            bool isMining = currentState == UnitState.Mining;
            _spriteController.UpdateAnimationState(currentState, isMining: isMining);
        }
        if (currentState == UnitState.Moving || currentState == UnitState.ReturningToStorage)
        {
            Vector3 moveDir = unitMovement.GetMoveDirection();
            _spriteController?.UpdateSpriteDirection(moveDir);
            _spriteController?.ClearTarget(); // Clear target when moving
        }
        else if (currentState == UnitState.Mining && _targetResourceNode != null)
        {
            _spriteController?.SetTargetTransform(_targetResourceNode.transform);
        }
        else if (currentState == UnitState.Unloading && _targetStorage != null)
        {
            _spriteController?.SetTargetTransform(((Component)_targetStorage).transform);
        }
        else if (currentState == UnitState.Idle)
        {
            // Keep facing the last target if available, or clear it
            if (_targetResourceNode != null)
            {
                _spriteController?.SetTargetTransform(_targetResourceNode.transform);
            }
            else
            {
                _spriteController?.ClearTarget();
            }
        }
        
        int currentTotal = _currentCarryAmounts.Values.Sum();
        _spriteController.UpdateCargoFill(currentTotal, maxCarryAmount);
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
        StartMiningVibration();
        StartMiningParticles();
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
            Dictionary<ResourceType, int> resourcesBefore = new Dictionary<ResourceType, int>();
            Dictionary<ResourceType, int> resourcesToUnload = new Dictionary<ResourceType, int>();

            foreach (KeyValuePair<ResourceType, int> pair in _currentCarryAmounts.Where(p => p.Value > 0)) {
                resourcesBefore[pair.Key] = _targetStorage.GetCurrentResourceAmount(pair.Key);
                resourcesToUnload[pair.Key] = pair.Value;
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
        
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null)
        {
            return bestTarget;
        }

        List<ResourceNode> availableResources = ResourceManager.Instance.GetAllResources()
            .Where(r =>
                r != null && !r.IsDepleted && r.gameObject.activeInHierarchy &&
                mineableResourceTypes.Contains(r.resourceType) &&
                (!r.IsReserved || r.GetReservedUnit() == this)
            )
            .ToList();

        if (availableResources.Count == 0)
        {
            return bestTarget;
        }

        Vector3 unitPosition = transform.position;
        List<(ResourceNode node, float distance)> resourcesWithDistance = new List<(ResourceNode, float)>();
        
        foreach (ResourceNode resourceNode in availableResources)
        {
            Vector3 resourceWorldPos = BuildingManager.Instance.grid.GetCellCenterWorld(resourceNode.cellPosition);
            float distanceToResource = Vector3.Distance(unitPosition, resourceWorldPos);
            
            if (distanceToResource > maxSearchRadius)
            {
                continue;
            }
            
            resourcesWithDistance.Add((resourceNode, distanceToResource));
        }

        resourcesWithDistance.Sort((a, b) => a.distance.CompareTo(b.distance));

        Vector3Int[] neighborOffsets = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        Grid grid = BuildingManager.Instance.grid;

        foreach (var (resourceNode, resourceDistance) in resourcesWithDistance)
        {
                if (bestTarget.resourceNode != null && resourceDistance > bestTarget.distance + 2f)
            {
                break; // All remaining resources are further away
            }

            foreach (Vector3Int offset in neighborOffsets)
            {
                Vector3Int neighborCell = resourceNode.cellPosition + offset;

                Vector3 neighborWorldPos = grid.GetCellCenterWorld(neighborCell);
                float distanceToNeighbor = Vector3.Distance(unitPosition, neighborWorldPos);
                
                if (distanceToNeighbor >= bestTarget.distance)
                {
                    continue;
                }

                if (BuildingManager.Instance.CanPlaceBuilding(neighborCell))
                {
                    if (distanceToNeighbor < bestTarget.distance)
                    {
                        bestTarget.resourceNode = resourceNode;
                        bestTarget.miningCell = neighborCell;
                        bestTarget.distance = distanceToNeighbor;
                        
                        if (distanceToNeighbor < 2f)
                        {
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
        Grid grid = BuildingManager.Instance.grid;

        bool hasAether = _currentCarryAmounts.ContainsKey(ResourceType.Aether) &&
            _currentCarryAmounts[ResourceType.Aether] > 0;
        bool hasNonAether = _currentCarryAmounts.Any(p => p.Key != ResourceType.Aether && p.Value > 0);

        IEnumerable<IStorage> allStorages = ResourceManager.Instance.GetAllStorages()
            .Where(s => s != null && s.GetTotalCurrentAmount() < s.GetMaxCapacity());

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
        unitMining?.StopMining();
        StopMiningVibration();
        StopMiningParticles();
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
            StopMiningVibration();
            StopMiningParticles();
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
        
        _basePosition = transform.position;
        
        CreateNextCircleMotion();
    }
    
    private void CreateNextCircleMotion()
    {
        if (currentState != UnitState.Mining) return;
        if (_vibrationSequence != null && _vibrationSequence.IsActive())
        {
            _vibrationSequence.Kill();
        }
        
        Vector3 circleCenter = _basePosition;
        Vector3 currentPos = transform.position;
        Vector3 toCenter = circleCenter - currentPos;
        
        float distanceFromCenter = toCenter.magnitude;
        if (distanceFromCenter > vibrationRadius * 1.5f)
        {
            Vector3 closerPos = circleCenter + toCenter.normalized * (vibrationRadius * 0.8f);
            _vibrationSequence = DOTween.Sequence();
            _vibrationSequence.Append(transform.DOMove(closerPos, 0.1f).SetEase(Ease.OutQuad));
            currentPos = closerPos;
        }
        else
        {
            _vibrationSequence = DOTween.Sequence();
        }
        
        float randomAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        int circlePoints = 8;
        float angleStep = (Mathf.PI * 2f) / circlePoints;
        float circleDuration = 1f / vibrationSpeed;
        float pointDuration = circleDuration / circlePoints;
        
        for (int i = 0; i <= circlePoints; i++)
        {
            float angle = randomAngle + (i * angleStep) + UnityEngine.Random.Range(-0.2f, 0.2f);
            
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * vibrationRadius,
                Mathf.Sin(angle) * vibrationRadius,
                0f
            );
            
            Vector3 targetPos = circleCenter + offset;
            _vibrationSequence.Append(transform.DOMove(targetPos, pointDuration).SetEase(Ease.Linear));
        }
        
        _vibrationSequence.OnComplete(() => {
            if (currentState == UnitState.Mining)
            {
                CreateNextCircleMotion();
            }
        });
    }
    
    private void StartMiningParticles()
    {
        if (miningParticleSystem != null)
        {
            UpdateParticlePosition();
            if (!miningParticleSystem.isPlaying)
            {
                miningParticleSystem.Play();
            }
        }
    }
    
    private void UpdateParticlePosition()
    {
        if (miningParticleSystem == null) return;
        
        Transform particleTransform = miningParticleSystem.transform;
        Vector3 offsetPosition = transform.position + (_currentMiningDirection * particleOffsetDistance) - new Vector3(0, yOffset, 0);
        particleTransform.position = offsetPosition;
    }
    
    private void StopMiningParticles()
    {
        if (miningParticleSystem != null && miningParticleSystem.isPlaying)
        {
            miningParticleSystem.Stop();
        }
    }

    private void StopMiningVibration()
    {
        if (_vibrationSequence != null && _vibrationSequence.IsActive())
        {
            _vibrationSequence.Kill();
            _vibrationSequence = null;
        }
        
        if (_miningVibrationTween != null && _miningVibrationTween.IsActive())
        {
            _miningVibrationTween.Kill();
            _miningVibrationTween = null;
        }
        
        if (transform != null && _basePosition != Vector3.zero && currentState != UnitState.Mining)
        {
            transform.DOMove(_basePosition, 0.15f).SetEase(Ease.OutQuad);
        }
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
