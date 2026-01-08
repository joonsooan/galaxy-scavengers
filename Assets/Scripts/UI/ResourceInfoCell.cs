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
        
        Sprite resourceIcon = GetResourceIcon(type);
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
    
    private Sprite GetResourceIcon(ResourceType type)
    {
        // Try BaseResourceDataManager first (for base scene)
        BaseResourceDataManager resourceDataManager = FindFirstObjectByType<BaseResourceDataManager>();
        if (resourceDataManager != null)
        {
            return resourceDataManager.GetResourceIcon(type);
        }
        
        // Fallback to ResourceManager (for game scene compatibility)
        if (ResourceManager.Instance != null)
        {
            return ResourceManager.Instance.GetResourceIcon(type);
        }
        
        return null;
    }
}
