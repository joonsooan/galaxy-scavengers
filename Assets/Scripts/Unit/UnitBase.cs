using UnityEngine;

public abstract class UnitBase : Damageable
{
    public enum UnitState
    {
        Idle,
        Moving,
        Attacking,
        Mining,
        Constructing,
        ReturningToStorage,
        Unloading
    }

    public enum UnitType
    {
        Ally,
        Enemy
    }

    [Header("Unit Settings")]
    public UnitType unitType;
    public UnitData unitData;

    [Header("Progress Bar")]
    [SerializeField] private GameObject progressBarPrefab;

    public UnitState currentState;
    private bool _isRegisteredToNoiseManager;
    protected UnitProgressBar progressBar;

    private void Update()
    {
        if (unitType == UnitType.Ally && !_isRegisteredToNoiseManager && unitData != null && NoiseManager.Instance != null) {
            NoiseManager.Instance.RegisterUnit(this);
            _isRegisteredToNoiseManager = true;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (UnitManager.Instance != null) {
            UnitManager.Instance.AddUnit(this);
        }

        if (unitType == UnitType.Ally && NoiseManager.Instance != null) {
            NoiseManager.Instance.RegisterUnit(this);
            _isRegisteredToNoiseManager = true;
        }

        VisionProvider visionProvider = GetComponent<VisionProvider>();
        if (visionProvider != null && FogOfWarManager.Instance != null && FogOfWarManager.Instance.IsInitialized) {
            visionProvider.ForceUpdateAffectedTiles();
        }
    }

    protected override void OnDisable()
    {
        if (UnitManager.Instance != null) {
            UnitManager.Instance.RemoveUnit(this);
        }

        if (unitType == UnitType.Ally && NoiseManager.Instance != null) {
            NoiseManager.Instance.UnregisterUnit(this);
        }

        HideProgressBar();

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        HideProgressBar();
    }

    protected void ShowProgressBar()
    {
        if (progressBar != null) return;

        GameObject barObj = Instantiate(progressBarPrefab);
        progressBar = barObj.GetComponent<UnitProgressBar>();
        progressBar.Initialize(transform);
    }

    protected void HideProgressBar()
    {
        if (progressBar != null) {
            progressBar.Destroy();
            progressBar = null;
        }
    }

    protected void UpdateProgressBar(float progress)
    {
        if (progressBar == null) {
            ShowProgressBar();
        }

        if (progressBar != null) {
            progressBar.SetProgress(progress);
        }
    }
}
