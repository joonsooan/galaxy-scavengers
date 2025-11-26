using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [Header("Resource Stats")]
    public ResourceType resourceType;
    [HideInInspector] public int amountToMine; // 채굴해 얻을 수 있는 자원의 총량
    [HideInInspector] public float timeToMinePerUnit; // 한 번 채굴을 완료하는 데 걸리는 시간

    [Header("Visuals")]
    [SerializeField] private Color highlightColor = Color.red;
    [HideInInspector] public Vector3Int cellPosition;
    
    [HideInInspector] public ResourceSpawner spawner;

    private Color _originalColor;
    private SpriteRenderer _sr;
    private Unit_Miner _reservedUnit;

    public bool IsReserved { get; private set; }

    public bool IsDepleted {
        get {
            return amountToMine <= 0;
        }
    }

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _originalColor = _sr.color;

        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.AddResourceNode(this);
            ResourceStats stats = ResourceManager.Instance.GetResourceStats(resourceType);
            
            if (stats != null)
            {
                amountToMine = stats.amountToMine;
                timeToMinePerUnit = stats.timeToMinePerUnit;
            }
        }
    }
    
    private void Start()
    {
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null)
        {
            cellPosition = BuildingManager.Instance.grid.WorldToCell(transform.position);
        }
    }
    
    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.NotifyResourceDestroyed(this);
        }
        
        if (ResourceManager.Instance != null) {
            ResourceManager.Instance.RemoveResourceNode(this);
        }
        
        if (BuildingManager.Instance != null) {
            BuildingManager.Instance.RemoveResourceTile(cellPosition);
        }
    }

    public bool Reserve(Unit_Miner unit)
    {
        if (IsReserved) {
            return false;
        }
        IsReserved = true;
        _reservedUnit = unit;
        if (_sr != null) {
            _sr.color = highlightColor;
        }

        return true;
    }

    public void Unreserve()
    {
        IsReserved = false;
        _reservedUnit = null;
        if (_sr != null) {
            _sr.color = _originalColor;
        }
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
}
