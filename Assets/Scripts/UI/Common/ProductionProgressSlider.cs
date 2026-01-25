using UnityEngine;
using UnityEngine.UI;

public class ProductionProgressSlider : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image fillImage;
    
    private DroneHub _targetDroneHub;
    private Vector3 _offset;
    
    public void Initialize(DroneHub droneHub, Vector3 offset)
    {
        _targetDroneHub = droneHub;
        _offset = offset;
        
        if (_targetDroneHub != null)
        {
            UpdatePosition();
            UpdateProgress();
        }
    }
    
    private void Update()
    {
        if (_targetDroneHub != null)
        {
            UpdatePosition();
            UpdateProgress();
            
            bool shouldShow = _targetDroneHub.IsProducing();
            if (gameObject.activeSelf != shouldShow)
            {
                gameObject.SetActive(shouldShow);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    private void UpdatePosition()
    {
        if (_targetDroneHub != null)
        {
            Transform targetTransform = _targetDroneHub.transform;
            if (targetTransform != null)
            {
                transform.position = targetTransform.position + _offset;
                if (Camera.main != null)
                {
                    transform.LookAt(Camera.main.transform);
                    transform.Rotate(0, 180, 0);
                }
            }
        }
    }
    
    private void UpdateProgress()
    {
        if (_targetDroneHub == null || progressSlider == null)
        {
            return;
        }
        
        float progress = _targetDroneHub.GetProductionProgress();
        progressSlider.value = progress;
    }
}
