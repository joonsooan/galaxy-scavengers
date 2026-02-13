using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class Unit_Player : UnitBase
{
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
    }

    private void Start()
    {
        _grid = BuildingManager.Instance.grid;
    }

    private void OnEnable()
    {
        base.OnEnable();
        GameManager.OnPauseStateChanged += HandlePauseStateChanged;
    }

    private void OnDisable()
    {
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

        HandleInput();
        CheckMiningRange();
        UpdateUnitState();
        UpdateAnimationState();
        UpdateSpriteDirection();
        UpdateLaser();
        UpdateParticlePosition();
    }

    protected override void OnDestroy()
    {
        StopMiningParticles();
        StopMiningSound();

        if (_laserRenderer != null) {
            Destroy(_laserRenderer.gameObject);
        }

        if (GameManager.Instance != null)
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
                Vector2 direction = (_targetResourceNode.transform.position - transform.position).normalized;
                _spriteController.SetTargetTransform(_targetResourceNode.transform);

                if (direction.sqrMagnitude > 0.01f) {
                    _spriteController.UpdateSpriteDirection(direction);
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

    private void HandleInput()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging()) {
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
                    hit.collider.GetComponent<Processor>() != null ||
                    hit.collider.GetComponent<DroneHub>() != null) {
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
        if (_mineCoroutine != null) {
            StopCoroutine(_mineCoroutine);
            _mineCoroutine = null;
        }
        currentState = UnitState.Idle;
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
                if (_targetResourceNode != null && _targetResourceNode.IsDepleted) {
                    Vector3Int depletedCell = _targetResourceNode.cellPosition;
                    yield return null;
                    bool foundAdjacent = TryMineAdjacentResource(depletedCell);
                    if (!foundAdjacent) {
                        StopMining();
                    }
                }
                else {
                    StopMining();
                }
                yield break;
            }

            float distance = Vector3.Distance(transform.position, _targetResourceNode.transform.position);
            if (distance > interactionRange) {
                StopMining();
                yield break;
            }

            int minedAmount = _targetResourceNode.Mine(mineAmountPerAction);
            if (minedAmount > 0) {
                AddResourceToStorage(_targetResourceNode.resourceType, minedAmount);

                if (TutorialManager.Instance != null) {
                    TutorialManager.Instance.OnResourceMined(_targetResourceNode.resourceType, minedAmount);
                }
            }

            yield return _miningDelay;
        }
    }

    private bool TryMineAdjacentResource(Vector3Int depletedCell)
    {
        if (_grid == null) return false;

        Vector3Int[] neighborOffsets = {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };

        List<ResourceNode> allResources = ResourceManager.Instance.GetAllResources();
        ResourceNode closestAdjacentResource = null;
        float minDistance = float.MaxValue;

        foreach (Vector3Int offset in neighborOffsets) {
            Vector3Int neighborCell = depletedCell + offset;

            foreach (ResourceNode resource in allResources) {
                if (resource == null || resource.IsDepleted || !resource.gameObject.activeInHierarchy)
                    continue;

                if (resource.cellPosition == neighborCell) {
                    float distance = Vector3.Distance(transform.position, resource.transform.position);
                    if (distance <= interactionRange && distance < minDistance) {
                        minDistance = distance;
                        closestAdjacentResource = resource;
                    }
                }
            }
        }

        if (closestAdjacentResource != null) {
            _targetResourceNode = closestAdjacentResource;
            _mineCoroutine = null;
            StartMining();
            return true;
        }

        return false;
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
                remaining -= actuallyAdded;
            }
        }
    }

    private bool CanEnableBulletFiring()
    {
        if (TutorialManager.Instance == null) {
            return true;
        }

        if (!TutorialManager.Instance.ShouldStartTutorial()) {
            return true;
        }

        if (!TutorialManager.Instance.IsTutorialActive()) {
            return false;
        }

        if (TutorialManager.Instance.HasReachedStepType(TutorialStepType.BulletFired)) {
            return true;
        }

        return false;
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

        if (TutorialManager.Instance != null) {
            TutorialManager.Instance.OnBulletFired();
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

        if (currentState == UnitState.Mining && _targetResourceNode != null) {
            Transform particleTransform = miningParticleSystem.transform;
            Vector3 offsetPosition = _targetResourceNode.transform.position - new Vector3(0, yOffset, 0);
            particleTransform.position = offsetPosition;
        }
    }
}
