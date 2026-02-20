using UnityEngine;

public class BuildingHoverTrigger : MonoBehaviour
{
    private BuildingDataHolder _buildingDataHolder;
    private IStorage _storage;
    private bool _isInitialized;

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
        _capsuleColliders = GetComponentsInChildren<CapsuleCollider2D>();

        _isInitialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("MouseHoverDetector"))
        {
            Initialize();

            bool isTouchingBuildingArea = IsTouchingCapsuleCollider(other);

            if (!isTouchingBuildingArea)
            {
                return;
            }

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
            bool isTouchingBuildingArea = IsTouchingCapsuleCollider(other);

            if (isTouchingBuildingArea)
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
}