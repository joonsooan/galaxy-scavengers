using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StorageFilterPanel : MonoBehaviour
{
    [Header("Priority")]
    [SerializeField] private Button priorityButton;
    [SerializeField] private GameObject prioritySelectPanel;
    [SerializeField] private Button[] priorityButtons;

    [Header("Resource Filter")]
    [SerializeField] private Transform resourceFilterGrid;
    [SerializeField] private GameObject filterCellPrefab;
    [SerializeField] private Sprite allowedSprite;
    [SerializeField] private Sprite deniedSprite;

    [Header("Bulk Toggle")]
    [SerializeField] private Button allowAllButton;
    [SerializeField] private Button allowNoneButton;

    private IStorage _targetStorage;
    private Dictionary<ResourceType, bool> _allowedResources;
    private Dictionary<ResourceType, Image> _resourceFilterImages;
    private int _selectedPriority;

    private void Awake()
    {
        _allowedResources = new Dictionary<ResourceType, bool>();
        _resourceFilterImages = new Dictionary<ResourceType, Image>();

        if (priorityButton != null)
            priorityButton.onClick.AddListener(OnPriorityButtonClicked);

        if (priorityButtons != null)
        {
            for (int i = 0; i < priorityButtons.Length; i++)
            {
                int level = i;
                if (priorityButtons[i] != null)
                    priorityButtons[i].onClick.AddListener(() => OnPriorityLevelSelected(level));
            }
        }

        if (allowAllButton != null)
            allowAllButton.onClick.AddListener(AllowAll);

        if (allowNoneButton != null)
            allowNoneButton.onClick.AddListener(AllowNone);
    }

    public void Initialize(IStorage storage)
    {
        _targetStorage = storage;

        if (_allowedResources == null)
            _allowedResources = new Dictionary<ResourceType, bool>();
        if (_resourceFilterImages == null)
            _resourceFilterImages = new Dictionary<ResourceType, Image>();

        _allowedResources.Clear();
        Array resourceTypes = Enum.GetValues(typeof(ResourceType));

        StorageFilter currentFilter = (_targetStorage != null) ? _targetStorage.GetFilter() : null;

        _selectedPriority = (currentFilter != null) ? currentFilter.Priority : 0;

        foreach (ResourceType type in resourceTypes)
        {
            if (type == ResourceType.None) continue;
            bool isAllowed = (currentFilter != null) ? currentFilter.IsAllowed(type) : true;
            _allowedResources[type] = isAllowed;
        }

        if (resourceFilterGrid != null)
        {
            foreach (Transform child in resourceFilterGrid)
                Destroy(child.gameObject);
        }

        _resourceFilterImages.Clear();

        if (resourceFilterGrid != null && filterCellPrefab != null)
        {
            foreach (ResourceType type in resourceTypes)
            {
                if (type == ResourceType.None) continue;

                GameObject cellObj = Instantiate(filterCellPrefab, resourceFilterGrid);
                Image cellImage = cellObj.GetComponent<Image>();

                if (cellImage != null)
                    _resourceFilterImages[type] = cellImage;

                if (ResourceManager.Instance != null)
                {
                    Image[] images = cellObj.GetComponentsInChildren<Image>();
                    foreach (Image img in images)
                    {
                        if (img != null && img.gameObject != cellObj)
                        {
                            img.sprite = ResourceManager.Instance.GetResourceIcon(type);
                            break;
                        }
                    }
                }

                Button cellButton = cellObj.GetComponent<Button>();
                if (cellButton == null)
                    cellButton = cellObj.AddComponent<Button>();

                ResourceType capturedType = type;
                cellButton.onClick.AddListener(() => ToggleResource(capturedType));
            }
        }

        RefreshAllFilterSprites();

        if (prioritySelectPanel != null)
            prioritySelectPanel.SetActive(false);
    }

    public void ToggleResource(ResourceType type)
    {
        if (!_allowedResources.ContainsKey(type)) return;

        _allowedResources[type] = !_allowedResources[type];

        if (_resourceFilterImages.ContainsKey(type))
        {
            Image img = _resourceFilterImages[type];
            if (img != null)
                img.sprite = _allowedResources[type] ? allowedSprite : deniedSprite;
        }

        if (_targetStorage != null)
        {
            StorageFilter filter = _targetStorage.GetFilter();
            if (filter != null)
            {
                filter.SetAllowed(type, _allowedResources[type]);
                _targetStorage.SetFilter(filter);
            }
        }

        EventSystem.current.SetSelectedGameObject(null);
    }

    public void AllowAll()
    {
        Array resourceTypes = Enum.GetValues(typeof(ResourceType));
        foreach (ResourceType type in resourceTypes)
        {
            if (type == ResourceType.None) continue;
            if (_allowedResources.ContainsKey(type))
                _allowedResources[type] = true;
        }
        RefreshAllFilterSprites();
        PushAllowedResourcesToStorage();
    }

    public void AllowNone()
    {
        Array resourceTypes = Enum.GetValues(typeof(ResourceType));
        foreach (ResourceType type in resourceTypes)
        {
            if (type == ResourceType.None) continue;
            if (_allowedResources.ContainsKey(type))
                _allowedResources[type] = false;
        }
        RefreshAllFilterSprites();
        PushAllowedResourcesToStorage();
    }

    private void PushAllowedResourcesToStorage()
    {
        if (_targetStorage == null) return;

        StorageFilter filter = _targetStorage.GetFilter();
        if (filter == null) return;

        foreach (KeyValuePair<ResourceType, bool> kvp in _allowedResources)
            filter.SetAllowed(kvp.Key, kvp.Value);

        _targetStorage.SetFilter(filter);
    }

    private void RefreshAllFilterSprites()
    {
        foreach (KeyValuePair<ResourceType, Image> kvp in _resourceFilterImages)
        {
            Image img = kvp.Value;
            if (img == null) continue;

            bool isAllowed = _allowedResources.ContainsKey(kvp.Key) && _allowedResources[kvp.Key];
            img.sprite = isAllowed ? allowedSprite : deniedSprite;
        }
    }

    public void OnPriorityButtonClicked()
    {
        if (prioritySelectPanel != null)
            prioritySelectPanel.SetActive(!prioritySelectPanel.activeSelf);
    }

    public void OnPriorityLevelSelected(int level)
    {
        _selectedPriority = level;

        if (_targetStorage != null)
        {
            StorageFilter filter = _targetStorage.GetFilter();
            if (filter != null)
            {
                filter.SetPriority(level);
                _targetStorage.SetFilter(filter);
            }
        }

        if (prioritySelectPanel != null)
            prioritySelectPanel.SetActive(false);
    }

    public Dictionary<ResourceType, bool> GetAllowedResources()
    {
        return _allowedResources;
    }

    public int GetPriority()
    {
        return _selectedPriority;
    }
}
