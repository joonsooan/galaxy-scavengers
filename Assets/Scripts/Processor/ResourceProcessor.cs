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
            _activeRecipes.Add(new ActiveRecipe(recipeData));
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
    }

    public void RequestTask(Unit_Drone drone)
    {
        // 0. 이 드론이 이미 맡은 '진행 중인' 작업이 있는지 확인
        if (drone.CurrentRecipeTask != null && drone.CurrentRecipeTask.isProcessing) {
            // 이미 작업이 있다면, 계속 그 작업을 하도록 함
            drone.SetTask_Process(this, drone.CurrentRecipeTask);
            return;
        }

        // 1. 재료 운반 작업 찾기
        ResourceRequest newRequest = FindNextIngredientRequest(drone.carryCapacity);
        if (newRequest != null) {
            newRequest.assignedDrone = drone;
            _pendingRequests.Add(newRequest);
            drone.SetTask_FetchResource(newRequest, this);
            return;
        }

        // 2. 새로운 '처리' 작업 찾기
        foreach (ActiveRecipe recipe in _activeRecipes) {
            // 이 레시피가 (작업 가능하고) && (담당자가 없고) && (재료가 있고) && (생산 제한에 걸리지 않았는지) 확인
            if (recipe.assignedDrone == null &&
                !recipe.isProcessing &&
                HasIngredientsFor(recipe.recipeData) &&
                PassesProductionCapCheck(recipe)) {
                // 작업 시작
                ConsumeIngredients(recipe.recipeData); // 재료 소모
                recipe.isProcessing = true;
                recipe.processingProgress = 0f;
                recipe.assignedDrone = drone; // 드론 배정

                drone.SetTask_Process(this, recipe); // 드론에게 이 레시피를 처리하라고 명령
                return;
            }
        }

        // 3. 담당자 없는 '진행 중인' 작업 이어받기
        // (예: 드론이 파괴되었다가 다른 드론이 할당된 경우)
        foreach (ActiveRecipe recipe in _activeRecipes) {
            if (recipe.assignedDrone == null && recipe.isProcessing) {
                recipe.assignedDrone = drone; // 이 드론이 이어받음
                drone.SetTask_Process(this, recipe);
                return;
            }
        }

        // 4. 할 일이 없음
        drone.SetTask_Idle();
    }

    private bool PassesProductionCapCheck(ActiveRecipe recipe)
    {
        if (recipe.Mode == ProductionMode.Infinite) {
            return true;
        }

        int currentAmount = ResourceManager.Instance.GetResourceAmount(recipe.recipeData.product.resourceType);
        return currentAmount < recipe.MaxProductionLimit;
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

    public void ProcessRecipeWork(ActiveRecipe recipe, float workAmount)
    {
        if (!recipe.isProcessing || recipe.assignedDrone == null) {
            return;
        }

        recipe.processingProgress += workAmount;

        if (recipe.processingProgress >= recipe.recipeData.processingTime) {
            ProduceOutput(recipe.recipeData);

            recipe.isProcessing = false;
            recipe.processingProgress = 0;

            recipe.assignedDrone = null;
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
        ResourceManager.Instance.AddResource(recipeData.product.resourceType, recipeData.product.amount);
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

    public class ResourceRequest
    {
        public int amount;
        public Unit_Drone assignedDrone;
        public ResourceType type;
    }
}
