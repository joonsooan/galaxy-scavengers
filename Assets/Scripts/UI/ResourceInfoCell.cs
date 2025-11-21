using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceInfoCell : MonoBehaviour
{
    [Header("References")]
    public Image resourceImage;
    public TMP_Text resourceAmount;
    
    public void SetInfo(ResourceType type, int amount)
    {
        SetInfo(type, amount, true);
    }
    
    public void SetInfo(ResourceType type, int amount, bool rebuildImmediately)
    {
        resourceAmount.text = amount.ToString();
        
        Sprite resourceIcon = ResourceManager.Instance.GetResourceIcon(type);
        if (resourceIcon != null)
        {
            resourceImage.sprite = resourceIcon;
        }
        else
        {
            resourceImage.sprite = null; 
        }
        
        if (rebuildImmediately)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
    }
}
