using UnityEngine;

/// <summary>
/// Component that can be added to buildings and units to provide vision in the fog of war system.
/// </summary>
public class VisionProvider : MonoBehaviour, IVisionProvider
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private bool isActive = true;
    
    public float VisionRange => visionRange;
    public bool IsActive => isActive;
    
    private bool _isRegistered = false;
    
    private void OnEnable()
    {
        RegisterWithFogOfWar();
    }
    
    private void Start()
    {
        // Try to register again in Start in case FogOfWarManager wasn't ready in OnEnable
        RegisterWithFogOfWar();
    }
    
    private void OnDisable()
    {
        UnregisterFromFogOfWar();
    }
    
    private void RegisterWithFogOfWar()
    {
        if (!_isRegistered && FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.RegisterVisionProvider(this);
            _isRegistered = true;
        }
    }
    
    private void UnregisterFromFogOfWar()
    {
        if (_isRegistered && FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.UnregisterVisionProvider(this);
            _isRegistered = false;
        }
    }
    
    // Called when FogOfWarManager becomes available
    public void OnFogOfWarManagerReady()
    {
        RegisterWithFogOfWar();
    }
    
    public Vector3 GetPosition()
    {
        return transform.position;
    }
    
    public float GetVisionRange()
    {
        return visionRange;
    }
    
    public bool CheckIsActive()
    {
        return isActive && gameObject.activeInHierarchy;
    }
    
    // Allow runtime changes
    public void SetVisionRange(float range)
    {
        visionRange = range;
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
    }
    
    // Visualize vision range in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}

