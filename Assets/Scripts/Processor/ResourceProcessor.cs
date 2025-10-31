using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class ResourceProcessor : Damageable
{
    public class ResourceRequest
    {
        public ResourceType type;
        public int amount;
        public Unit_Drone assignedDrone;
    }

    [SerializeField] private ResourceProcessorData processorData;

    private List<ProcessorRecipe> _recipes;
    private int _maxIngredientStorage;
    private int _maxAssignedDrones;

    private readonly List<Unit_Drone> _assignedDrones = new();
    private readonly List<ResourceRequest> _pendingRequests = new();
    private readonly Dictionary<ResourceType, int> _currentIngredients = new();
    
    private ProcessorRecipe _activeRecipe;
    private float _processingProgress;
    private bool _isProcessing;

    public bool IsFull => _assignedDrones.Count >= _maxAssignedDrones;
    public bool IsProcessing => _isProcessing;

    protected override void Awake()
    {
        base.Awake();

        if (processorData == null)
        {
            return;
        }

        _recipes = processorData.Recipes;
        _maxIngredientStorage = processorData.MaxIngredientStorage;
        _maxAssignedDrones = processorData.MaxAssignedDrones;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
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
        foreach (var drone in dronesToRelease)
        {
            drone.AssignProcessor(null);
        }
    }

    private void SortRecipes()
    {
        if (_recipes == null) return;
        _recipes.Sort((a, b) => a.priority.CompareTo(b.priority));
    }
    
    public void AssignDrone(Unit_Drone drone)
    {
        if (!_assignedDrones.Contains(drone) && !IsFull)
        {
            _assignedDrones.Add(drone);
        }
    }

    public void ReleaseDrone(Unit_Drone drone)
    {
        _assignedDrones.Remove(drone);
        
        ResourceRequest request = _pendingRequests.FirstOrDefault(r => r.assignedDrone == drone);
        if (request != null)
        {
            _pendingRequests.Remove(request);
        }
    }

    public void RequestTask(Unit_Drone drone)
    {
        if (_pendingRequests.Any(r => r.assignedDrone == drone))
        {
            return; 
        }

        ResourceRequest newRequest = FindNextIngredientRequest(drone.carryCapacity);
        if (newRequest != null)
        {
            newRequest.assignedDrone = drone;
            _pendingRequests.Add(newRequest);
            drone.SetTask_FetchResource(newRequest, this);
            return;
        }

        if (!_isProcessing && TryStartNextRecipe())
        {
            drone.SetTask_Process(this);
            return;
        }

        if (_isProcessing)
        {
            drone.SetTask_Process(this);
            return;
        }
        
        drone.SetTask_Idle();
    }
    
    private ResourceRequest FindNextIngredientRequest(int droneCapacity)
    {
        int totalOnTheWay = _pendingRequests.Sum(r => r.amount);
        int totalInStorage = GetTotalCurrentIngredients();

        if (totalOnTheWay + totalInStorage >= _maxIngredientStorage)
        {
            return null; 
        }
        
        if (_recipes == null) return null;

        foreach (var recipe in _recipes)
        {
            foreach (var ingredient in recipe.ingredients)
            {
                int needed = ingredient.amount;
                int inStorage = _currentIngredients.ContainsKey(ingredient.resourceType) ? _currentIngredients[ingredient.resourceType] : 0;
                int onTheWay = _pendingRequests
                    .Where(r => r.type == ingredient.resourceType)
                    .Sum(r => r.amount);

                int stillNeeded = needed - inStorage - onTheWay;

                if (stillNeeded > 0)
                {
                    int spaceAvailable = _maxIngredientStorage - totalInStorage - totalOnTheWay;
                    int amountToFetch = Mathf.Min(stillNeeded, droneCapacity, spaceAvailable);

                    if (amountToFetch > 0)
                    {
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
        
        if (request == null || request.type != type)
        {
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
        if (_isProcessing) return false;
        if (_recipes == null) return false;

        foreach (var recipe in _recipes)
        {
            if (HasIngredientsFor(recipe))
            {
                _activeRecipe = recipe;
                _isProcessing = true;
                _processingProgress = 0f;
                ConsumeIngredients(recipe);
                return true;
            }
        }
        return false;
    }
    
    private bool HasIngredientsFor(ProcessorRecipe recipe)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            if (!_currentIngredients.ContainsKey(ingredient.resourceType) || _currentIngredients[ingredient.resourceType] < ingredient.amount)
            {
                return false;
            }
        }
        return true;
    }

    private void ConsumeIngredients(ProcessorRecipe recipe)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            _currentIngredients[ingredient.resourceType] -= ingredient.amount;
        }
    }
    
    public void ProcessRecipeWork(float workAmount)
    {
        if (!_isProcessing || _activeRecipe == null)
        {
            return;
        }

        _processingProgress += workAmount;

        if (_processingProgress >= _activeRecipe.processingTime)
        {
            ProduceOutput();
            _isProcessing = false;
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
        if (_pendingRequests.Contains(request))
        {
            _pendingRequests.Remove(request);
        }
    }
}