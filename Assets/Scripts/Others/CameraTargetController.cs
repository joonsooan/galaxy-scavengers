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

    private bool _isManualMode;
    private Vector3 _direction;
    private Camera _mainCamera;
    
    public Transform followTarget;
    public MapGenerator mapGenerator;
    private Bounds _mapBounds;
    private bool _hasBounds;

    private PixelPerfectCamera _pixelPerfCam;
    private int _defaultPpu;
    private float _defaultPanSpeed;
    private float _defaultEdgePanSpeed;
    private bool _isEdgePanEnable;
    
    private int _currentZoomIndex;
    private readonly int[] _zoomDivisors = { 1, 2, 4 };
    private readonly float[] _speedMultipliers = { 1f, 1.5f, 2.5f };
    
    private void Awake()
    {
        _mainCamera = Camera.main;
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
        
        InitializeMapBounds();
        UpdateActiveCamera();
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
            
            if (groundTilemap.layoutGrid != null)
            {
                Grid grid = groundTilemap.layoutGrid;
                Vector3 worldMin = grid.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
                Vector3 worldMax = grid.CellToWorld(new Vector3Int(cellBounds.xMax - 1, cellBounds.yMax - 1, 0));
                
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
    
    private void ClampTargetPosition()
    {
        if (!_hasBounds) return;
        
        int currentDivisor = _zoomDivisors[_currentZoomIndex];
        float currentPPU = (float)_defaultPpu / currentDivisor;
        
        float vertExtent;
        if (_pixelPerfCam != null && _pixelPerfCam.runInEditMode) 
        {
            vertExtent = (_pixelPerfCam.refResolutionY * 0.5f) / currentPPU;
        }
        else
        {
            vertExtent = (Screen.height * 0.5f) / currentPPU;
        }
        
        float horzExtent = vertExtent * _mainCamera.aspect;

        float minX = _mapBounds.min.x + horzExtent;
        float maxX = _mapBounds.max.x - horzExtent;
        float minY = _mapBounds.min.y + vertExtent;
        float maxY = _mapBounds.max.y - vertExtent;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        
        transform.position = pos;
    }

    private void HandlePlayerInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        
        if (moveX != 0f || moveY != 0f)
        {
            _direction = new Vector3(moveX, moveY, 0).normalized;
        }
        else
        {
            _direction = Vector3.zero;
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            transform.position = Vector3.zero;
            _currentZoomIndex = 0;
            UpdateActiveCamera();
            _isManualMode = false;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            _isEdgePanEnable = !_isEdgePanEnable;
        }
    }
    
    private void HandleMovement()
    {
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

        Vector3 finalDirection = Vector3.zero;
        float currentPanSpeed = 0f;
        bool hasInput = false;

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

        if (hasInput)
        {
            transform.Translate(finalDirection * currentPanSpeed * Time.unscaledDeltaTime, Space.World);
        }
        
        if (!_isManualMode && followTarget != null)
        {
            Vector3 targetPosition = new Vector3(followTarget.position.x, followTarget.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.unscaledDeltaTime);
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll == 0 || _pixelPerfCam == null) return;

        int prevIndex = _currentZoomIndex;

        if (scroll < 0)
            _currentZoomIndex++;
        else
            _currentZoomIndex--;

        _currentZoomIndex = Mathf.Clamp(_currentZoomIndex, 0, zoomCameras.Length - 1);

        if (prevIndex != _currentZoomIndex)
        {
            UpdateActiveCamera();
        }
    }
    
    private void UpdateActiveCamera()
    {
        for (int i = 0; i < zoomCameras.Length; i++)
        {
            if (zoomCameras[i] == null) continue;
            zoomCameras[i].Priority = (i == _currentZoomIndex) ? 20 : 10;
        }

        if (_pixelPerfCam != null && _currentZoomIndex < _zoomDivisors.Length)
        {
            int divisor = _zoomDivisors[_currentZoomIndex];
            _pixelPerfCam.assetsPPU = _defaultPpu / divisor;
        }

        if (_currentZoomIndex < _speedMultipliers.Length)
        {
            float multiplier = _speedMultipliers[_currentZoomIndex];
            panSpeed = _defaultPanSpeed * multiplier;
            edgePanSpeed = _defaultEdgePanSpeed * multiplier;
        }
        
        ClampTargetPosition();
    }
}