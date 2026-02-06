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

    [Header("Health Bar")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float healthBarYOffset = 1.5f;

    public UnitState currentState;
    private bool _isRegisteredToNoiseManager;
    protected UnitProgressBar progressBar;
    protected UnitHealthBar healthBar;
    private int _previousHealth;

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

        _previousHealth = currentHealth;
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
        HideHealthBar();

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        HideProgressBar();
        HideHealthBar();
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

    protected override void OnHealthChanged()
    {
        base.OnHealthChanged();
        
        if (_previousHealth != currentHealth)
        {
            if (currentHealth < maxHealth)
            {
                ShowHealthBar();
                if (healthBar != null)
                {
                    float healthRatio = (float)currentHealth / maxHealth;
                    healthBar.SetHealth(healthRatio);
                }
            }
            else if (currentHealth >= maxHealth)
            {
                HideHealthBar();
            }
            
            _previousHealth = currentHealth;
        }
    }

    protected void ShowHealthBar()
    {
        if (healthBar != null) return;
        if (healthBarPrefab == null) return;

        GameObject barObj = Instantiate(healthBarPrefab);
        healthBar = barObj.GetComponent<UnitHealthBar>();
        if (healthBar != null)
        {
            healthBar.Initialize(transform);
            healthBar.SetYOffset(healthBarYOffset);
            float healthRatio = (float)currentHealth / maxHealth;
            healthBar.SetHealth(healthRatio);
        }
    }

    protected void HideHealthBar()
    {
        if (healthBar != null) {
            healthBar.Destroy();
            healthBar = null;
        }
    }
}
