using System;
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
    [SerializeField] private Transform costPanel;
    [SerializeField] private GameObject producableResourceImgPrefab;
    [SerializeField] private Sprite completedSprite;

    public static event Action<int> OnCellSelected;

    private ResearchPanelUI _researchPanelUI;
    private RectTransform _rectTransform;
    private Vector2 _iconContainerSize;
    private Sprite _originalIconSprite;

    public TechData TechData => techData;

    private void Awake()
    {
        _originalIconSprite = gameObject.GetComponent<Image>()?.sprite;
        _rectTransform = GetComponent<RectTransform>();
        if (iconImg != null)
            _iconContainerSize = iconImg.rectTransform.sizeDelta;
    }

    private void OnEnable()
    {
        TechResearchManager.OnResearchStateChanged += RefreshCompletionVisual;
        TechResearchManager.OnResearchCompleted += OnResearchCompleted;
    }

    private void OnDisable()
    {
        TechResearchManager.OnResearchStateChanged -= RefreshCompletionVisual;
        TechResearchManager.OnResearchCompleted -= OnResearchCompleted;
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

    public void OnPointerClick(PointerEventData eventData)
    {
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
        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);
        return (corners[0] + corners[1]) * 0.5f;
    }

    public Vector3 GetRightCenterWorld()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);
        return (corners[2] + corners[3]) * 0.5f;
    }
}
