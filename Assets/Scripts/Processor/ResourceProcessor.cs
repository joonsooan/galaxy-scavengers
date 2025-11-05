using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceProcessor : Damageable, IClickable
{
    [SerializeField] private ResourceProcessorData processorData;

    private readonly List<Unit_Drone> _assignedDrones = new List<Unit_Drone>();
    private readonly Dictionary<ResourceType, int> _currentIngredients = new Dictionary<ResourceType, int>();
    private readonly List<ResourceRequest> _pendingRequests = new List<ResourceRequest>();

    private ProcessorRecipe _activeRecipe;
    private int _maxAssignedDrones;
    private int _maxIngredientStorage;
    private float _processingProgress;

    private List<ProcessorRecipe> _recipes;

    public ResourceProcessorData ProcessorData {
        get {
            return processorData;
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

    public bool IsProcessing { get; private set; }

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

        ResourceRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);
        if (request != null) {
            _pendingRequests.Remove(request);
        }
    }

    public void RequestTask(Unit_Drone drone)
    {
        if (_pendingRequests.Any(r => r.assignedDrone == drone)) {
            return;
        }

        ResourceRequest newRequest = FindNextIngredientRequest(drone.carryCapacity);
        if (newRequest != null) {
            newRequest.assignedDrone = drone;
            _pendingRequests.Add(newRequest);
            drone.SetTask_FetchResource(newRequest, this);
            return;
        }

        if (!IsProcessing && TryStartNextRecipe()) {
            drone.SetTask_Process(this);
            return;
        }

        if (IsProcessing) {
            drone.SetTask_Process(this);
            return;
        }

        drone.SetTask_Idle();
    }

    private ResourceRequest FindNextIngredientRequest(int droneCapacity)
    {
        int totalOnTheWay = _pendingRequests.Sum(r => r.amount);
        int totalInStorage = GetTotalCurrentIngredients();

        if (totalOnTheWay + totalInStorage >= _maxIngredientStorage) {
            return null;
        }

        if (_recipes == null) return null;

        foreach (ProcessorRecipe recipe in _recipes) {
            foreach (ResourceCost ingredient in recipe.ingredients) {
                int needed = ingredient.amount;
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

        TryStartNextRecipe();

        return canAddAmount > 0;
    }

    private bool TryStartNextRecipe()
    {
        if (IsProcessing) return false;
        if (_recipes == null) return false;

        foreach (ProcessorRecipe recipe in _recipes) {
            if (HasIngredientsFor(recipe)) {
                _activeRecipe = recipe;
                IsProcessing = true;
                _processingProgress = 0f;
                ConsumeIngredients(recipe);
                return true;
            }
        }
        return false;
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

    public void ProcessRecipeWork(float workAmount)
    {
        if (!IsProcessing || _activeRecipe == null) {
            return;
        }

        _processingProgress += workAmount;

        if (_processingProgress >= _activeRecipe.processingTime) {
            ProduceOutput();
            IsProcessing = false;
            _activeRecipe = null;
            _processingProgress = 0;

            TryStartNextRecipe();
        }
    }

    private void ProduceOutput()
    {
        ResourceManager.Instance.AddResource(_activeRecipe.product.resourceType, _activeRecipe.product.amount);
    }

    public int GetTotalCurrentIngredients()
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
