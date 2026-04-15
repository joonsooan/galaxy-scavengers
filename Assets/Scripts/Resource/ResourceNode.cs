using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [Header("Resource Stats")]
    public ResourceType resourceType;
    [HideInInspector] public int amountToMine;
    [HideInInspector] public float timeToMinePerUnit;

    [HideInInspector] public Vector3Int cellPosition;
    
    [Header("UI")]
    [SerializeField] private ProductionProgressSlider progressSliderPrefab;

    private Unit_Miner _reservedUnit;
    private int _initialAmountToMine;
    private ProductionProgressSlider _progressSliderInstance;
    private float _lastMinedTime = -999f;
    private readonly HashSet<int> _activeMiningUnitIds = new HashSet<int>();
    private const float SliderHideDelay = 1f;

    public bool IsReserved { get; private set; }
    public bool IsDepleted => amountToMine <= 0;
    public int InitialAmountToMine => _initialAmountToMine;

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
                _initialAmountToMine = stats.amountToMine;
            }
        }
    }

    private void OnDestroy()
    {
        HideProgressSlider();
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
        _lastMinedTime = Time.time;
        int amountMined = Mathf.Min(amountToMine, workAmount);
        amountToMine -= amountMined;
        UpdateProgressSlider();

        if (IsDepleted) {
            HideProgressSlider();
            Destroy(gameObject);
        }

        return amountMined;
    }

    public void BeginMining(UnitBase unit)
    {
        if (unit == null)
        {
            return;
        }
        _activeMiningUnitIds.Add(unit.GetInstanceID());
        EnsureProgressSliderVisible();
        RefreshProgressSliderValue();
    }

    public void EndMining(UnitBase unit)
    {
        if (unit == null)
        {
            return;
        }
        _activeMiningUnitIds.Remove(unit.GetInstanceID());
        if (_activeMiningUnitIds.Count == 0)
        {
            _lastMinedTime = Time.time;
        }
    }

    private void Update()
    {
        if (_progressSliderInstance != null && !IsActivelyBeingMined() && (Time.time - _lastMinedTime) >= SliderHideDelay)
        {
            HideProgressSlider();
        }
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

    private void UpdateProgressSlider()
    {
        if (_initialAmountToMine <= 0 || amountToMine <= 0)
        {
            HideProgressSlider();
            return;
        }
        EnsureProgressSliderVisible();
        RefreshProgressSliderValue();
    }

    private bool IsActivelyBeingMined()
    {
        return _activeMiningUnitIds.Count > 0;
    }

    private void HideProgressSlider()
    {
        if (_progressSliderInstance != null)
        {
            Destroy(_progressSliderInstance.gameObject);
            _progressSliderInstance = null;
        }
    }

    private void EnsureProgressSliderVisible()
    {
        if (_progressSliderInstance == null && progressSliderPrefab != null)
        {
            _progressSliderInstance = Instantiate(progressSliderPrefab);
            _progressSliderInstance.Initialize(transform);
            _progressSliderInstance.gameObject.SetActive(true);
        }
    }

    private void RefreshProgressSliderValue()
    {
        if (_progressSliderInstance == null || _initialAmountToMine <= 0)
        {
            return;
        }
        float progress = Mathf.Clamp01((float)amountToMine / _initialAmountToMine);
        _progressSliderInstance.SetProgress(progress);
    }
}
