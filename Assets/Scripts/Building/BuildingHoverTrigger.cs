using UnityEngine;

public class BuildingHoverTrigger : MonoBehaviour
{
    private BuildingDataHolder _buildingDataHolder;
    private IStorage _storage;
    private bool _isInitialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        _buildingDataHolder = GetComponent<BuildingDataHolder>();
        _storage = GetComponent<IStorage>();
        
        _isInitialized = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("MouseHoverDetector"))
        {
            Initialize();
            
            if (BuildingHoverManager.Instance != null)
            {
                if (_buildingDataHolder.buildingData != null)
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
            if (BuildingHoverManager.Instance != null)
            {
                if (_buildingDataHolder.buildingData != null)
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
}
