using UnityEngine;
using UnityEngine.UI;

public class AetherWarningIcon : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject iconObject;
    [SerializeField] private Image iconImage;
    
    private IAetherConsumer _aetherConsumer;
    
    private void Awake()
    {
        _aetherConsumer = GetComponentInParent<IAetherConsumer>();
        
        if (iconObject != null)
        {
            iconObject.SetActive(false);
        }
    }
    
    private void Update()
    {
        if (_aetherConsumer != null && iconObject != null)
        {
            iconObject.SetActive(!_aetherConsumer.IsOperational);
        }
    }
}
