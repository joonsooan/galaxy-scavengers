using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Controls")]
    public float smoothing = 5f;
    public float panSpeed = 10f;
    public float edgePanSpeed = 20f;
    public float panBorderThickness = 10f;

    private Camera _cam;
    private bool _isManualMode;
    private Vector3 _direction;
    private Bounds? _worldBounds;
    public Transform target;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) {
            _cam = Camera.main;
        }
    }

    private void Update()
    {
        HandlePlayerInput();
    }

    private void HandlePlayerInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        _direction = new Vector3(moveX, moveY, 0).normalized;
    }

    private void LateUpdate()
    {
        HandleCameraMovement();
    }

    private void HandleCameraMovement()
    {
        Vector3 mousePanDirection = Vector3.zero;

        // if (Input.mousePosition.y >= Screen.height - panBorderThickness)
        // {
        //     mousePanDirection += Vector3.up;
        // }
        // if (Input.mousePosition.y <= panBorderThickness * 0.01f)
        // {
        //     mousePanDirection += Vector3.down;
        // }
        // if (Input.mousePosition.x >= Screen.width - panBorderThickness)
        // {
        //     mousePanDirection += Vector3.right;
        // }
        // if (Input.mousePosition.x <= panBorderThickness)
        // {
        //     mousePanDirection += Vector3.left;
        // }
        // mousePanDirection.Normalize();

        if (_direction != Vector3.zero || mousePanDirection != Vector3.zero)
        {
            _isManualMode = true;
            Vector3 finalDirection = (_direction != Vector3.zero) ? _direction : mousePanDirection;
            float currentPanSpeed = (_direction != Vector3.zero) ? panSpeed : edgePanSpeed;

            transform.Translate(finalDirection * currentPanSpeed * Time.unscaledDeltaTime, Space.World);
        }
        else
        {
            _isManualMode = false;
        }

        if (!_isManualMode && target != null)
        {
            Vector3 targetPosition = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing * Time.unscaledDeltaTime);
        }
        
        ClampToBounds();
    }
    
    public void SetBounds(Bounds bounds)
    {
        _worldBounds = bounds;
        ClampToBounds();
    }

    private void ClampToBounds()
    {
        if (_cam == null || !_cam.orthographic || _worldBounds == null) {
            return;
        }

        Bounds bounds = _worldBounds.Value;

        float halfHeight = _cam.orthographicSize;
        float halfWidth = halfHeight * _cam.aspect;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }
}