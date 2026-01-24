using UnityEngine;

public class BuildingHoverTrigger : MonoBehaviour
{
    private BuildingDataHolder _buildingDataHolder;
    private IStorage _storage;
    private bool _isInitialized;

    private BoxCollider2D[] _boxColliders;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        _buildingDataHolder = GetComponent<BuildingDataHolder>();
        _storage = GetComponent<IStorage>();
        _boxColliders = GetComponentsInChildren<BoxCollider2D>();
        
        _isInitialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("MouseHoverDetector"))
        {
            if (!IsTouchingAnyBoxCollider(other))
            {
                return; 
            }

            Initialize();
            
            if (BuildingHoverManager.Instance != null)
            {
                if (_buildingDataHolder != null && _buildingDataHolder.buildingData != null)
                {
                    BuildingHoverManager.Instance.OnBuildingEnter(_buildingDataHolder);
                }
                if (_storage != null)
                {
                    BuildingHoverManager.Instance.OnStorageEnter(_storage);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("MouseHoverDetector"))
        {
            if (IsTouchingAnyBoxCollider(other))
            {
                return;
            }

            if (BuildingHoverManager.Instance != null)
            {
                if (_buildingDataHolder != null && _buildingDataHolder.buildingData != null)
                {
                    BuildingHoverManager.Instance.OnBuildingExit(_buildingDataHolder);
                }
                if (_storage != null)
                {
                    BuildingHoverManager.Instance.OnStorageExit(_storage);
                }
            }
        }
    }

    private bool IsTouchingAnyBoxCollider(Collider2D other)
    {
        if (_boxColliders == null)
        {
            _boxColliders = GetComponentsInChildren<BoxCollider2D>();
        }

        foreach (BoxCollider2D boxCollider in _boxColliders)
        {
            if (boxCollider != null && boxCollider.enabled && boxCollider.isTrigger)
            {
                if (boxCollider.IsTouching(other))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}