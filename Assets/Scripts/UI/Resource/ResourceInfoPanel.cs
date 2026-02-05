using TMPro;
using UnityEngine;

public class ResourceInfoPanel : MonoBehaviour
{
    [Header("UI References")]
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
        RefreshAmountText();
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
}
