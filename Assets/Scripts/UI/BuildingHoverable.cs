using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingHoverable : MonoBehaviour
{
    private BuildingData _buildingData;
    private Collider2D _collider;
    private bool _isHovering;
    private bool _isInitialized;

    private void Start()
    {
        _collider = GetComponent<Collider2D>();
        InitializeBuildingData();
    }

    private void Update()
    {
        CheckMouseHover();
    }

    private void OnDisable()
    {
        ResetHover();
    }

    private void CheckMouseHover()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsDragging() ||
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            ResetHover();
            return;
        }

        if (_collider == null) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hitColliders = Physics2D.OverlapPointAll(mousePos);

        bool isHitMe = false;
        foreach (Collider2D col in hitColliders) {
            if (col == _collider) {
                isHitMe = true;
                break;
            }
        }

        if (isHitMe) {
            if (!_isHovering) {
                ShowBuildingInfo();
            }
        }
        else {
            ResetHover();
        }
    }

    private void ShowBuildingInfo()
    {
        if (!_isInitialized) {
            InitializeBuildingData();
        }

        if (_buildingData == null) {
            RefreshBuildingData();
        }

        if (_buildingData == null) {
            Debug.Log($"BuildingData is null for {gameObject.name} at position {transform.position}");
            return;
        }

        if (BuildingInfoPanel.Instance != null) {
            _isHovering = true;
            BuildingInfoPanel.Instance.PreviewInfo(_buildingData);
        }
    }

    private void RefreshBuildingData()
    {
        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            Vector3Int cellPosition = GetCellPosition();
            _buildingData = BuildingManager.Instance.GetBuildingDataAt(cellPosition);
        }
    }

    private void ResetHover()
    {
        if (_isHovering) {
            _isHovering = false;
            if (BuildingInfoPanel.Instance != null) {
                BuildingInfoPanel.Instance.CancelPreview();
            }
        }
    }

    private void InitializeBuildingData()
    {
        if (_isInitialized) return;

        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            Vector3Int cellPosition = GetCellPosition();
            _buildingData = BuildingManager.Instance.GetBuildingDataAt(cellPosition);
            _isInitialized = true;
        }
    }

    private Vector3Int GetCellPosition()
    {
        BuildingPiece buildingPiece = GetComponent<BuildingPiece>();
        if (buildingPiece != null && buildingPiece.cellPosition != Vector3Int.zero) {
            return buildingPiece.cellPosition;
        }

        if (BuildingManager.Instance != null && BuildingManager.Instance.grid != null) {
            return BuildingManager.Instance.grid.WorldToCell(transform.position);
        }

        return Vector3Int.zero;
    }
}
