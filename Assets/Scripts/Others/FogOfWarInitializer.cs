using UnityEngine;

/// <summary>
/// Helper component that can be added to GameObjects to automatically set up fog of war components.
/// This is useful for ensuring enemies, resources, buildings, and units have the correct components.
/// </summary>
public class FogOfWarInitializer : MonoBehaviour
{
    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool addVisionProvider = false;
    [SerializeField] private bool addVisibilityController = false;
    
    [Header("Vision Provider Settings")]
    [SerializeField] private float visionRange = 5f;
    
    [Header("Visibility Controller Settings")]
    [SerializeField] private VisibilityController.VisibilityType visibilityType = VisibilityController.VisibilityType.Enemy;
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupComponents();
        }
    }
    
    public void SetupComponents()
    {
        // Add VisionProvider if needed
        if (addVisionProvider && GetComponent<VisionProvider>() == null)
        {
            VisionProvider visionProvider = gameObject.AddComponent<VisionProvider>();
            // Note: VisionProvider has its own serialized fields, so we can't set them here
            // The user should configure the VisionProvider component in the inspector
        }
        
        // Add VisibilityController if needed
        if (addVisibilityController && GetComponent<VisibilityController>() == null)
        {
            VisibilityController visibilityController = gameObject.AddComponent<VisibilityController>();
            // Note: VisibilityController has its own serialized fields, so we can't set them here
            // The user should configure the VisibilityController component in the inspector
        }
    }
}

