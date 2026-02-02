using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RequestQuestAcceptPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text requiredResourceText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private GameObject requiredResourcesGridContainer;
    [SerializeField] private GameObject rewardGridContainer;
    [SerializeField] private GameObject baseInventoryCellPrefab;
    [SerializeField] private GameObject moduleInventoryCellPrefab;
    [SerializeField] private GameObject creditRewardCellPrefab;
    [SerializeField] private GameObject buildingRewardCellPrefab;
    [SerializeField] private GameObject unitRewardCellPrefab;
    [SerializeField] private GameObject requirementTextPrefab;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    private int _currentQuestId = -1;

    private void Awake()
    {
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(OnAcceptButtonClicked);
        }

        if (rejectButton != null)
        {
            rejectButton.onClick.RemoveAllListeners();
            rejectButton.onClick.AddListener(OnRejectButtonClicked);
        }
    }

    public void DisplayQuestInfo(QuestData questData)
    {
        if (questData == null)
        {
            return;
        }

        _currentQuestId = questData.questId;

        if (questNameText != null)
        {
            questNameText.text = questData.questName;
            questNameText.gameObject.SetActive(true);
        }

        UpdateRequiredResourceText(questData);

        if (requiredResourceText != null)
        {
            requiredResourceText.gameObject.SetActive(true);
        }

        if (rewardText != null)
        {
            rewardText.text = "보상";
            rewardText.gameObject.SetActive(true);
        }

        DisplayRequiredResources(questData);
        DisplayQuestRewards(questData);
    }

    public void ClearQuestInfo()
    {
        _currentQuestId = -1;

        if (questNameText != null)
        {
            questNameText.text = "";
            questNameText.gameObject.SetActive(false);
        }

        if (requiredResourceText != null)
        {
            requiredResourceText.text = "";
            requiredResourceText.gameObject.SetActive(false);
        }

        if (rewardText != null)
        {
            rewardText.text = "";
            rewardText.gameObject.SetActive(false);
        }

        ClearRequiredResources();
        ClearQuestRewards();
    }

    private void UpdateRequiredResourceText(QuestData questData)
    {
        if (requiredResourceText == null)
        {
            return;
        }

        bool hasNonResourceRequirements = false;
        QuestCheckType? primaryCheckType = null;

        if (questData.questCheckRequirements != null && questData.questCheckRequirements.Length > 0)
        {
            foreach (QuestCheckData checkData in questData.questCheckRequirements)
            {
                if (checkData.checkType != QuestCheckType.ResourceRequirement)
                {
                    hasNonResourceRequirements = true;
                    if (primaryCheckType == null)
                    {
                        primaryCheckType = checkData.checkType;
                    }
                    if (checkData.checkType != QuestCheckType.Default)
                    {
                        break;
                    }
                }
            }
        }

        if (hasNonResourceRequirements && primaryCheckType.HasValue)
        {
            requiredResourceText.text = GetRequirementTextForCheckType(primaryCheckType.Value);
        }
        else if (questData.requiredResources != null && questData.requiredResources.Length > 0)
        {
            requiredResourceText.text = "필요한 자원";
        }
        else
        {
            requiredResourceText.text = "요구사항";
        }
    }

    private string GetRequirementTextForCheckType(QuestCheckType checkType)
    {
        return checkType switch
        {
            QuestCheckType.ResourceRequirement => "자원 요구사항",
            QuestCheckType.BuildingConstructed => "건설 요구사항",
            QuestCheckType.UnitProduced => "생산 요구사항",
            QuestCheckType.BeaconPlacedForScout => "탐사 요구사항",
            QuestCheckType.ScoutEnteredLocation => "탐사 요구사항",
            QuestCheckType.ModulePlacedOnCore => "모듈 요구사항",
            QuestCheckType.Default => "요구사항",
            _ => "요구사항"
        };
    }

    private void DisplayRequiredResources(QuestData questData)
    {
        if (requiredResourcesGridContainer == null || baseInventoryCellPrefab == null)
        {
            return;
        }

        foreach (Transform child in requiredResourcesGridContainer.transform)
        {
            Destroy(child.gameObject);
        }

        if (questData.questCheckRequirements != null && questData.questCheckRequirements.Length > 0)
        {
            foreach (QuestCheckData checkData in questData.questCheckRequirements)
            {
                if (checkData.checkType == QuestCheckType.BuildingConstructed ||
                    checkData.checkType == QuestCheckType.UnitProduced ||
                    checkData.checkType == QuestCheckType.BeaconPlacedForScout ||
                    checkData.checkType == QuestCheckType.ScoutEnteredLocation ||
                    checkData.checkType == QuestCheckType.ModulePlacedOnCore ||
                    checkData.checkType == QuestCheckType.Default)
                {
                    if (!string.IsNullOrEmpty(checkData.displayText))
                    {
                        if (requirementTextPrefab != null)
                        {
                            GameObject textObj = Instantiate(requirementTextPrefab, requiredResourcesGridContainer.transform);
                            TMP_Text textComponent = textObj.GetComponent<TMP_Text>();
                            if (textComponent != null)
                            {
                                textComponent.text = checkData.displayText;
                            }
                        }
                        else
                        {
                            GameObject textObj = new GameObject("RequirementText");
                            textObj.transform.SetParent(requiredResourcesGridContainer.transform);
                            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
                            rectTransform.sizeDelta = new Vector2(200, 30);
                            TMP_Text textComponent = textObj.AddComponent<TextMeshProUGUI>();
                            textComponent.text = checkData.displayText;
                            textComponent.fontSize = 14;
                        }
                    }
                }
            }
        }

        if (questData.requiredResources != null && questData.requiredResources.Length > 0)
        {
            foreach (ResourceCost cost in questData.requiredResources)
            {
                GameObject cellObj = Instantiate(baseInventoryCellPrefab, requiredResourcesGridContainer.transform);
                BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();

                if (cell != null)
                {
                    cell.SetResource(cost.resourceType, cost.amount);
                }
                else
                {
                    InventoryCell inventoryCell = cellObj.GetComponent<InventoryCell>();
                    inventoryCell.SetResource(cost.resourceType, cost.amount);
                    inventoryCell.GetComponent<Button>().interactable = false;
                }
            }
        }

        foreach (Transform child in requiredResourcesGridContainer.transform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
        }

        RectTransform containerRect = requiredResourcesGridContainer.GetComponent<RectTransform>();
        if (containerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private void ClearRequiredResources()
    {
        if (requiredResourcesGridContainer == null)
        {
            return;
        }

        foreach (Transform child in requiredResourcesGridContainer.transform)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform child in requiredResourcesGridContainer.transform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
        }

        RectTransform containerRect = requiredResourcesGridContainer.GetComponent<RectTransform>();
        if (containerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private void DisplayQuestRewards(QuestData questData)
    {
        if (rewardGridContainer == null || baseInventoryCellPrefab == null || moduleInventoryCellPrefab == null)
        {
            return;
        }

        foreach (Transform child in rewardGridContainer.transform)
        {
            Destroy(child.gameObject);
        }

        if (questData.questFinishReward != null)
        {
            if (questData.questFinishReward.resourceRewards != null && questData.questFinishReward.resourceRewards.Length > 0)
            {
                foreach (ResourceCost reward in questData.questFinishReward.resourceRewards)
                {
                    GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                    BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                    if (cell != null)
                    {
                        cell.SetResource(reward.resourceType, reward.amount);
                    }
                }
            }

            if (questData.questFinishReward.moduleRewards != null && questData.questFinishReward.moduleRewards.Length > 0)
            {
                foreach (ModuleRecipe moduleRecipe in questData.questFinishReward.moduleRewards)
                {
                    GameObject cellObj = Instantiate(moduleInventoryCellPrefab, rewardGridContainer.transform);
                    ModuleInventoryCell cell = cellObj.GetComponent<ModuleInventoryCell>();
                    if (cell != null)
                    {
                        Module module = new Module(moduleRecipe);
                        cell.SetModule(module);
                    }
                }
            }

            if (questData.questFinishReward.creditReward > 0)
            {
                if (creditRewardCellPrefab != null)
                {
                    GameObject cellObj = Instantiate(creditRewardCellPrefab, rewardGridContainer.transform);
                    CreditRewardCell cell = cellObj.GetComponent<CreditRewardCell>();
                    if (cell != null)
                    {
                        cell.SetCreditAmount(questData.questFinishReward.creditReward);
                    }
                }
                else
                {
                    GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                    BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                    if (cell != null)
                    {
                        TMP_Text creditText = cellObj.GetComponentInChildren<TMP_Text>();
                        if (creditText != null)
                        {
                            creditText.text = $"{questData.questFinishReward.creditReward} Credits";
                        }
                    }
                }
            }

            if (questData.questFinishReward.unlockedBuildings != null && questData.questFinishReward.unlockedBuildings.Length > 0)
            {
                foreach (BuildingData building in questData.questFinishReward.unlockedBuildings)
                {
                    if (building == null)
                    {
                        continue;
                    }

                    if (buildingRewardCellPrefab != null)
                    {
                        GameObject cellObj = Instantiate(buildingRewardCellPrefab, rewardGridContainer.transform);
                        BuildingRewardCell cell = cellObj.GetComponent<BuildingRewardCell>();
                        if (cell != null)
                        {
                            cell.SetBuildingData(building);
                        }
                    }
                    else
                    {
                        GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                        BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                        if (cell != null)
                        {
                            Image iconImage = cellObj.GetComponentInChildren<Image>();
                            if (iconImage != null && building.icon != null)
                            {
                                iconImage.sprite = building.icon;
                            }

                            TMP_Text nameText = cellObj.GetComponentInChildren<TMP_Text>();
                            if (nameText != null)
                            {
                                nameText.text = building.displayName;
                            }
                        }
                    }
                }
            }

            if (questData.questFinishReward.newUnits != null && questData.questFinishReward.newUnits.Length > 0)
            {
                foreach (UnitData unit in questData.questFinishReward.newUnits)
                {
                    if (unit == null)
                    {
                        continue;
                    }

                    if (unitRewardCellPrefab != null)
                    {
                        GameObject cellObj = Instantiate(unitRewardCellPrefab, rewardGridContainer.transform);
                        UnitRewardCell cell = cellObj.GetComponent<UnitRewardCell>();
                        if (cell != null)
                        {
                            cell.SetUnitData(unit);
                        }
                    }
                    else
                    {
                        GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                        BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                        if (cell != null)
                        {
                            Image iconImage = cellObj.GetComponentInChildren<Image>();
                            if (iconImage != null && unit.unitIcon != null)
                            {
                                iconImage.sprite = unit.unitIcon;
                            }

                            TMP_Text nameText = cellObj.GetComponentInChildren<TMP_Text>();
                            if (nameText != null)
                            {
                                nameText.text = $"{unit.unitName} (NEW)";
                            }
                        }
                    }
                }
            }

            foreach (Transform child in rewardGridContainer.transform)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
            }

            RectTransform containerRect = rewardGridContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            }
        }
    }

    private void ClearQuestRewards()
    {
        if (rewardGridContainer == null)
        {
            return;
        }

        foreach (Transform child in rewardGridContainer.transform)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform child in rewardGridContainer.transform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
        }

        RectTransform containerRect = rewardGridContainer.GetComponent<RectTransform>();
        if (containerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private void OnAcceptButtonClicked()
    {
        if (_currentQuestId == -1 || QuestDataManager.Instance == null)
        {
            return;
        }

        QuestData questData = QuestDataManager.Instance.GetQuestData(_currentQuestId);
        if (questData == null || questData.questType != QuestType.RequestQuest)
        {
            return;
        }

        QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);
        if (questState != QuestState.Available)
        {
            return;
        }

        GameSceneQuestUIManager questUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
        if (questUIManager != null)
        {
            questUIManager.OnRequestQuestAccepted(_currentQuestId);
        }

        if (gameObject.activeSelf)
        {
            ClearQuestInfo();
            gameObject.SetActive(false);
        }
    }

    private void OnRejectButtonClicked()
    {
        if (_currentQuestId == -1 || QuestDataManager.Instance == null)
        {
            return;
        }

        QuestData questData = QuestDataManager.Instance.GetQuestData(_currentQuestId);
        if (questData == null || questData.questType != QuestType.RequestQuest)
        {
            return;
        }

        QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);
        if (questState != QuestState.Available)
        {
            return;
        }

        GameSceneQuestUIManager questUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
        if (questUIManager != null)
        {
            questUIManager.OnRequestQuestRejected(_currentQuestId);
        }

        if (gameObject.activeSelf)
        {
            ClearQuestInfo();
            gameObject.SetActive(false);
        }
    }
}

