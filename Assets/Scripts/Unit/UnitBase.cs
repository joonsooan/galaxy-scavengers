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
    protected UnitProgressBar progressBar;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (UnitManager.Instance != null) {
            UnitManager.Instance.AddUnit(this);
        }

        if (unitType == UnitType.Ally && NoiseManager.Instance != null)
        {
            NoiseManager.Instance.RegisterUnit(this);
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

        if (unitType == UnitType.Ally && NoiseManager.Instance != null)
        {
            NoiseManager.Instance.UnregisterUnit(this);
        }

        HideProgressBar();

        base.OnDisable();
    }

    protected virtual void OnDestroy()
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
