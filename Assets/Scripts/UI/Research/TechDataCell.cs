using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TechDataCell : MonoBehaviour, IPointerClickHandler
{
    [Header("Tech Data")]
    [SerializeField] private TechData techData;

    [Header("UI References")]
    [SerializeField] private Image cellImg;
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject outlineObj;
    [SerializeField] private Transform costPanel;
    [SerializeField] private GameObject producableResourceImgPrefab;
    [SerializeField] private Sprite completedSprite;

    public static event Action<int> OnCellSelected;

    private static TechDataCell _selectedCell;

    private ResearchPanelUI _researchPanelUI;
    private RectTransform _rectTransform;
    private Vector2 _iconContainerSize;
    private Sprite _originalIconSprite;
    private readonly Vector3[] _corners = new Vector3[4];

    public TechData TechData => techData;

    private void Awake()
    {
        _originalIconSprite = gameObject.GetComponent<Image>()?.sprite;
        _rectTransform = GetComponent<RectTransform>();
        if (iconImg != null)
            _iconContainerSize = iconImg.rectTransform.sizeDelta;
        if (outlineObj != null)
            outlineObj.SetActive(false);
    }

    private void OnEnable()
    {
        TechResearchManager.OnResearchStateChanged += RefreshCompletionVisual;
        TechResearchManager.OnResearchCompleted += OnResearchCompleted;
        ResearchPanelUI.OnSelectionCleared += OnSelectionCleared;
    }

    private void OnDisable()
    {
        TechResearchManager.OnResearchStateChanged -= RefreshCompletionVisual;
        TechResearchManager.OnResearchCompleted -= OnResearchCompleted;
        ResearchPanelUI.OnSelectionCleared -= OnSelectionCleared;
        if (_selectedCell == this)
        {
            SetSelected(false);
            _selectedCell = null;
        }
    }

    private void OnSelectionCleared()
    {
        if (_selectedCell == this)
        {
            SetSelected(false);
            _selectedCell = null;
        }
    }

    private void OnResearchCompleted(int techIndex)
    {
        RefreshCompletionVisual();
    }

    public void Setup(ResearchPanelUI researchPanelUI)
    {
        _researchPanelUI = researchPanelUI;
        InitializeDisplay();
        RefreshCompletionVisual();
    }

    private void InitializeDisplay()
    {
        if (techData == null)
        {
            return;
        }

        if (iconImg != null)
        {
            iconImg.sprite = techData.GetTechIcon();
            if (iconImg.sprite != null)
                ApplyFitSize(iconImg.rectTransform, iconImg.sprite);
        }

        if (nameText != null)
            nameText.text = techData.GetTechName();

        if (costPanel != null)
        {
            for (int i = costPanel.childCount - 1; i >= 0; i--)
            {
                Destroy(costPanel.GetChild(i).gameObject);
            }

            ResourceCost[] costs = techData.researchCosts;
            if (costs != null)
            {
                for (int i = 0; i < costs.Length; i++)
                {
                    if (producableResourceImgPrefab == null)
                    {
                        continue;
                    }

                    ResourceCost cost = costs[i];
                    GameObject iconObj = Instantiate(producableResourceImgPrefab, costPanel);
                    Image iconImage = GetChildImage(iconObj);
                    if (iconImage != null && ResourceManager.Instance != null)
                    {
                        iconImage.sprite = ResourceManager.Instance.GetResourceIcon(cost.resourceType);
                    }
                }
            }
        }
    }

    private void RefreshCompletionVisual()
    {
        if (TechResearchManager.Instance == null || techData == null || cellImg == null)
        {
            return;
        }

        TechResearchState state = TechResearchManager.Instance.GetTechState(techData.techIndex);
        cellImg.sprite = state == TechResearchState.Completed ? completedSprite : _originalIconSprite;
    }

    private void ApplyFitSize(RectTransform iconRt, Sprite sprite)
    {
        float w = sprite.rect.width;
        float h = sprite.rect.height;
        float containerW = _iconContainerSize.x > 0 ? _iconContainerSize.x : w;
        float containerH = _iconContainerSize.y > 0 ? _iconContainerSize.y : h;
        float scale = Mathf.Min(containerW / w, containerH / h);
        iconRt.sizeDelta = new Vector2(w * scale, h * scale);
    }

    private static Image GetChildImage(GameObject obj)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            Image img = obj.transform.GetChild(i).GetComponent<Image>();
            if (img != null)
            {
                return img;
            }
        }
        return null;
    }

    public void SetSelected(bool selected)
    {
        if (outlineObj != null)
            outlineObj.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_selectedCell != null && _selectedCell != this)
            _selectedCell.SetSelected(false);
        _selectedCell = this;
        SetSelected(true);

        if (_researchPanelUI != null)
        {
            _researchPanelUI.SelectTech(techData);
        }

        if (techData != null)
        {
            if (OnCellSelected != null) OnCellSelected(techData.techIndex);
        }
    }

    public Vector3 GetLeftCenterWorld()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
        _rectTransform.GetWorldCorners(_corners);
        return (_corners[0] + _corners[1]) * 0.5f;
    }

    public Vector3 GetRightCenterWorld()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
        _rectTransform.GetWorldCorners(_corners);
        return (_corners[2] + _corners[3]) * 0.5f;
    }
}
