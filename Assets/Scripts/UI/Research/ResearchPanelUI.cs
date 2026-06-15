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
    [SerializeField] private GameObject producableResourceImgPrefab;
    [SerializeField] private TMP_Text requiredAmountText;
    [SerializeField] private TMP_Text descText;

    [Header("Research Reward Panel")]
    [SerializeField] private Transform rewardTechPanel;
    [SerializeField] private Button statusBtn;
    [SerializeField] private TMP_Text statusBtnText;

    [Header("Research Current Panel")]
    [SerializeField] private Image currentResearchImg;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressSliderText;

    private TechData _selectedTech;
    private static readonly string[] StatusTexts = { "연구 잠김", "연구 진행 전", "연구 진행 중", "연구 진행 완료" };

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
                    if (producableResourceImgPrefab == null)
                    {
                        continue;
                    }

                    GameObject iconObj = Instantiate(producableResourceImgPrefab, costPanel);
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

                    if (producableResourceImgPrefab == null)
                    {
                        continue;
                    }

                    GameObject iconObj = Instantiate(producableResourceImgPrefab, rewardTechPanel);
                    Image iconImage = GetChildImage(iconObj);
                    if (iconImage != null)
                    {
                        iconImage.sprite = successorTech.GetTechIcon();
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
                currentResearchImg.enabled = false;
            }

            if (progressSlider != null)
            {
                progressSlider.value = 0;
            }

            if (progressSliderText != null)
            {
                progressSliderText.text = string.Empty;
            }

            return;
        }

        if (currentResearchImg != null)
        {
            currentResearchImg.sprite = current.GetTechIcon();
            currentResearchImg.enabled = true;
        }

        int prog = TechResearchManager.Instance.GetCurrentProgress();
        int max = TechResearchManager.Instance.GetMaxProgress();

        if (progressSlider != null)
        {
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
            currentResearchImg.enabled = false;
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0;
        }

        if (progressSliderText != null)
        {
            progressSliderText.text = string.Empty;
        }
    }
}
