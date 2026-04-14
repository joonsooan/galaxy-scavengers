using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestBriefPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text requiredResourceText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private GameObject requiredResourcesGridContainer;
    [SerializeField] private GameObject rewardGridContainer;
    [SerializeField] private GameObject baseInventoryCellPrefab;
    [SerializeField] private GameObject moduleInventoryCellPrefab;
    [SerializeField] private GameObject creditRewardCellPrefab;
    [SerializeField] private GameObject buildingRewardCellPrefab;
    [SerializeField] private GameObject unitRewardCellPrefab;
    [SerializeField] private GameObject requirementTextPrefab;
    [SerializeField] private Button questActionButton;
    [SerializeField] private TMP_Text questActionButtonText;
    [SerializeField] private GameObject questButtonPanel;

    private int _currentQuestId = -1;
    private bool _isGameSceneMode = false;
    private Coroutine _rebuildCoroutine;

    public void SetGameSceneMode(bool isGameSceneMode)
    {
        _isGameSceneMode = isGameSceneMode;
    }

    private void Awake()
    {
        if (questActionButton != null)
        {
            questActionButton.onClick.RemoveAllListeners();
            questActionButton.onClick.AddListener(OnQuestActionButtonClicked);
        }
    }

    private void OnEnable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.OnResourceChanged += OnInventoryResourceChanged;
        }
    }

    private void OnDisable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.OnResourceChanged -= OnInventoryResourceChanged;
        }
    }

    private void OnInventoryResourceChanged(ResourceType type, int amount)
    {
        if (_currentQuestId != -1)
        {
            UpdateButtonVisibility();
        }
    }

    private void OnQuestStateChanged(int questId)
    {
        if (_currentQuestId == questId)
        {
            UpdateButtonVisibility();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(1) && gameObject.activeSelf && _currentQuestId != -1 && !_isGameSceneMode)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsDragging()) return;
            ClearQuestInfo();
        }
    }

    public void DisplayQuestInfo(QuestData questData, int questId)
    {
        if (questData == null)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        _currentQuestId = questId;

        QuestUIHandler handler = FindFirstObjectByType<QuestUIHandler>();
        if (handler != null)
        {
            handler.OnQuestViewed(questId);
        }
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

        bool showRewardPanel = true;
        if (rewardPanel != null)
        {
            rewardPanel.SetActive(showRewardPanel);
        }
        else
        {
            if (rewardText != null)
            {
                rewardText.gameObject.SetActive(showRewardPanel);
            }
            if (rewardGridContainer != null)
            {
                rewardGridContainer.SetActive(showRewardPanel);
            }
        }

        DisplayRequiredResources(questData);
        if (showRewardPanel)
        {
            DisplayQuestRewards(questData);
        }
        else
        {
            ClearQuestRewards();
        }
        UpdateButtonVisibility();

        RebuildLayoutImmediate();
        if (_rebuildCoroutine != null)
        {
            StopCoroutine(_rebuildCoroutine);
        }
        if (gameObject.activeInHierarchy)
        {
            _rebuildCoroutine = StartCoroutine(RebuildAllLayouts());
        }
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
        UpdateButtonVisibility();

        RebuildLayoutImmediate();
        if (_rebuildCoroutine != null)
        {
            StopCoroutine(_rebuildCoroutine);
        }
        if (gameObject.activeInHierarchy)
        {
            _rebuildCoroutine = StartCoroutine(RebuildAllLayouts());
        }
    }

    private void UpdateRequiredResourceText(QuestData questData)
    {
        if (requiredResourceText == null) return;

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
                    if (checkData.checkType != QuestCheckType.ReturnFromGameSceneSuccess &&
                        checkData.checkType != QuestCheckType.ReturnFromGameSceneFailure)
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
            QuestCheckType.ScoutEnteredLocation => "탐사 요구사항",
            QuestCheckType.ModulePlacedOnCore => "모듈 요구사항",
            QuestCheckType.ReturnFromGameSceneSuccess => "요구사항",
            QuestCheckType.ReturnFromGameSceneFailure => "요구사항",
            _ => "요구사항"
        };
    }

    private void DisplayRequiredResources(QuestData questData)
    {
        if (requiredResourcesGridContainer == null || baseInventoryCellPrefab == null) return;

        while (requiredResourcesGridContainer.transform.childCount > 0)
        {
            DestroyImmediate(requiredResourcesGridContainer.transform.GetChild(0).gameObject);
        }

        if (questData.questCheckRequirements != null && questData.questCheckRequirements.Length > 0)
        {
            foreach (QuestCheckData checkData in questData.questCheckRequirements)
            {
                if (checkData.checkType == QuestCheckType.BuildingConstructed ||
                    checkData.checkType == QuestCheckType.UnitProduced ||
                    checkData.checkType == QuestCheckType.ScoutEnteredLocation ||
                    checkData.checkType == QuestCheckType.ModulePlacedOnCore ||
                    checkData.checkType == QuestCheckType.ReturnFromGameSceneSuccess ||
                    checkData.checkType == QuestCheckType.ReturnFromGameSceneFailure)
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
                            TMP_Text textComponent = textObj.AddComponent<TMPro.TextMeshProUGUI>();
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

        if (requiredResourcesGridContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        }
    }

    private void ClearRequiredResources()
    {
        while (requiredResourcesGridContainer.transform.childCount > 0)
        {
            DestroyImmediate(requiredResourcesGridContainer.transform.GetChild(0).gameObject);
        }

        if (requiredResourcesGridContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        }
    }

    private void DisplayQuestRewards(QuestData questData)
    {
        if (rewardGridContainer == null || baseInventoryCellPrefab == null || moduleInventoryCellPrefab == null) return;

        while (rewardGridContainer.transform.childCount > 0)
        {
            DestroyImmediate(rewardGridContainer.transform.GetChild(0).gameObject);
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
                    if (building == null) continue;

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
                    if (unit == null) continue;

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

            if (rewardGridContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
            }
        }
    }

    private void ClearQuestRewards()
    {
        while (rewardGridContainer.transform.childCount > 0)
        {
            DestroyImmediate(rewardGridContainer.transform.GetChild(0).gameObject);
        }

        if (rewardGridContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
        }
    }

    private void UpdateButtonVisibility()
    {
        if (_currentQuestId == -1)
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(false);
            if (questActionButton != null) questActionButton.interactable = false;
            return;
        }

        if (QuestDataManager.Instance == null)
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(false);
            if (questActionButton != null) questActionButton.interactable = false;
            return;
        }

        QuestData questData = QuestDataManager.Instance.GetQuestData(_currentQuestId);
        QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);

        bool isRequestQuest = questData != null && questData.questType == QuestType.RequestQuest;

        if (isRequestQuest && questState == QuestState.Available)
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(false);
            questActionButton.interactable = false;
            return;
        }

        if (questActionButton == null)
        {
            return;
        }

        FMODUIButton fmodButton = questActionButton.GetComponent<FMODUIButton>();
        if (fmodButton != null)
        {
            if (questState == QuestState.Available)
            {
                fmodButton.SetClickState("Quest Accept");
            }
            else if (questState == QuestState.Completable)
            {
                fmodButton.SetClickState("Quest Clear");
            }
            else
            {
                fmodButton.SetClickState("Default");
            }
        }

        if (questState == QuestState.Available)
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(true);
            questActionButton.interactable = true;
            if (questActionButtonText != null)
            {
                questActionButtonText.text = "수락";
            }
        }
        else if (questState == QuestState.Active)
        {
            bool canFinish = QuestDataManager.Instance.CheckQuestCompletion(_currentQuestId);
            if (canFinish)
            {
                if (questButtonPanel != null) questButtonPanel.SetActive(true);
                questActionButton.interactable = true;
                if (questActionButtonText != null)
                {
                    questActionButtonText.text = "완료";
                }
            }
            else
            {
                if (questButtonPanel != null) questButtonPanel.SetActive(true);
                questActionButton.interactable = false;
                if (questActionButtonText != null)
                {
                    questActionButtonText.text = "진행 중";
                }
            }
        }
        else if (questState == QuestState.Completable)
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(true);
            questActionButton.interactable = true;
            if (questActionButtonText != null)
            {
                questActionButtonText.text = "완료";
            }
        }
        else if (questState == QuestState.Completed)
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(true);
            questActionButton.interactable = true;
            if (questActionButtonText != null)
            {
                questActionButtonText.text = "완료";
            }
        }
        else
        {
            if (questButtonPanel != null) questButtonPanel.SetActive(false);
            questActionButton.interactable = false;
        }
    }

    private void OnQuestActionButtonClicked()
    {
        if (_currentQuestId == -1 || QuestDataManager.Instance == null) return;

        QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);

        if (questState == QuestState.Available)
        {
            if (QuestManager.Instance != null)
            {
                bool success = QuestManager.Instance.StartQuest(_currentQuestId);
                if (success)
                {
                    StartCoroutine(UpdateButtonAfterStateChange());
                }
            }
        }
        else if (questState == QuestState.Active)
        {
            bool canFinish = QuestDataManager.Instance.CheckQuestCompletion(_currentQuestId);
            if (canFinish)
            {
                if (QuestManager.Instance != null)
                {
                    bool completed = QuestManager.Instance.CompleteQuest(_currentQuestId);
                    if (completed)
                    {
                        FinishQuestAndGiveRewards(_currentQuestId);
                    }
                }
            }
        }
        else if (questState == QuestState.Completable)
        {
            if (QuestManager.Instance != null)
            {
                bool completed = QuestManager.Instance.CompleteQuest(_currentQuestId);
                if (completed)
                {
                    FinishQuestAndGiveRewards(_currentQuestId);
                }
            }
        }
        else if (questState == QuestState.Completed)
        {
            FinishQuestAndGiveRewards(_currentQuestId);
        }
    }

    private void FinishQuestAndGiveRewards(int questId)
    {
        QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
        bool isRequestQuest = quest != null && quest.questType == QuestType.RequestQuest;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.FinishQuest(questId);
        }

        if (quest == null)
        {
            return;
        }

        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();

        if (isRequestQuest)
        {
            if (ResourceDataManager.Instance != null && quest.requiredResources != null && quest.requiredResources.Length > 0)
            {
                foreach (ResourceCost cost in quest.requiredResources)
                {
                    ResourceDataManager.Instance.RemoveResource(cost.resourceType, cost.amount);
                }
            }

            if (quest.questFinishReward != null)
            {
                if (quest.questFinishReward.resourceRewards != null && quest.questFinishReward.resourceRewards.Length > 0)
                {
                    foreach (ResourceCost reward in quest.questFinishReward.resourceRewards)
                    {
                        if (ResourceDataManager.Instance != null)
                        {
                            ResourceDataManager.Instance.AddResource(reward.resourceType, reward.amount);
                        }
                    }
                }
            }
        }
        else
        {
            if (inventoryManager == null) return;

            if (quest.requiredResources != null && quest.requiredResources.Length > 0)
            {
                foreach (ResourceCost cost in quest.requiredResources)
                {
                    inventoryManager.RemoveResource(cost.resourceType, cost.amount);
                }
            }

            if (quest.questFinishReward != null)
            {
                if (quest.questFinishReward.resourceRewards != null && quest.questFinishReward.resourceRewards.Length > 0)
                {
                    foreach (ResourceCost reward in quest.questFinishReward.resourceRewards)
                    {
                        inventoryManager.AddResource(reward.resourceType, reward.amount);
                    }
                }
            }
        }

        if (quest.questFinishReward != null)
        {
            if (quest.questFinishReward.moduleRewards != null && quest.questFinishReward.moduleRewards.Length > 0 && inventoryManager != null)
            {
                foreach (ModuleRecipe moduleRecipe in quest.questFinishReward.moduleRewards)
                {
                    Module module = new Module(moduleRecipe);
                    inventoryManager.AddModule(module);
                }
            }

            if (quest.questFinishReward.creditReward > 0)
            {
                if (CreditManager.Instance != null)
                {
                    CreditManager.Instance.AddCredits(quest.questFinishReward.creditReward);
                }
            }

            if (quest.questFinishReward.unlockedBuildings != null && quest.questFinishReward.unlockedBuildings.Length > 0)
            {
                if (BuildingUnlockManager.Instance == null)
                {
                    GameObject unlockManagerObj = new GameObject("BuildingUnlockManager");
                    unlockManagerObj.AddComponent<BuildingUnlockManager>();
                }

                if (BuildingUnlockManager.Instance != null)
                {
                    BuildingUnlockManager.Instance.UnlockBuildings(quest.questFinishReward.unlockedBuildings);
                }
            }

            if (inventoryManager != null)
            {
                BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
                if (inventorySystem != null)
                {
                    inventorySystem.ForceRefreshInventory();
                }

                CoreCustomUIManager coreCustomUIManager = FindFirstObjectByType<CoreCustomUIManager>();
                if (coreCustomUIManager != null)
                {
                    coreCustomUIManager.RefreshModuleSelectionGrid();
                }
            }
        }

        if (isRequestQuest)
        {
            GameSceneQuestUIManager questUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            if (questUIManager != null)
            {
                questUIManager.LoadActiveQuests();
            }
        }

        ClearQuestInfo();

        if (_isGameSceneMode)
        {
            GameSceneQuestUIManager questUIManager = FindFirstObjectByType<GameSceneQuestUIManager>();
            if (questUIManager != null)
                questUIManager.HideQuestDetailPanel();
        }
    }

    public void RefreshQuestState(int questId)
    {
        if (_currentQuestId == questId)
        {
            UpdateButtonVisibility();
        }
    }

    private IEnumerator UpdateButtonAfterStateChange()
    {
        yield return null;
        UpdateButtonVisibility();
    }

    private void RebuildLayoutImmediate()
    {
        Canvas.ForceUpdateCanvases();

        if (questNameText != null)
        {
            questNameText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(questNameText.rectTransform);
        }
        if (requiredResourceText != null)
        {
            requiredResourceText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourceText.rectTransform);
        }
        if (rewardText != null)
        {
            rewardText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardText.rectTransform);
        }

        if (requiredResourcesGridContainer != null)
        {
            foreach (Transform child in requiredResourcesGridContainer.transform)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        }

        if (rewardGridContainer != null)
        {
            foreach (Transform child in rewardGridContainer.transform)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
        }

        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator RebuildAllLayouts()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (questNameText != null)
        {
            questNameText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(questNameText.rectTransform);
        }
        if (requiredResourceText != null)
        {
            requiredResourceText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourceText.rectTransform);
        }
        if (rewardText != null)
        {
            rewardText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardText.rectTransform);
        }

        if (requiredResourcesGridContainer != null)
        {
            foreach (Transform child in requiredResourcesGridContainer.transform)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        }

        if (rewardGridContainer != null)
        {
            foreach (Transform child in rewardGridContainer.transform)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
        }

        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (questNameText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(questNameText.rectTransform);
        if (requiredResourceText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourceText.rectTransform);
        if (rewardText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardText.rectTransform);
        if (requiredResourcesGridContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        if (rewardGridContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect2)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect2);

        _rebuildCoroutine = null;
    }
}
