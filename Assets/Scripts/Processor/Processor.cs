using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Processor : Damageable, IClickable, IAetherConsumer
{
    private const float TaskCheckInterval = 1f;
    [SerializeField] private ProcessorData processorData;
    [Header("Aether Consumption")]
    [SerializeField] private int aetherConsumptionPerSecond = 1;

    private readonly List<ActiveRecipe> _activeRecipes = new List<ActiveRecipe>();

    private readonly List<Unit_Drone> _assignedDrones = new List<Unit_Drone>();
    private readonly Dictionary<ResourceType, int> _currentIngredients = new Dictionary<ResourceType, int>();
    private readonly Dictionary<Unit_Drone, Vector3Int> _droneInteractionCells = new Dictionary<Unit_Drone, Vector3Int>();
    private readonly List<ResourceRequest> _pendingRequests = new List<ResourceRequest>();
    private AetherConsumptionManager _aetherConsumptionManager;
    private bool _isInitialized;
    private float _lastTaskCheckTime;

    private int _maxAssignedDrones;
    private int _maxIngredientStorage;

    private List<ProcessorRecipe> _recipes;

    public ProcessorData ProcessorData {
        get {
            return processorData;
        }
    }

    public IReadOnlyList<ActiveRecipe> ActiveRecipes {
        get {
            return _activeRecipes;
        }
    }

    public IReadOnlyList<Unit_Drone> AssignedDrones {
        get {
            return _assignedDrones;
        }
    }

    public bool IsFull {
        get {
            return _assignedDrones.Count >= _maxAssignedDrones;
        }
    }


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

    private void Update()
    {
        if (!_isInitialized) return;

        if (Time.time - _lastTaskCheckTime >= TaskCheckInterval) {
            _lastTaskCheckTime = Time.time;
            CheckIdleDronesForTasks();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BuildingManager.Instance?.RegisterProcessor(this);

        if (!BuildingManager.IsBuildingProperlyPlaced(transform)) {
            _isInitialized = false;
            return;
        }

        FindAndCacheAetherManager();
        if (_aetherConsumptionManager != null) {
            _aetherConsumptionManager.RegisterConsumer(this);
        }

        ResourceManager.OnResourceAmountChanged += OnResourceAmountChanged;
        _lastTaskCheckTime = Time.time;
        _isInitialized = true;
    }

    protected override void OnDisable()
    {
        ResourceManager.OnResourceAmountChanged -= OnResourceAmountChanged;

        if (_aetherConsumptionManager != null) {
            _aetherConsumptionManager.UnregisterConsumer(this);
        }

        base.OnDisable();
        BuildingManager.Instance?.UnregisterProcessor(this);

        List<Unit_Drone> dronesToRelease = new List<Unit_Drone>(_assignedDrones);
        foreach (Unit_Drone drone in dronesToRelease) {
            drone.AssignProcessor(null);
        }
    }

    // IAetherConsumer implementation
    public int AetherConsumptionPerSecond {
        get {
            return aetherConsumptionPerSecond;
        }
    }

    public bool IsOperational { get; private set; } = true;

    public void OnAetherUnavailable()
    {
        if (IsOperational) {
            IsOperational = false;

            // Stop all processing - release drones from recipes
            foreach (ActiveRecipe recipe in _activeRecipes) {
                if (recipe.isProcessing && recipe.assignedDrone != null) {
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
        if (!IsOperational) {
            IsOperational = true;

            // Resume processing - request tasks for idle drones
            foreach (Unit_Drone drone in _assignedDrones) {
                if (drone != null && drone.HasCheckedIn && drone.CurrentRecipeTask == null) {
                    bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
                    if (!hasPendingRequest) {
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

    private void CheckIdleDronesForTasks()
    {
        if (!IsOperational) return;

        foreach (Unit_Drone drone in _assignedDrones) {
            if (drone != null && drone.HasCheckedIn && drone.CurrentRecipeTask == null) {
                bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
                if (!hasPendingRequest) {
                    RequestTask(drone);
                    break;
                }
            }
        }
    }

    private void FindAndCacheAetherManager()
    {
        if (_aetherConsumptionManager == null) {
            _aetherConsumptionManager = FindFirstObjectByType<AetherConsumptionManager>();
        }
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

        if (drone.CurrentRecipeTask != null) {
            ActiveRecipe assignedRecipe = _activeRecipes.FirstOrDefault(r => r.assignedDrone == drone && r == drone.CurrentRecipeTask);

            if (assignedRecipe != null && assignedRecipe.assignedDrone == drone) {
                drone.SetTask_Process(this, assignedRecipe);
                return;
            }
            ReleaseDroneFromRecipe(drone);
            drone.SetTask_Idle();
        }

        ResourceRequest existingRequest = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);
        if (existingRequest != null) {
            return;
        }

        ResourceRequest newRequest = FindNextIngredientRequest(drone.carryCapacity);
        if (newRequest != null) {
            newRequest.assignedDrone = drone;
            _pendingRequests.Add(newRequest);
            drone.SetTask_FetchResource(newRequest, this);
            return;
        }

        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null &&
                !recipe.isProcessing &&
                HasIngredientsFor(recipe.recipeData) &&
                PassesProductionCapCheck(recipe)) {
                recipe.assignedDrone = drone;
                recipe.processingProgress = 0f;

                drone.SetTask_Process(this, recipe);
                return;
            }
        }

        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null && recipe.isProcessing) {
                recipe.assignedDrone = drone;
                drone.SetTask_Process(this, recipe);
                return;
            }
        }

        drone.SetTask_Idle();
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
            return false;
        }

        int totalIngredients = GetTotalCurrentIngredients();
        int canAddAmount = Mathf.Min(amount, _maxIngredientStorage - totalIngredients);

        _currentIngredients[type] += canAddAmount;
        _pendingRequests.Remove(request);

        if (canAddAmount > 0) {
            CheckAndAssignReadyRecipes();

            if (drone != null && drone.HasCheckedIn && drone.CurrentRecipeTask == null) {
                bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
                if (!hasPendingRequest) {
                    foreach (ActiveRecipe recipe in _activeRecipes) {
                        if (recipe.assignedDrone == null &&
                            !recipe.isProcessing &&
                            HasIngredientsFor(recipe.recipeData) &&
                            PassesProductionCapCheck(recipe)) {
                            recipe.assignedDrone = drone;
                            recipe.processingProgress = 0f;
                            drone.SetTask_Process(this, recipe);
                            return canAddAmount > 0;
                        }
                    }
                }
            }
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

    private void OnResourceAmountChanged(ResourceType type, int amount)
    {
        bool ingredientChanged = false;
        foreach (ActiveRecipe recipe in _activeRecipes) {
            foreach (ResourceCost ingredient in recipe.recipeData.ingredients) {
                if (ingredient.resourceType == type) {
                    ingredientChanged = true;
                    break;
                }
            }
            if (ingredientChanged) break;
        }

        if (ingredientChanged) {
            foreach (ActiveRecipe recipe in _activeRecipes) {
                if (recipe.assignedDrone != null && !recipe.isProcessing) {
                    if (!HasIngredientsFor(recipe.recipeData)) {
                        Unit_Drone drone = recipe.assignedDrone;
                        ReleaseDroneFromRecipe(drone);
                        drone.SetTask_Idle();
                    }
                }
            }

            CheckAndAssignReadyRecipes();
        }
    }

    private void CheckAndAssignReadyRecipes()
    {
        bool assignedRecipe = false;

        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null &&
                !recipe.isProcessing &&
                HasIngredientsFor(recipe.recipeData) &&
                PassesProductionCapCheck(recipe)) {
                foreach (Unit_Drone drone in _assignedDrones) {
                    if (drone != null && drone.HasCheckedIn && drone.CurrentRecipeTask == null) {
                        bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
                        if (!hasPendingRequest) {
                            RequestTask(drone);
                            assignedRecipe = true;
                            break;
                        }
                    }
                }

                if (assignedRecipe) break;
            }
        }
    }

    public void ProcessRecipeWork(ActiveRecipe recipe, float workAmount)
    {
        // Don't process if not operational (no aether)
        if (!IsOperational) {
            if (recipe.assignedDrone != null) {
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
                Unit_Drone drone = recipe.assignedDrone;
                recipe.assignedDrone = null;
                recipe.processingProgress = 0f;
                if (drone != null) {
                    drone.SetTask_Idle();
                }
                return;
            }
        }

        float previousProgress = recipe.processingProgress;
        recipe.processingProgress += workAmount;

        float progressPercent = recipe.processingProgress / recipe.recipeData.processingTime * 100f;
        float previousPercent = previousProgress / recipe.recipeData.processingTime * 100f;

        int currentQuarter = Mathf.FloorToInt(Mathf.Min(progressPercent, 100f) / 25f);
        int previousQuarter = Mathf.FloorToInt(Mathf.Min(previousPercent, 100f) / 25f);


        if (recipe.processingProgress >= recipe.recipeData.processingTime) {
            ProduceOutput(recipe.recipeData);

            Unit_Drone completedDrone = recipe.assignedDrone;
            recipe.isProcessing = false;
            recipe.processingProgress = 0;
            recipe.assignedDrone = null;

            if (completedDrone != null) {
                completedDrone.SetTask_Idle();
            }

            CheckProductionLimits(recipe);
        }
    }

    public void ReleaseDroneFromRecipe(Unit_Drone drone)
    {
        ActiveRecipe recipe = _activeRecipes.FirstOrDefault(r => r.assignedDrone == drone);
        if (recipe != null) {
            recipe.assignedDrone = null;
        }
    }

    private void ProduceOutput(ProcessorRecipe recipeData)
    {
        ResourceManager.Instance.AddResource(recipeData.resourceType, recipeData.produceAmount);

        if (TutorialManager.Instance != null) {
            string itemTypeName = recipeData.resourceType.ToString();
            if (itemTypeName.Contains("Alloy") || itemTypeName.Contains("합금")) {
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
            Vector3Int fallbackCell = FindClosestWalkableCell(occupiedCells, drone.transform.position);
            if (fallbackCell != new Vector3Int(int.MinValue, int.MinValue, int.MinValue)) {
                return BuildingManager.Instance.grid.GetCellCenterWorld(fallbackCell);
            }
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
            bool isGloballyAssigned = UnitMovement.IsCellAssigned(cell);
            if (isGloballyAssigned && isUnassigned) continue;

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
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };
        Vector3Int[] diagonalOffsets = {
            new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
        };

        foreach (Vector3Int occupiedCell in occupiedSet) {
            foreach (Vector3Int offset in cardinalOffsets) {
                Vector3Int neighbor = occupiedCell + offset;
                if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                    interactionCells.Add(neighbor);
                }
            }
        }

        if (interactionCells.Count == 0) {
            foreach (Vector3Int occupiedCell in occupiedSet) {
                foreach (Vector3Int offset in diagonalOffsets) {
                    Vector3Int neighbor = occupiedCell + offset;
                    if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                        interactionCells.Add(neighbor);
                    }
                }
            }
        }

        if (interactionCells.Count == 0) {
            foreach (Vector3Int occupiedCell in occupiedSet) {
                for (int dx = -2; dx <= 2; dx++) {
                    for (int dy = -2; dy <= 2; dy++) {
                        if (dx == 0 && dy == 0) continue;
                        Vector3Int neighbor = occupiedCell + new Vector3Int(dx, dy, 0);
                        if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                            interactionCells.Add(neighbor);
                        }
                    }
                }
            }
        }

        return interactionCells.ToList();
    }

    private Vector3Int FindClosestWalkableCell(List<Vector3Int> occupiedCells, Vector3 fromPos)
    {
        if (BuildingManager.Instance == null) return new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        HashSet<Vector3Int> occupiedSet = new HashSet<Vector3Int>(occupiedCells);
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>(occupiedSet);
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        foreach (Vector3Int cell in occupiedCells) {
            queue.Enqueue(cell);
        }

        Vector3Int[] allOffsets = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
        };

        Vector3Int bestCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        float minDistance = float.MaxValue;
        const int maxSearchRadius = 8;

        for (int i = 0; i < maxSearchRadius && queue.Count > 0; i++) {
            int levelCount = queue.Count;
            for (int j = 0; j < levelCount; j++) {
                Vector3Int current = queue.Dequeue();
                foreach (Vector3Int offset in allOffsets) {
                    Vector3Int neighbor = current + offset;
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    if (!occupiedSet.Contains(neighbor) && IsCellWalkable(neighbor)) {
                        float dist = Vector3.Distance(fromPos, BuildingManager.Instance.grid.GetCellCenterWorld(neighbor));
                        if (dist < minDistance) {
                            minDistance = dist;
                            bestCell = neighbor;
                        }
                    }
                    queue.Enqueue(neighbor);
                }
            }
        }

        return bestCell;
    }

    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) {
            return false;
        }
        if (BuildingManager.Instance.IsBuildingTile(cell)) return false;
        if (BuildingManager.Instance.IsResourceTile(cell)) return false;
        if (BuildingManager.Instance.IsTerrainCell(cell)) return false;
        if (BuildingManager.Instance.IsMainStructureCell(cell) ||
            BuildingManager.Instance.GetBuildingAt(cell, out _)) {
            return false;
        }
        return true;
    }

    public void CancelRequest(ResourceRequest request)
    {
        if (_pendingRequests.Contains(request)) {
            _pendingRequests.Remove(request);
        }
    }

    public void CheckProductionLimits(ActiveRecipe recipe)
    {
        if (recipe.maxProductionLimit <= 0) {
            if (recipe.assignedDrone != null) {
                Unit_Drone drone = recipe.assignedDrone;
                ReleaseDroneFromRecipe(drone);
                drone.SetTask_Idle();
            }
            return;
        }

        int currentAmount = ResourceManager.Instance.GetResourceAmount(recipe.recipeData.resourceType);
        if (currentAmount >= recipe.maxProductionLimit) {
            if (recipe.assignedDrone != null) {
                Unit_Drone drone = recipe.assignedDrone;
                ReleaseDroneFromRecipe(drone);
                drone.SetTask_Idle();
            }
            return;
        }

        if (recipe.assignedDrone != null) {
            return;
        }

        foreach (Unit_Drone drone in _assignedDrones) {
            bool hasPendingRequest = _pendingRequests.Any(r => r.assignedDrone == drone);
            bool isAssignedToRecipe = drone.CurrentRecipeTask != null;
            if (!hasPendingRequest && !isAssignedToRecipe) {
                RequestTask(drone);
                break;
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
