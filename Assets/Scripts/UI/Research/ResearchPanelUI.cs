using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchPanelUI : MonoBehaviour
{
    [Header("Research Info Panel")]
    [SerializeField] private Image iconImg;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform costPanel;
    [SerializeField] private GameObject costResourceImgPrefab;
    [SerializeField] private TMP_Text requiredAmountText;
    [SerializeField] private TMP_Text descText;

    [Header("Research Reward Panel")]
    [SerializeField] private Transform rewardTechPanel;
    [SerializeField] private GameObject rewardTechImgPrefab;
    [SerializeField] private Button statusBtn;
    [SerializeField] private TMP_Text statusBtnText;
    [SerializeField] private ResearchStatusButton statusResearchBtn;

    [Header("Research Current Panel")]
    [SerializeField] private Image currentResearchImg;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressSliderText;

    public static event System.Action OnSelectionCleared;

    private TechData _selectedTech;
    private Vector2 _iconContainerSize;
    private static readonly string[] StatusTexts = { "연구 잠김", "연구 진행 전", "연구 진행 중", "연구 진행 완료" };

    private void Awake()
    {
        if (iconImg != null)
            _iconContainerSize = iconImg.rectTransform.sizeDelta;
    }

    private void Start()
    {
        ClearCurrentResearchPanel();
    }

    private void OnEnable()
    {
        TechResearchManager.OnResearchStateChanged += RefreshStatusBtn;
        TechResearchManager.OnResearchProgressChanged += RefreshCurrentResearchPanel;
        TechResearchManager.OnResearchCompleted += OnResearchCompleted;
    }

    private void OnDisable()
    {
        TechResearchManager.OnResearchStateChanged -= RefreshStatusBtn;
        TechResearchManager.OnResearchProgressChanged -= RefreshCurrentResearchPanel;
        TechResearchManager.OnResearchCompleted -= OnResearchCompleted;
    }

    public void SelectTech(TechData techData)
    {
        _selectedTech = techData;
        RefreshInfoPanel();
        RefreshRewardPanel();
        RefreshStatusBtn();
    }

    public void ClearSelection()
    {
        _selectedTech = null;
        ClearInfoPanel();
        ClearRewardPanel();
        ClearStatusBtn();
        RefreshCurrentResearchPanel();
        if (OnSelectionCleared != null) OnSelectionCleared();
    }

    private void RefreshInfoPanel()
    {
        if (_selectedTech == null)
        {
            ClearInfoPanel();
            return;
        }

        if (iconImg != null)
        {
            iconImg.sprite = _selectedTech.GetTechIcon();
            if (iconImg.sprite != null)
                ApplyFitSize(iconImg.rectTransform, iconImg.sprite);
        }

        if (nameText != null)
        {
            nameText.text = _selectedTech.GetTechName();
        }

        if (descText != null)
        {
            descText.text = _selectedTech.GetTechDescription();
        }

        if (costPanel != null)
        {
            for (int i = costPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = costPanel.GetChild(i);
                if (requiredAmountText != null && child == requiredAmountText.transform)
                {
                    continue;
                }
                Destroy(child.gameObject);
            }

            ResourceCost[] costs = _selectedTech.researchCosts;
            if (costs != null)
            {
                for (int i = 0; i < costs.Length; i++)
                {
                    ResourceCost cost = costs[i];
                    if (costResourceImgPrefab == null)
                    {
                        continue;
                    }

                    GameObject iconObj = Instantiate(costResourceImgPrefab, costPanel);
                    Image iconImage = GetChildImage(iconObj);
                    if (iconImage != null && ResourceManager.Instance != null)
                    {
                        iconImage.sprite = ResourceManager.Instance.GetResourceIcon(cost.resourceType);
                    }
                }
            }

            if (requiredAmountText != null)
            {
                requiredAmountText.transform.SetAsLastSibling();
                if (costs != null && costs.Length > 0)
                {
                    requiredAmountText.text = costs[0].amount.ToString();
                }
                else
                {
                    requiredAmountText.text = "0";
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)costPanel);
            }
        }
    }

    private void RefreshRewardPanel()
    {
        if (_selectedTech == null)
        {
            ClearRewardPanel();
            return;
        }

        if (rewardTechPanel != null)
        {
            foreach (Transform child in rewardTechPanel)
            {
                Destroy(child.gameObject);
            }

            int[] successorIndices = _selectedTech.successorTechIndices;
            if (successorIndices != null && TechResearchManager.Instance != null)
            {
                for (int i = 0; i < successorIndices.Length; i++)
                {
                    TechData successorTech = TechResearchManager.Instance.GetTechData(successorIndices[i]);
                    if (successorTech == null)
                    {
                        continue;
                    }

                    if (rewardTechImgPrefab == null)
                    {
                        continue;
                    }

                    GameObject iconObj = Instantiate(rewardTechImgPrefab, rewardTechPanel);
                    Image iconImage = GetChildImage(iconObj);
                    if (iconImage != null)
                    {
                        Vector2 containerSize = iconImage.rectTransform.sizeDelta;
                        iconImage.sprite = successorTech.GetTechIcon();
                        if (iconImage.sprite != null)
                            ApplyFitSize(iconImage.rectTransform, iconImage.sprite, containerSize);
                    }
                }
            }
        }

        RefreshStatusBtn();
    }

    private void RefreshStatusBtn()
    {
        if (_selectedTech == null || TechResearchManager.Instance == null)
        {
            ClearStatusBtn();
            return;
        }

        TechResearchState state = TechResearchManager.Instance.GetTechState(_selectedTech.techIndex);
        int stateIndex = (int)state;

        if (statusBtnText != null && stateIndex >= 0 && stateIndex < StatusTexts.Length)
        {
            statusBtnText.text = StatusTexts[stateIndex];
        }

        if (statusBtn != null)
        {
            statusBtn.interactable = state == TechResearchState.Available;
        }

        if (statusResearchBtn != null)
        {
            statusResearchBtn.SetState(state);
        }
    }

    private void RefreshCurrentResearchPanel()
    {
        if (TechResearchManager.Instance == null)
        {
            ClearCurrentResearchPanel();
            return;
        }

        TechData current = TechResearchManager.Instance.GetCurrentResearch();
        if (current == null)
        {
            if (currentResearchImg != null)
            {
                currentResearchImg.sprite = null;
                currentResearchImg.gameObject.SetActive(false);
            }

            if (progressSlider != null)
            {
                progressSlider.gameObject.SetActive(false);
            }

            if (progressSliderText != null)
            {
                progressSliderText.text = string.Empty;
            }

            return;
        }

        if (currentResearchImg != null)
        {
            currentResearchImg.gameObject.SetActive(true);
            currentResearchImg.sprite = current.GetTechIcon();
        }

        int prog = TechResearchManager.Instance.GetCurrentProgress();
        int max = TechResearchManager.Instance.GetMaxProgress();

        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(true);
            progressSlider.minValue = 0;
            progressSlider.maxValue = max;
            progressSlider.value = prog;
        }

        if (progressSliderText != null)
        {
            progressSliderText.text = $"{prog}/{max}";
        }
    }

    private void OnResearchCompleted(int techIndex)
    {
        RefreshCurrentResearchPanel();
        RefreshStatusBtn();
    }

    public void OnStatusBtnClick()
    {
        if (_selectedTech == null || TechResearchManager.Instance == null)
        {
            return;
        }

        TechResearchState state = TechResearchManager.Instance.GetTechState(_selectedTech.techIndex);
        if (state == TechResearchState.Available)
        {
            TechResearchManager.Instance.StartResearch(_selectedTech.techIndex);
        }
    }

    private void ClearInfoPanel()
    {
        if (iconImg != null)
        {
            iconImg.sprite = null;
        }

        if (nameText != null)
        {
            nameText.text = string.Empty;
        }

        if (descText != null)
        {
            descText.text = string.Empty;
        }

        if (costPanel != null)
        {
            for (int i = costPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = costPanel.GetChild(i);
                if (requiredAmountText != null && child == requiredAmountText.transform)
                {
                    continue;
                }
                Destroy(child.gameObject);
            }
        }

        if (requiredAmountText != null)
        {
            requiredAmountText.text = string.Empty;
        }
    }

    private void ClearRewardPanel()
    {
        if (rewardTechPanel != null)
        {
            foreach (Transform child in rewardTechPanel)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ClearStatusBtn()
    {
        if (statusBtnText != null)
        {
            statusBtnText.text = string.Empty;
        }

        if (statusBtn != null)
        {
            statusBtn.interactable = false;
        }
    }

    private void ApplyFitSize(RectTransform iconRt, Sprite sprite)
    {
        ApplyFitSize(iconRt, sprite, _iconContainerSize);
    }

    private static void ApplyFitSize(RectTransform iconRt, Sprite sprite, Vector2 containerSize)
    {
        float w = sprite.rect.width;
        float h = sprite.rect.height;
        float containerW = containerSize.x > 0 ? containerSize.x : w;
        float containerH = containerSize.y > 0 ? containerSize.y : h;
        float scale = Mathf.Min(1f, Mathf.Min(containerW / w, containerH / h));
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

    private void ClearCurrentResearchPanel()
    {
        if (currentResearchImg != null)
        {
            currentResearchImg.sprite = null;
            currentResearchImg.gameObject.SetActive(false);
        }

        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(false);
        }

        if (progressSliderText != null)
        {
            progressSliderText.text = string.Empty;
        }
    }
}
