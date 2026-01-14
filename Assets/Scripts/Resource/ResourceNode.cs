using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [Header("Resource Stats")]
    public ResourceType resourceType;
    [HideInInspector] public int amountToMine; // 채굴해 얻을 수 있는 자원의 총량
    [HideInInspector] public float timeToMinePerUnit; // 한 번 채굴을 완료하는 데 걸리는 시간

    [HideInInspector] public Vector3Int cellPosition;
    
    private Unit_Miner _reservedUnit;

    public bool IsReserved { get; private set; }
    public bool IsDepleted => amountToMine <= 0;

    private VisibilityController _visibilityController;
    
    private void Awake()
    {
        _visibilityController = GetComponent<VisibilityController>();
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        _visibilityController = GetComponentInChildren<VisibilityController>();
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        DisableAllSpriteRenderers();
    }

    private void OnEnable()
    {
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        DisableAllSpriteRenderers();
        
        InitializeResourceNode();
    }

    private void Start()
    {
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        DisableAllSpriteRenderers();
        
        InitializeResourceNode();
    }
    
    private void InitializeResourceNode()
    {
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            cellPosition = BuildingManager.Instance.grid.WorldToCell(transform.position);
        }
        
        if (ResourceManager.Instance != null)
        {
            List<ResourceNode> allResources = ResourceManager.Instance.GetAllResources();
            if (!allResources.Contains(this))
            {
                ResourceManager.Instance.AddResourceNode(this);
            }
            
            ResourceStats stats = ResourceManager.Instance.GetResourceStats(resourceType);
            
            if (stats != null)
            {
                amountToMine = stats.amountToMine;
                timeToMinePerUnit = stats.timeToMinePerUnit;
            }
        }
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.RemoveResourceNode(this);
        }
        
        if (MapObjectSpawner.Instance != null && cellPosition != Vector3Int.zero)
        {
            MapObjectSpawner.Instance.UpdateNearbyRuleTiles(cellPosition);
        }
    }

    public bool Reserve(Unit_Miner unit)
    {
        if (IsReserved) {
            return false;
        }
        IsReserved = true;
        _reservedUnit = unit;
        return true;
    }

    public void Unreserve()
    {
        IsReserved = false;
        _reservedUnit = null;
    }

    public Unit_Miner GetReservedUnit()
    {
        return  _reservedUnit;
    }

    public int Mine(int workAmount)
    {
        int amountMined = Mathf.Min(amountToMine, workAmount);
        amountToMine -= amountMined;

        if (IsDepleted) {
            Destroy(gameObject);
        }

        return amountMined;
    }
    
    private void DisableAllSpriteRenderers()
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
            {
                sr.enabled = false;
            }
        }
    }
}
