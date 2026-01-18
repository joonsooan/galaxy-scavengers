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
    public float smoothing = 5f;

    public Transform followTarget;
    public MapGenerator mapGenerator;
    private readonly float[] _speedMultipliers = { 1f, 1.5f, 2.5f };
    private readonly int[] _zoomDivisors = { 1, 2, 4 };

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

    private PixelPerfectCamera _pixelPerfCam;
    private bool[] _zoomLevelInitialized;
    private Vector3[] _zoomLevelPositions;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Start()
    {
        _pixelPerfCam = FindFirstObjectByType<PixelPerfectCamera>();

        if (_pixelPerfCam != null) {
            _defaultPpu = _pixelPerfCam.assetsPPU;
        }
        else {
            _defaultPpu = 16;
        }

        _defaultPanSpeed = panSpeed;
        _defaultEdgePanSpeed = edgePanSpeed;

        if (zoomCameras == null || zoomCameras.Length == 0) {
            Debug.LogWarning("CameraTargetController: zoomCameras array is null or empty. Camera controls will not work.");
            _zoomLevelPositions = new Vector3[0];
            _zoomLevelInitialized = new bool[0];
            return;
        }

        _zoomLevelPositions = new Vector3[zoomCameras.Length];
        _zoomLevelInitialized = new bool[zoomCameras.Length];
        for (int i = 0; i < _zoomLevelPositions.Length; i++) {
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
        if (mapGenerator == null) {
            mapGenerator = FindFirstObjectByType<MapGenerator>();
        }

        if (mapGenerator != null && mapGenerator.GroundTilemap != null) {
            Tilemap groundTilemap = mapGenerator.GroundTilemap;
            groundTilemap.CompressBounds();
            BoundsInt cellBounds = groundTilemap.cellBounds;

            if (groundTilemap.layoutGrid != null) {
                Grid grid = groundTilemap.layoutGrid;
                Vector3 worldMin = grid.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
                Vector3 worldMax = grid.CellToWorld(new Vector3Int(cellBounds.xMax - 1, cellBounds.yMax - 1, 0));

                _mapBounds = new Bounds {
                    min = worldMin,
                    max = worldMax
                };
                _mapBounds.center = _mapBounds.min + (_mapBounds.max - _mapBounds.min) * 0.5f;
                _hasBounds = true;
            }
        }
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
        int currentDivisor = _zoomDivisors[zoomIndex];
        float currentPPU = (float)_defaultPpu / currentDivisor;

        float vertExtent;
        if (_pixelPerfCam != null) {
            vertExtent = _pixelPerfCam.refResolutionY * 0.5f / currentPPU;
        }
        else {
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
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        if (moveX != 0f || moveY != 0f) {
            _direction = new Vector3(moveX, moveY, 0).normalized;
        }
        else {
            _direction = Vector3.zero;
        }

        if (Input.GetKeyDown(KeyCode.H)) {
            for (int i = 0; i < _zoomLevelPositions.Length; i++) {
                _zoomLevelPositions[i] = Vector3.zero;
            }
            transform.position = Vector3.zero;
            _currentZoomIndex = 0;
            UpdateActiveCamera();
            _isManualMode = false;
        }

        if (Input.GetKeyDown(KeyCode.E)) {
            _isEdgePanEnable = !_isEdgePanEnable;
        }
    }

    private void HandleMovement()
    {
        Vector3 mousePanDirection = Vector3.zero;

        if (_isEdgePanEnable) {
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

        Vector3 finalDirection = Vector3.zero;
        float currentPanSpeed = 0f;
        bool hasInput = false;

        if (_direction != Vector3.zero) {
            finalDirection = _direction;
            currentPanSpeed = panSpeed;
            hasInput = true;
            _isManualMode = true;
        }
        else if (mousePanDirection != Vector3.zero) {
            finalDirection = mousePanDirection;
            currentPanSpeed = edgePanSpeed;
            hasInput = true;
            _isManualMode = true;
        }

        if (hasInput) {
            Vector3 movement = finalDirection * currentPanSpeed * Time.unscaledDeltaTime;
            for (int i = 0; i < _zoomLevelPositions.Length; i++) {
                Vector3 newPos = _zoomLevelPositions[i] + movement;

                GetBoundsForZoomLevel(i, out float minX, out float maxX, out float minY, out float maxY);
                newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                newPos.y = Mathf.Clamp(newPos.y, minY, maxY);

                _zoomLevelPositions[i] = newPos;
            }
            transform.position = _zoomLevelPositions[_currentZoomIndex];
        }

        if (!_isManualMode && followTarget != null) {
            Vector3 targetPosition = new Vector3(followTarget.position.x, followTarget.position.y, transform.position.z);
            Vector3 newPos = Vector3.Lerp(_zoomLevelPositions[_currentZoomIndex], targetPosition, smoothing * Time.unscaledDeltaTime);
            _zoomLevelPositions[_currentZoomIndex] = newPos;
            transform.position = newPos;
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll == 0 || _pixelPerfCam == null) return;

        int prevIndex = _currentZoomIndex;
        int newIndex = prevIndex;

        if (scroll < 0)
            newIndex++;
        else
            newIndex--;

        newIndex = Mathf.Clamp(newIndex, 0, zoomCameras.Length - 1);

        if (prevIndex != newIndex) {
            _zoomLevelPositions[prevIndex] = transform.position;
            _zoomLevelInitialized[prevIndex] = true;

            bool isZoomingIn = newIndex > prevIndex;

            Vector3 oldPosition = transform.position;
            Vector3 newPosition;

            if (!isZoomingIn) {
                newPosition = _zoomLevelPositions[prevIndex];
            }
            else {
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

            if (positionDelta.sqrMagnitude > 0.0001f || isZoomingIn) {
                WarpCameras(positionDelta);
            }

            _currentZoomIndex = newIndex;
            UpdateActiveCamera();
        }
    }

    private void WarpCameras(Vector3 deltaPos)
    {
        if (zoomCameras == null) return;
        
        foreach (CinemachineCamera cam in zoomCameras) {
            if (cam != null) {
                cam.OnTargetObjectWarped(transform, deltaPos);
            }
        }
    }

    private void UpdateActiveCamera()
    {
        if (zoomCameras == null || zoomCameras.Length == 0) return;
        
        for (int i = 0; i < zoomCameras.Length; i++) {
            if (zoomCameras[i] == null) continue;
            zoomCameras[i].Priority = i == _currentZoomIndex ? 20 : 10;
        }

        if (_pixelPerfCam != null && _currentZoomIndex < _zoomDivisors.Length) {
            int divisor = _zoomDivisors[_currentZoomIndex];
            _pixelPerfCam.assetsPPU = _defaultPpu / divisor;
        }

        if (_currentZoomIndex < _speedMultipliers.Length) {
            float multiplier = _speedMultipliers[_currentZoomIndex];
            panSpeed = _defaultPanSpeed * multiplier;
            edgePanSpeed = _defaultEdgePanSpeed * multiplier;
        }

        ClampTargetPosition();
    }
}
