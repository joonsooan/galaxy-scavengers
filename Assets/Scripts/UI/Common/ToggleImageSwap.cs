using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleImageSwap : MonoBehaviour
{
    [SerializeField]
    private Image targetImage;

    [SerializeField]
    private Color onColor = Color.white;

    [SerializeField]
    private Color offColor = Color.gray;
    
    private Toggle _toggle;

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();

        _toggle.onValueChanged.AddListener(UpdateImageColor);
    }

    private void Start()
    {
        UpdateImageColor(_toggle.isOn);
    }

    private void UpdateImageColor(bool isOn)
    {
        if (targetImage == null) return;
        
        targetImage.color = isOn ? onColor : offColor;
    }

    private void OnDestroy()
    {
        if (_toggle != null)
        {
            _toggle.onValueChanged.RemoveListener(UpdateImageColor);
        }
    }
}