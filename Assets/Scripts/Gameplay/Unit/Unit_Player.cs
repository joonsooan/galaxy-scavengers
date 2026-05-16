using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class Unit_Player : UnitBase
{
    private static readonly HashSet<ResourceType> BaseResourceTypes = new HashSet<ResourceType>
    {
        ResourceType.Ferrite,
        ResourceType.Aether,
        ResourceType.Biomass,
        ResourceType.CryoCrystal
    };

    [Header("Player Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private int mineAmountPerAction = 1;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private GameObject bulletPrefab;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CameraTargetController cameraController;

    [Header("Audio")]
    [SerializeField] private EventReference miningSound;
    [SerializeField] private EventReference fireSound;

    [Header("Visual Effects")]
    [SerializeField] private Material laserMaterial;
    [SerializeField] private float laserWidth = 0.1f;
    [SerializeField] private ParticleSystem miningParticleSystem;
    [SerializeField] private float yOffset;
    private float _attackStateEndTime;
    private Grid _grid;
    private bool _isAttacking;
    private bool _isBulletFiringEnabled;
    private bool _isLookingAtFireDirection;
    private LineRenderer _laserRenderer;
    private Vector2 _lastFireDirection;
    private float _lastFireTime;
    private float _lookAtFireDirectionEndTime;
    private Camera _mainCamera;
    private EventInstance _miningSoundInstance;
    private Coroutine _mineCoroutine;
    private WaitForSeconds _miningDelay;
    private UnitSpriteController _spriteController;
    private Vector2 _lockedMiningDirection;
    private bool _wasInputBlockedLastFrame = true;

    private ResourceNode _targetResourceNode;

    protected override void Awake()
    {
        base.Awake();
        unitType = UnitType.Ally;
        _mainCamera = Camera.main;
        playerMovement = GetComponent<PlayerMovement>();
        _spriteController = GetComponentInChildren<UnitSpriteController>();

        CreateLaserRenderer();

        if (cameraController == null) {
            cameraController = FindFirstObjectByType<CameraTargetController>();
        }

        if (cameraController != null) {
            cameraController.SetFollowTarget(transform);
        }

        if (miningParticleSystem != null) {
            var main = miningParticleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private void Start()
    {
        _grid = BuildingManager.Instance.grid;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        GameManager.OnPauseStateChanged += HandlePauseStateChanged;
    }

    protected override void OnDisable()
    {
        _targetResourceNode?.EndMining(this);
        GameManager.OnPauseStateChanged -= HandlePauseStateChanged;
        base.OnDisable();
    }

    private void Update()
    {
        if (!_isBulletFiringEnabled) {
            if (CanEnableBulletFiring()) {
                _isBulletFiringEnabled = true;
            }
        }

        if (ShouldBlockPlayerInput()) {
            _wasInputBlockedLastFrame = true;
        }
        else if (_wasInputBlockedLastFrame) {
            _wasInputBlockedLastFrame = false;
        }
        else {
            HandleInput();
        }
        CheckMiningRange();
        UpdateUnitState();
        UpdateAnimationState();
        UpdateSpriteDirection();
        UpdateLaser();
        UpdateParticlePosition();
        UpdateUnitLightAlpha();
    }

    protected override void OnDestroy()
    {
        _targetResourceNode?.EndMining(this);
        StopMiningParticles();
        StopMiningSound();

        if (_laserRenderer != null) {
            Destroy(_laserRenderer.gameObject);
        }

        bool isGameplayScene = gameObject.scene.IsValid() && gameObject.scene.name == "GameScene";
        bool isActiveGameplayScene = SceneManager.GetActiveScene().name == "GameScene";
        bool shouldTriggerGameOver = isGameplayScene &&
            isActiveGameplayScene &&
            GameManager.Instance != null &&
            GameManager.Instance.IsGameSceneInitialized &&
            GameManager.IsGameplayReady;

        if (shouldTriggerGameOver)
        {
            GameManager.Instance.GameOver();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    private void UpdateUnitState()
    {
        if (currentState == UnitState.Mining) {
            return;
        }

        if (_isAttacking && Time.time >= _attackStateEndTime) {
            _isAttacking = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        bool isMoving = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f;

        if (isMoving && currentState != UnitState.Mining && !_isAttacking) {
            currentState = UnitState.Moving;
        }
        else if (!isMoving && currentState != UnitState.Mining && !_isAttacking) {
            currentState = UnitState.Idle;
        }
    }

    private void UpdateAnimationState()
    {
        if (_spriteController == null) {
            return;
        }

        bool isMining = currentState == UnitState.Mining;
        bool isMoving = currentState == UnitState.Moving;
        bool isAttacking = _isAttacking;

        _spriteController.UpdateAnimationState(
            currentState,
            isMining,
            isAttacking: isAttacking
        );
    }

    private void UpdateSpriteDirection()
    {
        if (currentState == UnitState.Mining && _targetResourceNode != null) {
            if (_spriteController != null) {
                if (IsMovingWhileMining()) {
                    Vector2 liveDirection = (_targetResourceNode.transform.position - transform.position).normalized;
                    if (liveDirection.sqrMagnitude > 0.01f) {
                        _lockedMiningDirection = liveDirection;
                        _spriteController.UpdateSpriteDirection(liveDirection);
                    }
                }
                else if (_lockedMiningDirection.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(_lockedMiningDirection);
                }
            }
        }
        else if (_isLookingAtFireDirection && Time.time < _lookAtFireDirectionEndTime) {
            if (_spriteController != null && _lastFireDirection.sqrMagnitude > 0.01f) {
                _spriteController.UpdateSpriteDirection(_lastFireDirection);
            }
        }
        else if (_spriteController != null && playerMovement != null) {
            _isLookingAtFireDirection = false;
            Vector2 moveDir = playerMovement.GetMoveDirection();
            if (moveDir.sqrMagnitude > 0.01f) {
                _spriteController.ClearTarget();
                _spriteController.UpdateSpriteDirection(moveDir);
            }
        }
        else if (_isAttacking && _spriteController != null) {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            if (mouseWorldPos != Vector3.zero) {
                Vector2 direction = (mouseWorldPos - transform.position).normalized;
                if (direction.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(direction);
                }
            }
        }
    }

    private bool IsMovingWhileMining()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        return rb != null && rb.linearVelocity.sqrMagnitude > 0.01f;
    }

    private void HandleInput()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.WasDragEndedRecently(0.12f)) {
            return;
        }

        if (Input.GetMouseButtonDown(0)) {
            if (IsPointerOverUI()) {
                return;
            }

            Vector3 mouseWorldPos = GetMouseWorldPosition();
            if (mouseWorldPos != Vector3.zero) {
                float distanceToClick = Vector3.Distance(transform.position, mouseWorldPos);

                if (distanceToClick <= interactionRange) {
                    ResourceNode clickedResource = GetResourceAtPosition(mouseWorldPos);
                    if (clickedResource != null) {
                        TryMineResource(mouseWorldPos);
                    }
                }
            }
        }
        else if (Input.GetMouseButton(0)) {
            if (IsPointerOverUI() || IsPointerOverBuilding()) {
                return;
            }

            Vector3 mouseWorldPos = GetMouseWorldPosition();
            if (mouseWorldPos != Vector3.zero) {
                float distanceToClick = Vector3.Distance(transform.position, mouseWorldPos);
                ResourceNode clickedResource = GetResourceAtPosition(mouseWorldPos);
                if (distanceToClick <= interactionRange && clickedResource != null) {
                    return;
                }

                if (currentState == UnitState.Mining) {
                    StopMining();
                }
                TryFireBullet(mouseWorldPos);
            }
        }
    }

    private bool ShouldBlockPlayerInput()
    {
        if (GameManager.Instance != null) {
            if (!GameManager.IsGameplayReady || !GameManager.Instance.IsGameSceneInitialized || GameManager.Instance.IsPaused) {
                return true;
            }
        }

        if (IsLoadingScreenActive()) {
            return true;
        }

        if (cameraController == null) {
            cameraController = FindFirstObjectByType<CameraTargetController>(FindObjectsInactive.Include);
        }

        return cameraController == null || cameraController.followTarget != transform;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null) {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null) {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current) {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results) {
            if (result.gameObject != null &&
                result.gameObject.layer == LayerMask.NameToLayer("UI")) {
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOverBuilding()
    {
        if (_mainCamera == null) {
            return false;
        }

        Vector3 worldPoint = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(worldPoint.x, worldPoint.y);

        RaycastHit2D[] hits = Physics2D.RaycastAll(point, Vector2.zero);
        if (hits == null || hits.Length == 0) {
            return false;
        }

        foreach (RaycastHit2D hit in hits) {
            if (hit.collider == null) {
                continue;
            }

            if (hit.collider is BoxCollider2D) {
                if (hit.collider.GetComponent<MainStructure>() != null ||
                    hit.collider.GetComponent<Processor>() != null) {
                    return true;
                }
            }
        }

        foreach (RaycastHit2D hit in hits) {
            if (hit.collider == null || hit.collider.isTrigger) {
                continue;
            }

            if (hit.collider.GetComponent<ConstructionSite>() != null) {
                continue;
            }

            IClickable clickable = hit.collider.GetComponent<IClickable>();
            if (clickable != null) {
                return true;
            }
        }

        return false;
    }

    private ResourceNode GetResourceAtPosition(Vector3 position)
    {
        if (_grid == null) return null;

        Vector3Int clickCell = _grid.WorldToCell(position);
        List<ResourceNode> allResources = ResourceManager.Instance.GetAllResources();

        foreach (ResourceNode resource in allResources) {
            if (resource == null || resource.IsDepleted || !resource.gameObject.activeInHierarchy)
                continue;

            if (resource.cellPosition == clickCell) {
                return resource;
            }
        }

        return null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (_mainCamera == null) return Vector3.zero;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = _mainCamera.transform.position.z * -1f;
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0f;

        return mouseWorldPos;
    }

    private void UpdateLockedMiningDirection()
    {
        if (_targetResourceNode == null) {
            _lockedMiningDirection = Vector2.zero;
            return;
        }

        Vector2 direction = (_targetResourceNode.transform.position - transform.position).normalized;
        if (direction.sqrMagnitude > 0.01f) {
            _lockedMiningDirection = direction;
        }
    }

    private void TryMineResource(Vector3 clickPosition)
    {
        ResourceNode clickedResource = GetResourceAtPosition(clickPosition);

        if (clickedResource != null) {
            float distance = Vector3.Distance(transform.position, clickedResource.transform.position);
            if (distance <= interactionRange) {
                if (_targetResourceNode != null && _targetResourceNode != clickedResource) {
                    StopMining();
                }

                _targetResourceNode = clickedResource;
                StartMining();
            }
        }
    }

    private void StartMining()
    {
        if (_targetResourceNode == null || _targetResourceNode.IsDepleted) {
            StopMining();
            return;
        }
        _targetResourceNode.BeginMining(this);
        UpdateLockedMiningDirection();
        if (_spriteController != null) {
            _spriteController.ClearTarget();
        }

        if (_mineCoroutine == null) {
            _miningDelay = CoroutineCache.GetWaitForSeconds(_targetResourceNode.timeToMinePerUnit);
            _mineCoroutine = StartCoroutine(MineResourceCoroutine());
            currentState = UnitState.Mining;
        }

        StartMiningParticles();
        StartMiningSound();
    }

    private void StopMining()
    {
        if (_targetResourceNode != null) {
            _targetResourceNode.EndMining(this);
        }
        if (_mineCoroutine != null) {
            StopCoroutine(_mineCoroutine);
            _mineCoroutine = null;
        }
        currentState = UnitState.Idle;
        _lockedMiningDirection = Vector2.zero;
        StopMiningParticles();
        StopMiningSound();

        if (_spriteController != null) {
            _spriteController.ClearTarget();
        }
    }

    private IEnumerator MineResourceCoroutine()
    {
        yield return _miningDelay;

        while (true) {
            if (_targetResourceNode == null || _targetResourceNode.IsDepleted) {
                StopMining();
                yield break;
            }

            float distance = Vector3.Distance(transform.position, _targetResourceNode.transform.position);
            if (distance > interactionRange) {
                StopMining();
                yield break;
            }

            int minedAmount = _targetResourceNode.Mine(mineAmountPerAction);
            if (minedAmount > 0) {
                ResourceType minedResourceType = _targetResourceNode.resourceType;
                AddResourceToStorage(minedResourceType, minedAmount);
                TutorialManager.Instance?.OnResourceMined(minedResourceType, minedAmount);

            }

            if (_targetResourceNode == null || _targetResourceNode.IsDepleted) {
                continue;
            }

            yield return _miningDelay;
        }
    }

    private void CheckMiningRange()
    {
        if (currentState == UnitState.Mining && _targetResourceNode != null) {
            float distance = Vector3.Distance(transform.position, _targetResourceNode.transform.position);
            if (distance > interactionRange) {
                StopMining();
            }
        }
    }

    private void AddResourceToStorage(ResourceType type, int amount)
    {
        if (ResourceManager.Instance == null || amount <= 0) {
            return;
        }

        List<IStorage> allStorages = ResourceManager.Instance.GetAllStorages()
            .Where(s => s != null && s.GetTotalCurrentAmount() < s.GetMaxCapacity())
            .OrderBy(s => Vector3.Distance(transform.position, s.GetPosition()))
            .ToList();

        int remaining = amount;

        foreach (IStorage storage in allStorages) {
            if (remaining <= 0) {
                break;
            }

            int beforeAmount = storage.GetCurrentResourceAmount(type);
            bool added = storage.TryAddResource(type, remaining);
            int afterAmount = storage.GetCurrentResourceAmount(type);
            int actuallyAdded = afterAmount - beforeAmount;

            if (added && actuallyAdded > 0) {
                if (BaseResourceTypes.Contains(type))
                {
                    UnitProcessResourceStatTracker.RecordProduce(type, actuallyAdded);
                }
                remaining -= actuallyAdded;
            }
        }
    }

    private bool CanEnableBulletFiring()
    {
        bool isTutorialScene = SceneManager.GetActiveScene().name == "TutorialScene";
        if (isTutorialScene) {
            TutorialManager tutorialManager = TutorialManager.Instance;
            if (tutorialManager == null || !tutorialManager.HasReachedStepType(TutorialStepType.BulletFired)) {
                return false;
            }
        }

        return true;
    }

    private void TryFireBullet(Vector3 targetPosition)
    {
        if (Time.time - _lastFireTime < fireInterval)
            return;

        if (bulletPrefab == null)
            return;

        if (!_isBulletFiringEnabled) {
            if (!CanEnableBulletFiring()) {
                return;
            }

            _isBulletFiringEnabled = true;
        }

        Vector2 fireDirection = (targetPosition - transform.position).normalized;
        Vector3 spawnPosition = transform.position + (Vector3)fireDirection * 0.5f;

        Quaternion bulletRotation = Quaternion.identity;
        if (fireDirection.sqrMagnitude > 0.01f) {
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            bulletRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        GameObject bulletObj = ObjectPooler.Instance.SpawnFromPool("PlayerBullet", spawnPosition, bulletRotation);

        if (bulletObj != null) {
            Player_Bullet bulletScript = bulletObj.GetComponent<Player_Bullet>();
            if (bulletScript == null) {
                bulletScript = bulletObj.AddComponent<Player_Bullet>();
            }

            bulletScript.speed = 10f;
            bulletScript.lifeTime = 3f;
            bulletScript.Initialize(attackDamage, fireDirection);

            TutorialManager.Instance?.OnBulletFired();
        }

        _lastFireTime = Time.time;
        _isAttacking = true;
        _attackStateEndTime = Time.time + fireInterval * 0.5f;
        currentState = UnitState.Attacking;

        if (!fireSound.IsNull)
            RuntimeManager.PlayOneShot(fireSound);

        _lastFireDirection = fireDirection;
        _isLookingAtFireDirection = true;
        _lookAtFireDirectionEndTime = Time.time + 0.1f;

        if (_spriteController != null) {
            _spriteController.UpdateSpriteDirection(fireDirection);
        }

    }

    private void CreateLaserRenderer()
    {
        GameObject laserObj = new GameObject("MiningLaser");
        laserObj.transform.SetParent(transform);
        laserObj.transform.localPosition = Vector3.zero;

        _laserRenderer = laserObj.AddComponent<LineRenderer>();
        _laserRenderer.material = laserMaterial != null ? laserMaterial : CreateDefaultLaserMaterial();
        _laserRenderer.startWidth = laserWidth;
        _laserRenderer.endWidth = laserWidth;
        _laserRenderer.positionCount = 2;
        _laserRenderer.useWorldSpace = true;
        _laserRenderer.sortingLayerName = "Particles";
        _laserRenderer.sortingOrder = 10;
        _laserRenderer.enabled = false;
    }

    private Material CreateDefaultLaserMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        return mat;
    }

    private void UpdateLaser()
    {
        if (currentState == UnitState.Mining && _targetResourceNode != null && _laserRenderer != null) {
            _laserRenderer.enabled = true;
            Vector3 direction = (_targetResourceNode.transform.position - transform.position).normalized;
            Vector3 startPos = transform.position + direction * 0.3f;
            Vector3 endPos = _targetResourceNode.transform.position;
            _laserRenderer.SetPosition(0, startPos);
            _laserRenderer.SetPosition(1, endPos);
        }
        else if (_laserRenderer != null) {
            _laserRenderer.enabled = false;
        }
    }

    private void StartMiningParticles()
    {
        if (miningParticleSystem != null) {
            StopMiningParticles();
            UpdateParticlePosition();
            miningParticleSystem.Play();
        }
    }

    private void StopMiningParticles()
    {
        if (miningParticleSystem != null) {
            if (miningParticleSystem.isPlaying) {
                miningParticleSystem.Stop();
            }
            miningParticleSystem.Clear();
        }
    }

    private void StartMiningSound()
    {
        StopMiningSound();
        if (!miningSound.IsNull)
        {
            _miningSoundInstance = RuntimeManager.CreateInstance(miningSound);
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                RuntimeManager.AttachInstanceToGameObject(_miningSoundInstance, gameObject, rb);
            }
            else
            {
                RuntimeManager.AttachInstanceToGameObject(_miningSoundInstance, gameObject);
            }
            _miningSoundInstance.start();
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                _miningSoundInstance.setPaused(true);
            }
        }
    }

    private void StopMiningSound()
    {
        if (_miningSoundInstance.isValid())
        {
            RuntimeManager.DetachInstanceFromGameObject(_miningSoundInstance);
            _miningSoundInstance.stop(STOP_MODE.ALLOWFADEOUT);
            _miningSoundInstance.release();
            _miningSoundInstance = default;
        }
    }

    private void HandlePauseStateChanged(bool isPaused)
    {
        if (_miningSoundInstance.isValid())
        {
            _miningSoundInstance.setPaused(isPaused);
        }
    }

    private void UpdateParticlePosition()
    {
        if (miningParticleSystem == null) return;

        if (currentState == UnitState.Mining && _targetResourceNode != null && !_targetResourceNode.IsDepleted && miningParticleSystem.isPlaying) {
            Transform particleTransform = miningParticleSystem.transform;
            Vector3 offsetPosition = _targetResourceNode.transform.position - new Vector3(0, yOffset, 0);
            particleTransform.position = offsetPosition;
        }
    }
}
