using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    private Light2D _unitLight2D;
    private Color _unitLightBaseColor;
    private float _unitLightCurrentAlpha = 1f;
    private const float UnitLightAlphaLerpSpeed = 5f;

    private UnitMovement _idleRoamMovement;
    private UnitSpriteController _idleRoamSpriteController;
    private float _idleRoamTimer;
    private float _currentIdleRoamInterval;
    private bool _lastIdleActionWasMove;
    private bool _idleRoamOriginSet;
    private Vector3 _idleRoamOrigin;
    private float _idleRoamOriginalSpeed;
    private bool _idleRoamSpeedReduced;
    private const float IdleRoamMinInterval = 2f;
    private const float IdleRoamMaxInterval = 4f;
    private const float IdleRoamRadius = 3f;

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

    protected void UpdateUnitLightAlpha()
    {
        if (DayNightCycleManager.Instance == null) return;

        if (_unitLight2D == null) {
            _unitLight2D = GetComponentInChildren<Light2D>();
            if (_unitLight2D == null) return;
            Color c = _unitLight2D.color;
            _unitLightBaseColor = new Color(c.r, c.g, c.b, 1f);
            _unitLightCurrentAlpha = c.a;
        }

        float targetAlpha = DayNightCycleManager.Instance.IsDay() ? 0.2f : 1f;
        _unitLightCurrentAlpha = Mathf.Lerp(_unitLightCurrentAlpha, targetAlpha, UnitLightAlphaLerpSpeed * Time.deltaTime);
        _unitLight2D.color = new Color(_unitLightBaseColor.r, _unitLightBaseColor.g, _unitLightBaseColor.b, _unitLightCurrentAlpha);
    }

    protected void UpdateIdleRoam()
    {
        if (BuildingManager.Instance == null || BuildingManager.Instance.grid == null) return;

        if (_idleRoamMovement == null) {
            _idleRoamMovement = GetComponent<UnitMovement>();
            if (_idleRoamMovement == null) return;
            _currentIdleRoamInterval = Random.Range(IdleRoamMinInterval, IdleRoamMaxInterval);
        }

        if (_idleRoamSpriteController == null) {
            _idleRoamSpriteController = GetComponentInChildren<UnitSpriteController>();
        }

        if (!_idleRoamOriginSet) {
            _idleRoamOrigin = transform.position;
            _idleRoamOriginSet = true;
        }

        if (!_idleRoamSpeedReduced) {
            _idleRoamOriginalSpeed = _idleRoamMovement.moveSpeed;
            _idleRoamMovement.moveSpeed = _idleRoamOriginalSpeed * 0.5f;
            _idleRoamSpeedReduced = true;
        }

        if (_idleRoamMovement.IsMoving && _idleRoamSpriteController != null) {
            Vector3 moveDir = _idleRoamMovement.GetMoveDirection();
            _idleRoamSpriteController.UpdateSpriteDirection(moveDir);
        }

        _idleRoamTimer += Time.deltaTime;
        if (_idleRoamTimer < _currentIdleRoamInterval) return;

        _idleRoamTimer = 0f;
        _currentIdleRoamInterval = Random.Range(IdleRoamMinInterval, IdleRoamMaxInterval);

        if (!_lastIdleActionWasMove) {
            Vector3 destination = FindWalkableIdleDestination();
            if (destination != Vector3.zero) {
                _idleRoamMovement.SetNewTargetDirect(destination, _idleRoamMovement.waypointTolerance);
            }
            _lastIdleActionWasMove = true;
        }
        else {
            if (Random.value < 0.5f) {
                _idleRoamMovement.StopMovement();
                _lastIdleActionWasMove = false;
            }
            else {
                Vector3 destination = FindWalkableIdleDestination();
                if (destination != Vector3.zero) {
                    _idleRoamMovement.SetNewTargetDirect(destination, _idleRoamMovement.waypointTolerance);
                }
                _lastIdleActionWasMove = true;
            }
        }
    }

    protected void ResetIdleRoam()
    {
        _idleRoamTimer = 0f;
        _lastIdleActionWasMove = false;
        _idleRoamOriginSet = false;

        if (_idleRoamSpeedReduced && _idleRoamMovement != null) {
            _idleRoamMovement.moveSpeed = _idleRoamOriginalSpeed;
            _idleRoamSpeedReduced = false;
        }
    }

    private Vector3 FindWalkableIdleDestination()
    {
        Grid grid = BuildingManager.Instance.grid;

        for (int attempt = 0; attempt < 50; attempt++) {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            Vector3 candidatePos = _idleRoamOrigin + (Vector3)randomDir * Random.Range(0.5f, IdleRoamRadius);
            Vector3Int candidateCell = grid.WorldToCell(candidatePos);
            if (BuildingManager.Instance.IsCellWalkable(candidateCell)) {
                return grid.GetCellCenterWorld(candidateCell);
            }
        }

        return Vector3.zero;
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
