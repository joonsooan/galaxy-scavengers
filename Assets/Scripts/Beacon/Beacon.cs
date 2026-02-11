using UnityEngine;

public class Beacon : MonoBehaviour
{
    [Header("Beacon Settings")]
    [SerializeField] private float stayInterval = 5f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color defaultColor = Color.yellow;
    [SerializeField] private Color activeColor = Color.green;
    
    public float StayInterval => stayInterval;
    public Vector3 Position => transform.position;
    private bool IsAssigned { get; set; }
    public Unit_Scout AssignedUnit { get; private set; }
    
    private void Awake()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultColor;
        }
        if (BeaconManager.Instance != null)
            BeaconManager.Instance.RegisterBeacon(this);
    }

    private void Start()
    {
        if (BeaconManager.Instance != null)
            BeaconManager.Instance.RegisterBeacon(this);
    }
    
    public void AssignUnit(Unit_Scout unit)
    {
        AssignedUnit = unit;
        IsAssigned = true;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = activeColor;
        }
    }
    
    public void UnassignUnit()
    {
        AssignedUnit = null;
        IsAssigned = false;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultColor;
        }
    }
    
    public void DestroyBeacon()
    {
        if (IsAssigned && AssignedUnit != null)
        {
            AssignedUnit.OnBeaconDestroyed(this);
        }
        
        if (BeaconManager.Instance != null)
        {
            BeaconManager.Instance.RemoveBeacon(this);
        }
        
        Destroy(gameObject);
    }
}

