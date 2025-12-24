using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    
    private void ClampTargetPosition()
    {
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

        if (minX > maxX) minX = maxX = _mapBounds.center.x;
        if (minY > maxY) minY = maxY = _mapBounds.center.y;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        
        transform.position = pos;
    }

    private void HandlePlayerInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        _direction = new Vector3(moveX, moveY, 0).normalized;

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

        if (_direction != Vector3.zero || mousePanDirection != Vector3.zero)
        {
            _isManualMode = true;
            Vector3 finalDirection = (_direction != Vector3.zero) ? _direction : mousePanDirection;
            float currentPanSpeed = (_direction != Vector3.zero) ? panSpeed : edgePanSpeed;

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