using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BuildingHoverManager : MonoBehaviour
{
    public static BuildingHoverManager Instance { get; private set; }

    private Camera _mainCamera;
    private BuildingDataHolder _currentHoveredBuilding;
    private IStorage _currentHoveredStorage;
    private float _hoverTimer;
    private float _storageHoverTimer;
    private bool _isUIShown;
    private bool _isStorageUIShown;
    private const float HoverDelay = 0.3f;

    private GameObject _mouseHoverDetector;
    private BoxCollider2D _mouseDetectorCollider;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        CreateMouseHoverDetector();
    }

    private void CreateMouseHoverDetector()
    {
        _mouseHoverDetector = new GameObject("MouseHoverDetector");
        _mouseHoverDetector.tag = "MouseHoverDetector";
        _mouseHoverDetector.layer = LayerMask.NameToLayer("Ignore Raycast");
        
        _mouseDetectorCollider = _mouseHoverDetector.AddComponent<BoxCollider2D>();
        _mouseDetectorCollider.isTrigger = true;
        _mouseDetectorCollider.size = Vector2.one * 0.2f;
        
        Rigidbody2D rb = _mouseHoverDetector.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    private void Update()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        if (_mouseHoverDetector == null)
        {
            CreateMouseHoverDetector();
        }

        if (_mouseHoverDetector != null)
        {
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;
            _mouseHoverDetector.transform.position = mouseWorldPos;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsDragging())
        {
            ClearAllHovers();
            return;
        }

        if (UIUtils.IsPointerOverUI() && !IsPointerOverFloatingNumText())
        {
            ClearAllHovers();
            return;
        }

        UpdateHoverTimers();
    }

    private void UpdateHoverTimers()
    {
        if (_currentHoveredBuilding != null)
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (_hoverTimer >= HoverDelay && !_isUIShown)
            {
                ShowBuildingInfo(_currentHoveredBuilding);
                _isUIShown = true;
            }
        }

        if (_currentHoveredStorage != null)
        {
            _storageHoverTimer += Time.unscaledDeltaTime;
            if (_storageHoverTimer >= HoverDelay && !_isStorageUIShown)
            {
                ShowStorageInfo(_currentHoveredStorage);
                _isStorageUIShown = true;
            }
        }
    }

    public void OnBuildingEnter(BuildingDataHolder buildingDataHolder)
    {
        if (buildingDataHolder == null || buildingDataHolder.buildingData == null) return;
        if (buildingDataHolder == _currentHoveredBuilding) return;

        if (_currentHoveredBuilding != null && _currentHoveredBuilding != buildingDataHolder)
        {
            ClearHover();
        }

        _currentHoveredBuilding = buildingDataHolder;
        _hoverTimer = 0f;
        _isUIShown = false;
    }

    public void OnBuildingExit(BuildingDataHolder buildingDataHolder)
    {
        if (buildingDataHolder == _currentHoveredBuilding)
        {
            ClearHover();
        }
    }

    public void OnStorageEnter(IStorage storage)
    {
        if (storage == null) return;
        if (storage == _currentHoveredStorage) return;

        if (_currentHoveredStorage != null && _currentHoveredStorage != storage)
        {
            ClearStorageHover();
        }

        _currentHoveredStorage = storage;
        _storageHoverTimer = 0f;
        _isStorageUIShown = false;
    }

    public void OnStorageExit(IStorage storage)
    {
        if (storage == _currentHoveredStorage)
        {
            ClearStorageHover();
        }
    }

    private void ClearAllHovers()
    {
        ClearHover();
        ClearStorageHover();
    }

    private void ShowBuildingInfo(BuildingDataHolder buildingDataHolder)
    {
        if (BuildingInfoPanel.Instance == null) return;
        if (buildingDataHolder.buildingData == null) return;

        BuildingInfoPanel.Instance.PreviewInfo(buildingDataHolder.buildingData);
        Debug.Log($"Show {_currentHoveredBuilding.buildingData.name}");
    }

    private bool IsPointerOverFloatingNumText()
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null && result.gameObject.GetComponent<FloatingNumText>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearHover()
    {
        if (_currentHoveredBuilding != null)
        {
            if (BuildingInfoPanel.Instance != null)
            {
                BuildingInfoPanel.Instance.CancelPreview();
                BuildingInfoPanel.Instance.gameObject.SetActive(false);
            }
            Debug.Log($"Hide {_currentHoveredBuilding.buildingData.name}");
            _currentHoveredBuilding = null;
            _hoverTimer = 0f;
            _isUIShown = false;
        }
    }


    private void ShowStorageInfo(IStorage storage)
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.DisplayStorageInfo(storage);
        }
    }

    private void ClearStorageHover()
    {
        if (_currentHoveredStorage != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            {
                GameManager.Instance.uiManager.HideStorageInfo();
            }
            _currentHoveredStorage = null;
            _storageHoverTimer = 0f;
            _isStorageUIShown = false;
        }
    }

    public void ClearHoverOnClick()
    {
        ClearAllHovers();
    }

    private void OnDisable()
    {
        ClearAllHovers();
    }

    private void OnDestroy()
    {
        if (_mouseHoverDetector != null)
        {
            Destroy(_mouseHoverDetector);
        }
    }
}

