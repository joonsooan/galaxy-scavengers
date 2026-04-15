using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public class CameraTargetController : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera[] zoomCameras;

    [Header("Controls")]
    public float panSpeed = 10f;
    public float edgePanSpeed = 20f;
    public float panBorderThickness = 10f;
    public float smoothTime = 0.1f;

    public Transform followTarget;
    public MapGenerator mapGenerator;
    private readonly float[] _speedMultipliers = { 1f, 1.5f, 2.5f };
    private readonly float[] _zoomDivisors = { 0.75f, 1.25f, 2.5f };

    private int _currentZoomIndex;
    private float _defaultEdgePanSpeed;
    private float _defaultPanSpeed;
    private int _defaultPpu;
    private Vector3 _direction;
    private bool _hasBounds;
    private bool _isEdgePanEnable;

    private bool _isManualMode;
    private Camera _mainCamera;
    private Bounds _mapBounds;
    private Grid _grid;
    private MainControlPanel _mainControlPanel;

    private PixelPerfectCamera _pixelPerfCam;
    private bool[] _zoomLevelInitialized;
    private Vector3[] _zoomLevelPositions;
    private Vector3 _currentVelocity;
    private Transform _defaultFollowTarget;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _mainControlPanel = FindFirstObjectByType<MainControlPanel>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        _pixelPerfCam = FindFirstObjectByType<PixelPerfectCamera>();

        if (_pixelPerfCam != null)
        {
            _defaultPpu = _pixelPerfCam.assetsPPU;
        }
        else
        {
            _defaultPpu = 16;
        }

        _defaultPanSpeed = panSpeed;
        _defaultEdgePanSpeed = edgePanSpeed;

        if (zoomCameras == null || zoomCameras.Length == 0)
        {
            Debug.LogWarning("CameraTargetController: zoomCameras array is null or empty. Camera controls will not work.");
            _zoomLevelPositions = new Vector3[0];
            _zoomLevelInitialized = new bool[0];
            return;
        }

        _zoomLevelPositions = new Vector3[zoomCameras.Length];
        _zoomLevelInitialized = new bool[zoomCameras.Length];

        for (int i = 0; i < _zoomLevelPositions.Length; i++)
        {
            _zoomLevelPositions[i] = transform.position;
            _zoomLevelInitialized[i] = false;
        }

        InitializeMapBounds();
        UpdateActiveCamera();
    }

    private void Update()
    {
        HandlePlayerInput();
    }

    private void LateUpdate()
    {
        HandleMovement();
        HandleZoom();

        ClampTargetPosition();
    }

    private void InitializeMapBounds()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        if (mapGenerator != null && mapGenerator.GroundTilemap != null)
        {
            Tilemap groundTilemap = mapGenerator.GroundTilemap;
            groundTilemap.CompressBounds();
            BoundsInt cellBounds = groundTilemap.cellBounds;

            if (_grid == null && groundTilemap.layoutGrid != null)
            {
                _grid = groundTilemap.layoutGrid;
            }

            if (cellBounds.size.x == 0 || cellBounds.size.y == 0)
            {
                _hasBounds = false;
                return;
            }

            if (groundTilemap.layoutGrid != null)
            {
                Grid grid = groundTilemap.layoutGrid;
                Vector3 cellSize = grid.cellSize;

                Vector3 worldMin = grid.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
                worldMin.x -= cellSize.x * 0.5f;
                worldMin.y -= cellSize.y * 0.5f;

                Vector3 worldMax = grid.CellToWorld(new Vector3Int(cellBounds.xMax - 1, cellBounds.yMax - 1, 0));
                worldMax.x += cellSize.x * 0.5f;
                worldMax.y += cellSize.y * 0.5f;

                _mapBounds = new Bounds
                {
                    min = worldMin,
                    max = worldMax
                };
                _mapBounds.center = _mapBounds.min + (_mapBounds.max - _mapBounds.min) * 0.5f;
                _hasBounds = true;
            }
        }
    }

    public void RefreshMapBounds()
    {
        InitializeMapBounds();
    }

    private void ClampTargetPosition()
    {
        if (!_hasBounds) return;

        GetBoundsForZoomLevel(_currentZoomIndex, out float minX, out float maxX, out float minY, out float maxY);

        Vector3 pos = _zoomLevelPositions[_currentZoomIndex];
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        _zoomLevelPositions[_currentZoomIndex] = pos;
        transform.position = pos;
    }

    private void GetBoundsForZoomLevel(int zoomIndex, out float minX, out float maxX, out float minY, out float maxY)
    {
        float currentDivisor = _zoomDivisors[zoomIndex];
        float currentPPU = _defaultPpu / currentDivisor;

        float vertExtent;
        if (_pixelPerfCam != null)
        {
            vertExtent = _pixelPerfCam.refResolutionY * 0.5f / currentPPU;
        }
        else
        {
            vertExtent = Screen.height * 0.5f / currentPPU;
        }

        float horzExtent = vertExtent * _mainCamera.aspect;

        minX = _mapBounds.min.x + horzExtent;
        maxX = _mapBounds.max.x - horzExtent;
        minY = _mapBounds.min.y + vertExtent;
        maxY = _mapBounds.max.y - vertExtent;
    }

    private void HandlePlayerInput()
    {
        if (GameManager.Instance != null && !GameManager.IsGameplayReady)
        {
            _direction = Vector3.zero;
            return;
        }

        if (IsLoadingScreenActive())
        {
            _direction = Vector3.zero;
            return;
        }

        bool hasPlayerUnit = followTarget != null && followTarget.GetComponent<Unit_Player>() != null;

        if (!hasPlayerUnit)
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");

            if (moveX != 0f || moveY != 0f)
            {
                ResetFollowTargetToPlayer();
                hasPlayerUnit = followTarget != null && followTarget.GetComponent<Unit_Player>() != null;
                if (hasPlayerUnit)
                {
                    _direction = Vector3.zero;
                    return;
                }
                _direction = new Vector3(moveX, moveY, 0).normalized;
            }
            else
            {
                _direction = Vector3.zero;
            }
        }
        else
        {
            _direction = Vector3.zero;
        }

    }

    private void HandleMovement()
    {
        if (GameManager.Instance != null && !GameManager.IsGameplayReady)
        {
            return;
        }

        if (IsLoadingScreenActive())
        {
            return;
        }

        Vector3 mousePanDirection = Vector3.zero;

        if (_isEdgePanEnable)
        {
            if (Input.mousePosition.y >= Screen.height - panBorderThickness)
                mousePanDirection += Vector3.up;
            if (Input.mousePosition.y <= panBorderThickness * 0.01f)
                mousePanDirection += Vector3.down;
            if (Input.mousePosition.x >= Screen.width - panBorderThickness)
                mousePanDirection += Vector3.right;
            if (Input.mousePosition.x <= panBorderThickness)
                mousePanDirection += Vector3.left;

            mousePanDirection.Normalize();
        }

        bool hasPlayerUnit = followTarget != null && followTarget.GetComponent<Unit_Player>() != null;

        Vector3 finalDirection = Vector3.zero;
        float currentPanSpeed = 0f;
        bool hasInput = false;

        if (!hasPlayerUnit)
        {
            if (_direction != Vector3.zero)
            {
                finalDirection = _direction;
                currentPanSpeed = panSpeed;
                hasInput = true;
                _isManualMode = true;
            }
            else if (mousePanDirection != Vector3.zero)
            {
                finalDirection = mousePanDirection;
                currentPanSpeed = edgePanSpeed;
                hasInput = true;
                _isManualMode = true;
            }
        }
        else
        {
            if (mousePanDirection != Vector3.zero)
            {
                finalDirection = mousePanDirection;
                currentPanSpeed = edgePanSpeed;
                hasInput = true;
                _isManualMode = true;
            }
        }

        if (hasInput)
        {
            Vector3 movement = finalDirection * currentPanSpeed * Time.unscaledDeltaTime;
            for (int i = 0; i < _zoomLevelPositions.Length; i++)
            {
                Vector3 newPos = _zoomLevelPositions[i] + movement;

                GetBoundsForZoomLevel(i, out float minX, out float maxX, out float minY, out float maxY);
                newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                newPos.y = Mathf.Clamp(newPos.y, minY, maxY);

                _zoomLevelPositions[i] = newPos;
            }
            transform.position = _zoomLevelPositions[_currentZoomIndex];
        }

        if (!_isManualMode && followTarget != null)
        {
            Vector3 targetPosition = followTarget.position;

            if (_grid != null)
            {
                Vector3 cellSize = _grid.cellSize;
                targetPosition.x -= cellSize.x * 0.5f;
                targetPosition.y -= cellSize.y * 0.5f;
            }
            else
            {
                targetPosition.x -= 0.5f;
                targetPosition.y -= 0.5f;
            }

            targetPosition.z = transform.position.z;
            Vector3 newPos;

            if (smoothTime <= 0.01f)
            {
                newPos = targetPosition;
            }
            else
            {
                newPos = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, smoothTime);
            }

            _zoomLevelPositions[_currentZoomIndex] = newPos;
            transform.position = newPos;
        }
    }

    private void HandleZoom()
    {
        if (GameManager.Instance != null && !GameManager.IsGameplayReady)
        {
            return;
        }

        if (IsLoadingScreenActive())
        {
            return;
        }

        if (IsLaunchInputLocked())
        {
            return;
        }

        if (IsPanelZoomBlocked())
        {
            return;
        }

        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll == 0 || _pixelPerfCam == null) return;

        int prevIndex = _currentZoomIndex;
        int newIndex = prevIndex;

        if (scroll < 0)
            newIndex++;
        else
            newIndex--;

        newIndex = Mathf.Clamp(newIndex, 0, zoomCameras.Length - 1);

        if (prevIndex != newIndex)
        {
            _zoomLevelPositions[prevIndex] = transform.position;
            _zoomLevelInitialized[prevIndex] = true;

            bool isZoomingIn = newIndex > prevIndex;

            Vector3 oldPosition = transform.position;
            Vector3 newPosition;

            if (!isZoomingIn)
            {
                newPosition = _zoomLevelPositions[prevIndex];
            }
            else
            {
                GetBoundsForZoomLevel(newIndex, out float minX, out float maxX, out float minY, out float maxY);
                Vector3 pos = transform.position;
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
                pos.y = Mathf.Clamp(pos.y, minY, maxY);
                newPosition = pos;
            }

            transform.position = newPosition;
            _zoomLevelPositions[newIndex] = newPosition;
            _zoomLevelInitialized[newIndex] = true;

            Vector3 positionDelta = newPosition - oldPosition;

            if (positionDelta.sqrMagnitude > 0.0001f || isZoomingIn)
            {
                WarpCameras(positionDelta);
            }

            _currentZoomIndex = newIndex;
            UpdateActiveCamera();
        }
    }

    private bool IsLaunchInputLocked()
    {
        LaunchUIController launchUIController = FindFirstObjectByType<LaunchUIController>(FindObjectsInactive.Include);
        return launchUIController != null && launchUIController.IsLaunchInputLockActive();
    }

    private bool IsPanelZoomBlocked()
    {
        if (_mainControlPanel == null)
        {
            _mainControlPanel = FindFirstObjectByType<MainControlPanel>(FindObjectsInactive.Include);
            if (_mainControlPanel == null)
            {
                return false;
            }
        }

        return _mainControlPanel.IsUnitManagementPanelActive() || _mainControlPanel.IsResourceStatPanelActive();
    }

    private void WarpCameras(Vector3 deltaPos)
    {
        if (zoomCameras == null) return;

        foreach (CinemachineCamera cam in zoomCameras)
        {
            if (cam != null)
            {
                cam.OnTargetObjectWarped(transform, deltaPos);
            }
        }
    }

    private void UpdateActiveCamera()
    {
        if (zoomCameras == null || zoomCameras.Length == 0) return;

        for (int i = 0; i < zoomCameras.Length; i++)
        {
            if (zoomCameras[i] == null) continue;
            zoomCameras[i].Priority = i == _currentZoomIndex ? 20 : 10;
        }

        if (_pixelPerfCam != null && _currentZoomIndex < _zoomDivisors.Length)
        {
            float divisor = _zoomDivisors[_currentZoomIndex];
            _pixelPerfCam.assetsPPU = Mathf.Max(1, Mathf.RoundToInt(_defaultPpu / divisor));
        }

        if (_currentZoomIndex < _speedMultipliers.Length)
        {
            float multiplier = _speedMultipliers[_currentZoomIndex];
            panSpeed = _defaultPanSpeed * multiplier;
            edgePanSpeed = _defaultEdgePanSpeed * multiplier;
        }

        ClampTargetPosition();
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        if (target != null)
        {
            if (_defaultFollowTarget == null && target.GetComponent<Unit_Player>() != null)
                _defaultFollowTarget = target;
            _isManualMode = false;

            if (target.GetComponent<Unit_Player>() != null && !ShouldPreserveTutorialTargetBracket())
                TargetBracketEffect.Hide();
        }
    }

    public void ResetFollowTargetToPlayer()
    {
        if (_defaultFollowTarget == null)
        {
            Unit_Player player = FindFirstObjectByType<Unit_Player>();
            if (player != null)
                _defaultFollowTarget = player.transform;
        }
        if (_defaultFollowTarget == null)
            return;
        followTarget = _defaultFollowTarget;
        _isManualMode = false;
        if (!ShouldPreserveTutorialTargetBracket())
        {
            TargetBracketEffect.Hide();
        }
    }

    private bool ShouldPreserveTutorialTargetBracket()
    {
        return TutorialManager.Instance != null && TutorialManager.Instance.IsTargetBracketLocked();
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }
}
