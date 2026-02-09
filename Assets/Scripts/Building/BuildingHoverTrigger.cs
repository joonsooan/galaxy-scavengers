using UnityEngine;

public class BuildingHoverTrigger : MonoBehaviour
{
    private BuildingDataHolder _buildingDataHolder;
    private IStorage _storage;
    private bool _isInitialized;

    private BoxCollider2D[] _boxColliders;
    private CapsuleCollider2D[] _capsuleColliders;

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
        _capsuleColliders = GetComponentsInChildren<CapsuleCollider2D>();
        
        _isInitialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("MouseHoverDetector"))
        {
            Initialize();

            bool isTouchingBuildingArea = IsTouchingAnyCollider(other);
            bool isTouchingStorageArea = _storage != null && IsTouchingCapsuleCollider(other);

            if (!isTouchingBuildingArea && !isTouchingStorageArea)
            {
                return;
            }

            if (BuildingHoverManager.Instance != null)
            {
                if ((isTouchingBuildingArea || isTouchingStorageArea) && _buildingDataHolder != null && _buildingDataHolder.buildingData != null)
                {
                    BuildingHoverManager.Instance.OnBuildingEnter(_buildingDataHolder);
                }
                if (isTouchingStorageArea && _storage != null)
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
            bool isTouchingBuildingArea = IsTouchingAnyCollider(other);
            bool isTouchingStorageArea = _storage != null && IsTouchingCapsuleCollider(other);

            if (isTouchingBuildingArea || isTouchingStorageArea)
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

    private bool IsTouchingCapsuleCollider(Collider2D other)
    {
        if (_capsuleColliders == null)
        {
            _capsuleColliders = GetComponentsInChildren<CapsuleCollider2D>();
        }
        
        foreach (CapsuleCollider2D capsuleCollider in _capsuleColliders)
        {
            if (capsuleCollider != null && capsuleCollider.enabled && capsuleCollider.isTrigger)
            {
                if (capsuleCollider.IsTouching(other))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool IsTouchingAnyCollider(Collider2D other)
    {
        if (_boxColliders == null)
        {
            _boxColliders = GetComponentsInChildren<BoxCollider2D>();
        }

        Vector2 otherPosition = other.bounds.center;
        
        foreach (BoxCollider2D boxCollider in _boxColliders)
        {
            if (boxCollider != null && boxCollider.enabled)
            {
                if (boxCollider.isTrigger)
                {
                    if (boxCollider.IsTouching(other))
                    {
                        return true;
                    }
                }
                else
                {
                    Bounds boxBounds = boxCollider.bounds;
                    if (boxBounds.Contains(otherPosition))
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
}