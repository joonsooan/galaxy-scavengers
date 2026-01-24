using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Processor : Damageable, IClickable, IAetherConsumer
{
    [SerializeField] private ProcessorData processorData;
    [Header("Aether Consumption")]
    [SerializeField] private int aetherConsumptionPerSecond = 1;
    
    private readonly List<ActiveRecipe> _activeRecipes = new ();

    private readonly List<Unit_Drone> _assignedDrones = new ();
    private readonly Dictionary<ResourceType, int> _currentIngredients = new ();
    private readonly List<ResourceRequest> _pendingRequests = new ();
    private readonly Dictionary<Unit_Drone, Vector3Int> _droneInteractionCells = new ();

    private int _maxAssignedDrones;
    private int _maxIngredientStorage;

    private List<ProcessorRecipe> _recipes;
    private AetherConsumptionManager _aetherConsumptionManager;

    public ProcessorData ProcessorData => processorData;
    public IReadOnlyList<ActiveRecipe> ActiveRecipes => _activeRecipes;
    public IReadOnlyList<Unit_Drone> AssignedDrones => _assignedDrones;
    public bool IsFull => _assignedDrones.Count >= _maxAssignedDrones;
    
    // IAetherConsumer implementation
    public int AetherConsumptionPerSecond => aetherConsumptionPerSecond;
    private bool _isOperational = true;
    public bool IsOperational => _isOperational;


    protected override void Awake()
    {
        base.Awake();

        if (processorData == null) {
            return;
        }

        _recipes = processorData.Recipes;
        _maxIngredientStorage = processorData.MaxIngredientStorage;
        _maxAssignedDrones = processorData.MaxAssignedDrones;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) {
            _currentIngredients[type] = 0;
        }

        SortRecipes();

        foreach (ProcessorRecipe recipeData in _recipes) {
            _activeRecipes.Add(new ActiveRecipe(recipeData, this));
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BuildingManager.Instance?.RegisterProcessor(this);
        
        if (!BuildingManager.IsBuildingProperlyPlaced(transform))
        {
            return;
        }
        
        FindAndCacheAetherManager();
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.RegisterConsumer(this);
        }
    }

    protected override void OnDisable()
    {
        if (_aetherConsumptionManager != null)
        {
            _aetherConsumptionManager.UnregisterConsumer(this);
        }
        
        base.OnDisable();
        BuildingManager.Instance?.UnregisterProcessor(this);

        List<Unit_Drone> dronesToRelease = new List<Unit_Drone>(_assignedDrones);
        foreach (Unit_Drone drone in dronesToRelease) {
            drone.AssignProcessor(null);
        }
    }
    
    private void FindAndCacheAetherManager()
    {
        if (_aetherConsumptionManager == null)
        {
            _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        }
    }
    
    public void OnAetherUnavailable()
    {
        if (_isOperational)
        {
            _isOperational = false;
            // Stop all processing - release drones from recipes
            foreach (ActiveRecipe recipe in _activeRecipes)
            {
                if (recipe.isProcessing && recipe.assignedDrone != null)
                {
                    Unit_Drone drone = recipe.assignedDrone;
                    recipe.assignedDrone = null;
                    recipe.isProcessing = false;
                    drone.SetTask_Idle();
                }
            }
        }
    }
    
    public void OnAetherAvailable()
    {
        if (!_isOperational)
        {
            _isOperational = true;
            // Resume processing - request tasks for idle drones
            foreach (Unit_Drone drone in _assignedDrones)
            {
                if (drone != null && drone.HasCheckedIn && drone.CurrentRecipeTask == null)
                {
                    bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
                    if (!hasPendingRequest)
                    {
                        RequestTask(drone);
                    }
                }
            }
        }
    }

    public void OnClicked()
    {
        OnProcessorClicked?.Invoke(this);
    }

    public static event Action<Processor> OnProcessorClicked;

    private void SortRecipes()
    {
        if (_recipes == null) return;
        _recipes.Sort((a, b) => a.priority.CompareTo(b.priority));
    }

    public void AssignDrone(Unit_Drone drone)
    {
        if (!_assignedDrones.Contains(drone) && !IsFull) {
            _assignedDrones.Add(drone);
        }
    }

    public void ReleaseDrone(Unit_Drone drone)
    {
        _assignedDrones.Remove(drone);
        
        if (_droneInteractionCells.ContainsKey(drone)) {
            _droneInteractionCells.Remove(drone);
        }

        ReleaseDroneFromRecipe(drone);

        ResourceRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);
        if (request != null) {
            _pendingRequests.Remove(request);
        }
    }

    public void RequestTask(Unit_Drone drone)
    {
        if (!drone.HasCheckedIn) {
            return;
        }

        // 0. 이 드론이 이미 레시피 작업에 할당되어 있는지 확인 (처리 중이거나 처리하러 이동 중)
        if (drone.CurrentRecipeTask != null) {
            // 레시피에 할당되어 있으면 재할당하지 않음 (처리 중이거나 처리하러 이동 중)
            Debug.Log($"[Processor:{name}] RequestTask: Drone '{drone.name}' already assigned to recipe {drone.CurrentRecipeTask.recipeData.resourceType}, skipping reassignment");
            return;
        }

        // 0.5. 이 드론이 현재 재료를 운반 중인지 확인 (FetchingResource 또는 DeliveringResource 상태)
        ResourceRequest existingRequest = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);
        if (existingRequest != null) {
            // 드론이 이미 재료를 운반 중이면 재할당하지 않음
            Debug.Log($"[Processor:{name}] RequestTask: Drone '{drone.name}' already fetching/delivering {existingRequest.type} x{existingRequest.amount}, skipping reassignment");
            return;
        }

        // 1. 재료 운반 작업 찾기
        ResourceRequest newRequest = FindNextIngredientRequest(drone.carryCapacity);
        if (newRequest != null) {
            newRequest.assignedDrone = drone;
            _pendingRequests.Add(newRequest);
            drone.SetTask_FetchResource(newRequest, this);
            Debug.Log($"[Processor:{name}] RequestTask: Drone '{drone.name}' fetch {newRequest.amount} {newRequest.type}");
            return;
        }

        // 2. 새로운 '처리' 작업 찾기
        foreach (ActiveRecipe recipe in _activeRecipes) {
            // 이 레시피가 (작업 가능하고) && (담당자가 없고) && (재료가 있고) && (생산 제한에 걸리지 않았는지) 확인
            if (recipe.assignedDrone == null &&
                !recipe.isProcessing &&
                HasIngredientsFor(recipe.recipeData) &&
                PassesProductionCapCheck(recipe)) {
                // 레시피에 드론 배정 (아직 처리 시작하지 않음 - 드론이 프로세서에 도착하면 시작)
                recipe.assignedDrone = drone;
                recipe.processingProgress = 0f;

                Debug.Log($"[Processor:{name}] RequestTask: Assigned recipe {recipe.recipeData.resourceType} to drone '{drone.name}'. Ingredients ready, waiting for drone to arrive.");
                drone.SetTask_Process(this, recipe);
                return;
            }
        }

        // 3. 담당자 없는 '진행 중인' 작업 이어받기
        // (예: 드론이 파괴되었다가 다른 드론이 할당된 경우)
        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null && recipe.isProcessing) {
                recipe.assignedDrone = drone;
                drone.SetTask_Process(this, recipe);
                Debug.Log($"[Processor:{name}] RequestTask: Drone '{drone.name}' picks up ongoing {recipe.recipeData.resourceType}");
                return;
            }
        }

        // 4. 할 일 없음
        drone.SetTask_Idle();
        Debug.Log($"[Processor:{name}] RequestTask: No work for Drone '{drone.name}', set Idle");
    }

    private bool PassesProductionCapCheck(ActiveRecipe recipe)
    {
        if (recipe.maxProductionLimit <= 0) {
            return false;
        }

        int currentAmount = ResourceManager.Instance.GetResourceAmount(recipe.recipeData.resourceType);
        return currentAmount < recipe.maxProductionLimit;
    }

    private ResourceRequest FindNextIngredientRequest(int droneCapacity)
    {
        int totalOnTheWay = _pendingRequests.Sum(r => r.amount);
        int totalInStorage = GetTotalCurrentIngredients();

        if (totalOnTheWay + totalInStorage >= _maxIngredientStorage) {
            Debug.Log($"[Processor:{name}] Ingredient request blocked: storage full ({totalInStorage}+{totalOnTheWay}/{_maxIngredientStorage})");
            return null;
        }

        if (_recipes == null) return null;

        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (!PassesProductionCapCheck(recipe)) {
                continue;
            }

            // 레시피에 필요한 재료 확인
            foreach (ResourceCost ingredient in recipe.recipeData.ingredients) {
                int needed = ingredient.amount;

                // '이미 진행중인' 레시피는 재료가 필요 없음
                if (recipe.isProcessing) {
                    needed = 0;
                }

                int inStorage = _currentIngredients.ContainsKey(ingredient.resourceType) ? _currentIngredients[ingredient.resourceType] : 0;
                int onTheWay = _pendingRequests
                    .Where(r => r.type == ingredient.resourceType)
                    .Sum(r => r.amount);

                int stillNeeded = needed - inStorage - onTheWay;

                if (stillNeeded > 0) {
                    int spaceAvailable = _maxIngredientStorage - totalInStorage - totalOnTheWay;
                    int amountToFetch = Mathf.Min(stillNeeded, droneCapacity, spaceAvailable);

                    if (amountToFetch > 0) {
                        Debug.Log($"[Processor:{name}] Create ingredient request: {ingredient.resourceType} x{amountToFetch} (needed {stillNeeded}, storage {inStorage}, onWay {onTheWay})");
                        return new ResourceRequest { type = ingredient.resourceType, amount = amountToFetch };
                    }
                }
            }
        }
        return null;
    }

    public bool TryDepositIngredient(ResourceType type, int amount, Unit_Drone drone)
    {
        ResourceRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);

        if (request == null || request.type != type) {
            Debug.Log($"[Processor:{name}] Deposit FAILED from '{drone.name}': no matching request for {type}");
            return false;
        }

        int totalIngredients = GetTotalCurrentIngredients();
        int canAddAmount = Mathf.Min(amount, _maxIngredientStorage - totalIngredients);

        _currentIngredients[type] += canAddAmount;
        _pendingRequests.Remove(request);

        Debug.Log($"[Processor:{name}] Deposit from '{drone.name}': {type} +{canAddAmount} (requested {amount}). Total now {GetTotalCurrentIngredients()}/{_maxIngredientStorage}");

        if (canAddAmount > 0) {
            CheckAndAssignReadyRecipes();
        }

        return canAddAmount > 0;
    }

    private bool HasIngredientsFor(ProcessorRecipe recipe)
    {
        foreach (ResourceCost ingredient in recipe.ingredients) {
            if (!_currentIngredients.ContainsKey(ingredient.resourceType) || _currentIngredients[ingredient.resourceType] < ingredient.amount) {
                return false;
            }
        }
        return true;
    }

    private void ConsumeIngredients(ProcessorRecipe recipe)
    {
        foreach (ResourceCost ingredient in recipe.ingredients) {
            _currentIngredients[ingredient.resourceType] -= ingredient.amount;
        }
    }

    private void CheckAndAssignReadyRecipes()
    {
        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null &&
                !recipe.isProcessing &&
                HasIngredientsFor(recipe.recipeData) &&
                PassesProductionCapCheck(recipe)) {
            }
        }
    }

    public void ProcessRecipeWork(ActiveRecipe recipe, float workAmount)
    {
        // Don't process if not operational (no aether)
        if (!_isOperational)
        {
            if (recipe.assignedDrone != null)
            {
                recipe.assignedDrone.SetTask_Idle();
            }
            recipe.assignedDrone = null;
            recipe.isProcessing = false;
            return;
        }
        
        if (recipe.assignedDrone == null) {
            return;
        }

        if (!recipe.isProcessing) {
            if (HasIngredientsFor(recipe.recipeData)) {
                ConsumeIngredients(recipe.recipeData);
                recipe.isProcessing = true;
                recipe.processingProgress = 0f;
            }
            else {
                recipe.assignedDrone = null;
                recipe.processingProgress = 0f;
                return;
            }
        }

        float previousProgress = recipe.processingProgress;
        recipe.processingProgress += workAmount;

        float progressPercent = recipe.processingProgress / recipe.recipeData.processingTime * 100f;
        float previousPercent = previousProgress / recipe.recipeData.processingTime * 100f;

        int currentQuarter = Mathf.FloorToInt(Mathf.Min(progressPercent, 100f) / 25f);
        int previousQuarter = Mathf.FloorToInt(Mathf.Min(previousPercent, 100f) / 25f);

        // 25% 마다 로그 출력 (25%, 50%, 75%, 100%)
        if (currentQuarter > previousQuarter) {
            Debug.Log($"[Processor:{name}] Processing PROGRESS: {recipe.recipeData.resourceType} - {progressPercent:F1}% ({recipe.processingProgress:F2}/{recipe.recipeData.processingTime:F2}s)");
        }

        if (recipe.processingProgress >= recipe.recipeData.processingTime) {
            ProduceOutput(recipe.recipeData);

            Unit_Drone completedDrone = recipe.assignedDrone;
            recipe.isProcessing = false;
            recipe.processingProgress = 0;
            recipe.assignedDrone = null;

            completedDrone.SetTask_Idle();

            CheckProductionLimits(recipe);
        }
    }

    public void ReleaseDroneFromRecipe(Unit_Drone drone)
    {
        ActiveRecipe recipe = _activeRecipes.FirstOrDefault(r => r.assignedDrone == drone);
        if (recipe != null) {
            recipe.assignedDrone = null;
            Debug.Log($"[Processor:{name}] Recipe '{recipe.recipeData.resourceType}' released from Drone '{drone.name}'");
        }
    }

    private void ProduceOutput(ProcessorRecipe recipeData)
    {
        int amountBefore = ResourceManager.Instance.GetResourceAmount(recipeData.resourceType);
        ResourceManager.Instance.AddResource(recipeData.resourceType, recipeData.produceAmount);
        int amountAfter = ResourceManager.Instance.GetResourceAmount(recipeData.resourceType);

        Debug.Log($"[Processor:{name}] Resource Produced: {recipeData.resourceType}, +{recipeData.produceAmount} (ResourceManager: {amountBefore} -> {amountAfter})");

        if (TutorialManager.Instance != null)
        {
            string itemTypeName = recipeData.resourceType.ToString();
            if (itemTypeName.Contains("Alloy") || itemTypeName.Contains("합금"))
            {
                TutorialManager.Instance.OnItemProduced("AlloyPlate");
            }
        }

        List<IStorage> storages = ResourceManager.Instance.GetAllStorages();
        foreach (IStorage storage in storages) {
            if (storage is MainStructure mainStructure) {
                mainStructure.AddResourceToStorageOnly(recipeData.resourceType, recipeData.produceAmount);
                return;
            }
        }
    }

    private int GetTotalCurrentIngredients()
    {
        return _currentIngredients.Values.Sum();
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
    public Vector3 AssignInteractionCell(Unit_Drone drone)
    {
        Vector3Int processorCell = BuildingManager.Instance.grid.WorldToCell(transform.position);
        if (!BuildingManager.Instance.GetBuildingAt(processorCell, out List<Vector3Int> occupiedCells)) {
            occupiedCells = new List<Vector3Int> { processorCell };
        }
        
        List<Vector3Int> availableCells = GetAvailableInteractionCells(occupiedCells);
        
        if (availableCells.Count == 0) {
            return transform.position;
        }
        
        if (_droneInteractionCells.TryGetValue(drone, out Vector3Int existingCell)) {
            if (availableCells.Contains(existingCell)) {
                return BuildingManager.Instance.grid.GetCellCenterWorld(existingCell);
            }
        }
        
        Vector3Int bestCell = availableCells[0];
        float minDistance = float.MaxValue;
        Vector3 dronePos = drone.transform.position;
        
        HashSet<Vector3Int> assignedCells = new HashSet<Vector3Int>(_droneInteractionCells.Values);
        
        foreach (Vector3Int cell in availableCells) {
            bool isUnassigned = !assignedCells.Contains(cell);
            float distance = Vector3.Distance(dronePos, BuildingManager.Instance.grid.GetCellCenterWorld(cell));
            
            if (isUnassigned && assignedCells.Contains(bestCell)) {
                bestCell = cell;
                minDistance = distance;
            }
            else if (isUnassigned == !assignedCells.Contains(bestCell)) {
                if (distance < minDistance) {
                    bestCell = cell;
                    minDistance = distance;
                }
            }
        }
        
        _droneInteractionCells[drone] = bestCell;
        
        return BuildingManager.Instance.grid.GetCellCenterWorld(bestCell);
    }
    
    private List<Vector3Int> GetAvailableInteractionCells(List<Vector3Int> occupiedCells)
    {
        HashSet<Vector3Int> interactionCells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);
        
        Vector3Int[] cardinalOffsets = {
            new (1, 0, 0), new (-1, 0, 0),
            new (0, 1, 0), new (0, -1, 0)
        };
        
        foreach (Vector3Int occupiedCell in occupiedSet) {
            foreach (Vector3Int offset in cardinalOffsets) {
                Vector3Int neighbor = occupiedCell + offset;
                
                if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                    interactionCells.Add(neighbor);
                }
            }
        }
        
        return interactionCells.ToList();
    }
    
    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) {
            return false;
        }
        return !BuildingManager.Instance.IsResourceTile(cell) && 
               !BuildingManager.Instance.IsBuildingTile(cell);
    }

    public void CancelRequest(ResourceRequest request)
    {
        if (_pendingRequests.Contains(request)) {
            _pendingRequests.Remove(request);
        }
    }

    public void CheckProductionLimits(ActiveRecipe recipe)
    {
        foreach (Unit_Drone drone in _assignedDrones) {
            bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
            bool isAssignedToRecipe = drone.CurrentRecipeTask != null;
            if (!hasPendingRequest && !isAssignedToRecipe) {
                RequestTask(drone);
            }
        }
    }

    public class ResourceRequest
    {
        public int amount;
        public Unit_Drone assignedDrone;
        public ResourceType type;
    }
}
