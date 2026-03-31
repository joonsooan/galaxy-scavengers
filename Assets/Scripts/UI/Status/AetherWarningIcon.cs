using UnityEngine;
using UnityEngine.UI;

public class AetherWarningIcon : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject iconObject;
    [SerializeField] private Image iconImage;
    
    private IElectricityConsumer _electricityConsumer;
    
    private void Awake()
    {
        _electricityConsumer = GetComponentInParent<IElectricityConsumer>();
        
        if (iconObject != null)
        {
            iconObject.SetActive(false);
        }
    }
    
    private void Update()
    {
        if (_electricityConsumer != null && iconObject != null)
        {
            iconObject.SetActive(!_electricityConsumer.IsOperational);
        }
    }
}
