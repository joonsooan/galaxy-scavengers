using UnityEngine;

public class FogOfWarInitializer : MonoBehaviour
{
    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool addVisionProvider = false;
    [SerializeField] private bool addVisibilityController = false;
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupComponents();
        }
    }
    
    public void SetupComponents()
    {
        if (addVisionProvider && GetComponent<VisionProvider>() == null)
        {
            VisionProvider visionProvider = gameObject.AddComponent<VisionProvider>();
        }
        
        if (addVisibilityController && GetComponent<VisibilityController>() == null)
        {
            VisibilityController visibilityController = gameObject.AddComponent<VisibilityController>();
        }
    }
}

