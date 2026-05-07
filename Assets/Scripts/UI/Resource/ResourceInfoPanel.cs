using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image resourceIconImage;
    [SerializeField] private TMP_Text resourceNameText;
    [SerializeField] private TMP_Text resourceAmountText;
    [SerializeField] private TMP_Text resourceDescText;

    private ResourceNode _currentResourceNode;

    public static ResourceInfoPanel Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ClearAllInfo();
    }

    public void PreviewInfo(ResourceNode node)
    {
        if (node == null)
        {
            CancelPreview();
            return;
        }
        _currentResourceNode = node;
        RefreshUI();
        gameObject.SetActive(true);
    }

    public void CancelPreview()
    {
        _currentResourceNode = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    public void ClearAllInfo()
    {
        _currentResourceNode = null;
        ClearUI();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_currentResourceNode == null)
        {
            return;
        }
        if (_currentResourceNode.IsDepleted || !_currentResourceNode.gameObject.activeInHierarchy)
        {
            CancelPreview();
            return;
        }
        RefreshAmountText();
    }

    private void RefreshUI()
    {
        if (_currentResourceNode == null)
        {
            ClearUI();
            return;
        }
        if (ResourceManager.Instance != null)
        {
            if (resourceNameText != null)
            {
                resourceNameText.text = ResourceManager.Instance.GetResourceDisplayName(_currentResourceNode.resourceType);
            }
            if (resourceDescText != null)
            {
                resourceDescText.text = ResourceManager.Instance.GetResourceDescription(_currentResourceNode.resourceType);
            }
        }
        Sprite icon = GetResourceIcon(_currentResourceNode.resourceType);
        if (resourceIconImage != null)
        {
            resourceIconImage.sprite = icon;
            resourceIconImage.enabled = icon != null;
        }
        RefreshAmountText();
        RebuildLayout();
    }

    private void RefreshAmountText()
    {
        if (resourceAmountText == null || _currentResourceNode == null)
        {
            return;
        }
        int current = _currentResourceNode.amountToMine;
        int initial = _currentResourceNode.InitialAmountToMine;
        resourceAmountText.text = $"{current} / {initial}";
    }

    private void ClearUI()
    {
        if (resourceIconImage != null)
        {
            resourceIconImage.sprite = null;
            resourceIconImage.enabled = false;
        }
        if (resourceNameText != null)
        {
            resourceNameText.text = string.Empty;
        }
        if (resourceAmountText != null)
        {
            resourceAmountText.text = string.Empty;
        }
        if (resourceDescText != null)
        {
            resourceDescText.text = string.Empty;
        }
    }

    private void RebuildLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (resourceNameText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(resourceNameText.rectTransform);
        if (resourceIconImage != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(resourceIconImage.rectTransform);
        if (resourceDescText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(resourceDescText.rectTransform);

        if (resourceNameText != null && resourceNameText.transform.parent is RectTransform headerRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);

        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
    }

    private Sprite GetResourceIcon(ResourceType type)
    {
        BaseResourceDataManager resourceDataManager = FindFirstObjectByType<BaseResourceDataManager>();
        if (resourceDataManager != null)
        {
            return resourceDataManager.GetResourceIcon(type);
        }
        if (ResourceManager.Instance != null)
        {
            return ResourceManager.Instance.GetResourceIcon(type);
        }
        return null;
    }

    public void WarmupFirstUse()
    {
        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(true);
        WarmTouchTmp(resourceNameText);
        WarmTouchTmp(resourceAmountText);
        WarmTouchTmp(resourceDescText);
        if (resourceIconImage != null)
        {
            resourceIconImage.enabled = true;
        }
        RebuildLayout();
        Canvas.ForceUpdateCanvases();
        ClearAllInfo();
        if (wasActive)
        {
            gameObject.SetActive(true);
        }
    }

    private static void WarmTouchTmp(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }
        text.text = " ";
        text.ForceMeshUpdate(true);
        text.text = string.Empty;
    }
}
