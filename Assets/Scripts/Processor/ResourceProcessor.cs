using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceProcessor : Damageable, IClickable
{
    [SerializeField] private ResourceProcessorData processorData;
    private readonly List<ActiveRecipe> _activeRecipes = new List<ActiveRecipe>();

    private readonly List<Unit_Drone> _assignedDrones = new List<Unit_Drone>();
    private readonly Dictionary<ResourceType, int> _currentIngredients = new Dictionary<ResourceType, int>();
    private readonly List<ResourceRequest> _pendingRequests = new List<ResourceRequest>();

    private int _maxAssignedDrones;
    private int _maxIngredientStorage;

    private List<ProcessorRecipe> _recipes;

    public ResourceProcessorData ProcessorData {
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

    public bool IsProcessing {
        get {
            return _activeRecipes.Any(r => r.isProcessing);
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

    protected override void OnEnable()
    {
        base.OnEnable();
        BuildingManager.Instance?.RegisterProcessor(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        BuildingManager.Instance?.UnregisterProcessor(this);

        List<Unit_Drone> dronesToRelease = new List<Unit_Drone>(_assignedDrones);
        foreach (Unit_Drone drone in dronesToRelease) {
            drone.AssignProcessor(null);
        }
    }

    public void OnClicked()
    {
        OnProcessorClicked?.Invoke(this);
    }

    public static event Action<ResourceProcessor> OnProcessorClicked;

    private void SortRecipes()
    {
        if (_recipes == null) return;
        _recipes.Sort((a, b) => a.priority.CompareTo(b.priority));
    }

    public void AssignDrone(Unit_Drone drone)
    {
        if (!_assignedDrones.Contains(drone) && !IsFull) {
            _assignedDrones.Add(drone);
            Debug.Log($"[Processor:{name}] Drone '{drone.name}' assigned. Count={_assignedDrones.Count}/{_maxAssignedDrones}");
            RequestTask(drone);
        }
    }

    public void ReleaseDrone(Unit_Drone drone)
    {
        _assignedDrones.Remove(drone);

        ReleaseDroneFromRecipe(drone);

        ResourceRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);
        if (request != null) {
            _pendingRequests.Remove(request);
        }
        Debug.Log($"[Processor:{name}] Drone '{drone.name}' released. Count={_assignedDrones.Count}/{_maxAssignedDrones}");
    }

    public void RequestTask(Unit_Drone drone)
    {
        // 0. 이 드론이 이미 맡은 '진행 중인' 작업이 있는지 확인
        if (drone.CurrentRecipeTask != null && drone.CurrentRecipeTask.isProcessing) {
            // 이미 작업 중이면 재할당하지 않음 (이미 Processing 상태이고 목표로 이동 중이거나 작업 중)
            // 재할당하면 불필요한 pathfinding이 발생하고 무한 루프를 유발할 수 있음
            Debug.Log($"[Processor:{name}] RequestTask: Drone '{drone.name}' already processing {drone.CurrentRecipeTask.recipeData.resourceType}, skipping reassignment");
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
                // isProcessing은 드론이 프로세서에 도착했을 때 true로 설정됨
                
                Debug.Log($"[Processor:{name}] RequestTask: Assigned recipe {recipe.recipeData.resourceType} to drone '{drone.name}'. Ingredients ready, waiting for drone to arrive.");
                drone.SetTask_Process(this, recipe);
                return;
            }
        }

        // 3. 담당자 없는 '진행 중인' 작업 이어받기
        // (예: 드론이 파괴되었다가 다른 드론이 할당된 경우)
        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null && recipe.isProcessing) {
                recipe.assignedDrone = drone; // 이 드론이 이어받음
                drone.SetTask_Process(this, recipe);
                Debug.Log($"[Processor:{name}] RequestTask: Drone '{drone.name}' picks up ongoing {recipe.recipeData.resourceType}");
                return;
            }
        }

        // 4. 할 일이 없음
        drone.SetTask_Idle();
        Debug.Log($"[Processor:{name}] RequestTask: No work for Drone '{drone.name}', set Idle");
    }

    private bool PassesProductionCapCheck(ActiveRecipe recipe)
    {
        if (recipe.maxProductionLimit <= 0)
        {
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
                continue; // 다음 레시피 확인
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

        // After depositing ingredients, check if any recipe is now ready to start processing
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
            Debug.Log($"[Processor:{name}] Consume: {ingredient.resourceType} -{ingredient.amount}. Remaining {_currentIngredients[ingredient.resourceType]}");
        }
    }

    // Checks if any recipe has all required ingredients and assigns an idle drone to process it.
    private void CheckAndAssignReadyRecipes()
    {
        // Find recipes that are ready to process (all ingredients available, not already processing, no drone assigned)
        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null &&
                !recipe.isProcessing &&
                HasIngredientsFor(recipe.recipeData) &&
                PassesProductionCapCheck(recipe)) {
                
                // Try to find an idle drone (one that will request a task soon)
                // Check all assigned drones and see if any are idle or about to become idle
                // The RequestTask method will handle assignment when drones request tasks
                Debug.Log($"[Processor:{name}] CheckAndAssignReadyRecipes: Recipe {recipe.recipeData.resourceType} is ready with all ingredients. Will be assigned when drone requests task.");
                
                // Try to immediately assign to any drone that requests a task
                // We trigger this by having drones that just delivered request a task
            }
        }
    }

    public void ProcessRecipeWork(ActiveRecipe recipe, float workAmount)
    {
        if (recipe.assignedDrone == null) {
            return;
        }

        // If processing hasn't started yet, check if we should start now (drone has arrived)
        if (!recipe.isProcessing) {
            // Check if all ingredients are still available (they should be, but verify)
            if (HasIngredientsFor(recipe.recipeData)) {
                // Start processing: consume ingredients and begin processing
                ConsumeIngredients(recipe.recipeData);
                recipe.isProcessing = true;
                recipe.processingProgress = 0f;
                Debug.Log($"[Processor:{name}] Processing STARTED: {recipe.recipeData.resourceType} by drone '{recipe.assignedDrone.name}'. Processing time: {recipe.recipeData.processingTime}s");
            }
            else {
                // Ingredients were consumed by another recipe or removed - cancel this recipe
                Debug.LogWarning($"[Processor:{name}] Processing CANCELLED: {recipe.recipeData.resourceType} - ingredients no longer available");
                recipe.assignedDrone = null;
                recipe.processingProgress = 0f;
                return;
            }
        }

        // Process the recipe
        float previousProgress = recipe.processingProgress;
        recipe.processingProgress += workAmount;
        
        // Log progress periodically (every 25% completion milestone)
        float progressPercent = (recipe.processingProgress / recipe.recipeData.processingTime) * 100f;
        float previousPercent = (previousProgress / recipe.recipeData.processingTime) * 100f;
        
        int currentQuarter = Mathf.FloorToInt(Mathf.Min(progressPercent, 100f) / 25f);
        int previousQuarter = Mathf.FloorToInt(Mathf.Min(previousPercent, 100f) / 25f);
        
        // Log when we cross a 25% milestone (25%, 50%, 75%, 100%)
        if (currentQuarter > previousQuarter) {
            Debug.Log($"[Processor:{name}] Processing PROGRESS: {recipe.recipeData.resourceType} - {progressPercent:F1}% ({recipe.processingProgress:F2}/{recipe.recipeData.processingTime:F2}s)");
        }

        // Check if processing is complete
        if (recipe.processingProgress >= recipe.recipeData.processingTime) {
            ProduceOutput(recipe.recipeData);

            Debug.Log($"[Processor:{name}] Processing COMPLETED: {recipe.recipeData.resourceType} by drone '{recipe.assignedDrone.name}'. Output: {recipe.recipeData.produceAmount} {recipe.recipeData.resourceType}");

            Unit_Drone completedDrone = recipe.assignedDrone;
            recipe.isProcessing = false;
            recipe.processingProgress = 0;
            recipe.assignedDrone = null;
            
            // Release the drone and assign a new task
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
        ResourceManager.Instance.AddResource(recipeData.resourceType, recipeData.produceAmount);
        Debug.Log($"[Processor:{name}] Resource Produced: {recipeData.resourceType}, +{recipeData.produceAmount}");
    }

    private int GetTotalCurrentIngredients()
    {
        return _currentIngredients.Values.Sum();
    }

    public Vector3 GetPosition()
    {
        return transform.position;
    }

    public void CancelRequest(ResourceRequest request)
    {
        if (_pendingRequests.Contains(request)) {
            _pendingRequests.Remove(request);
        }
    }
    
    public void CheckProductionLimits(ActiveRecipe recipe)
    {
        int currentAmount = ResourceManager.Instance.GetResourceAmount(recipe.recipeData.resourceType);

        // Req #3: 현재 생산 중인데 한도에 도달했는지 확인
        if (recipe.isProcessing && recipe.assignedDrone != null && currentAmount >= recipe.maxProductionLimit)
        {
            Unit_Drone drone = recipe.assignedDrone;

            // 작업 중지
            recipe.isProcessing = false;
            recipe.processingProgress = 0;
            recipe.assignedDrone = null;

            // 드론을 대기 상태로 변경 (프로세서에는 할당된 상태 유지)
            // (Req #4)
            drone.SetTask_Idle(); 
        }

        // Req #4: 유휴 드론이 있는지 확인하고, 새 작업이 생겼는지 확인
        // 이 프로세서에 할당된 드론 중, 현재 작업이 없는(Idle) 드론을 찾음
        foreach (var drone in _assignedDrones.Where(d => d.CurrentRecipeTask == null))
        {
            RequestTask(drone);
        }
    }

    public class ResourceRequest
    {
        public int amount;
        public Unit_Drone assignedDrone;
        public ResourceType type;
    }
}
