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
        // Disable VisibilityController - resources should never be visible
        _visibilityController = GetComponent<VisibilityController>();
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        // Also check in children
        _visibilityController = GetComponentInChildren<VisibilityController>();
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        // Hide all sprite renderers - only rule tiles are visible
        DisableAllSpriteRenderers();
    }

    private void OnEnable()
    {
        // Ensure VisibilityController stays disabled
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        // Ensure sprite renderers stay hidden even if VisibilityController tries to enable them
        DisableAllSpriteRenderers();
    }

    private void Start()
    {
        // Ensure VisibilityController stays disabled
        if (_visibilityController != null)
        {
            _visibilityController.enabled = false;
        }
        
        // Ensure sprite renderers stay hidden
        DisableAllSpriteRenderers();
        
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            cellPosition = BuildingManager.Instance.grid.WorldToCell(transform.position);
        }
        
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResourceNode(this);
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
        
        // Update nearby rule tiles when this resource is mined
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
