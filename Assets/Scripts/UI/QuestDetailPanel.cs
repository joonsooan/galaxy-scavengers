using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestDetailPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questIdText;
    [SerializeField] private TMP_Text questDescriptionText;
    [SerializeField] private TMP_Text requiredResourceText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private GameObject requiredResourcesGridContainer;
    [SerializeField] private GameObject rewardGridContainer;
    [SerializeField] private GameObject baseInventoryCellPrefab;
    [SerializeField] private GameObject moduleInventoryCellPrefab;
    [SerializeField] private Button questActionButton;
    [SerializeField] private TMP_Text questActionButtonText;

    private int _currentQuestId = -1;

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
    }

    private void OnDisable()
    {
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
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
        if (Input.GetMouseButtonUp(1))
        {
            if (gameObject.activeSelf && _currentQuestId != -1)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsDragging())
                {
                    return;
                }
                
                ClearQuestInfo();
                return;
            }
        }
        
        if (_currentQuestId != -1 && QuestDataManager.Instance != null)
        {
            QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);
            if (questState == QuestState.Active)
            {
                bool canFinish = QuestDataManager.Instance.CheckQuestCompletion(_currentQuestId);
                bool shouldShowButton = canFinish;
                bool isButtonVisible = questActionButton != null && questActionButton.gameObject.activeSelf;
                
                if (shouldShowButton != isButtonVisible)
                {
                    UpdateButtonVisibility();
                }
            }
        }
    }

    public void DisplayQuestInfo(QuestData questData, int questId)
    {
        if (questData == null) return;

        if (_currentQuestId == questId)
        {
            return;
        }

        _currentQuestId = questId;
        questNameText.text = questData.questName;
        questIdText.text = $"퀘스트 ID : {questId:D4}";
        questDescriptionText.text = questData.questInfo;
        requiredResourceText.text = "필요한 자원";
        rewardText.text = "보상";

        DisplayRequiredResources(questData);
        DisplayQuestRewards(questData);
        UpdateButtonVisibility();
        
        RebuildAllLayouts();
    }
    
    public void ClearQuestInfo()
    {
        _currentQuestId = -1;
        questNameText.text = "";
        questIdText.text = "";
        questDescriptionText.text = "";
        requiredResourceText.text = "";
        rewardText.text = "";

        ClearRequiredResources();
        ClearQuestRewards();
        UpdateButtonVisibility();
        
        RebuildAllLayouts();
    }

    private void DisplayRequiredResources(QuestData questData)
    {
        if (requiredResourcesGridContainer == null || baseInventoryCellPrefab == null) return;

        foreach (Transform child in requiredResourcesGridContainer.transform)
        {
            Destroy(child.gameObject);
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
    }

    private void ClearRequiredResources()
    {
        foreach (Transform child in requiredResourcesGridContainer.transform)
        {
            Destroy(child.gameObject);
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
    
    private void DisplayQuestRewards(QuestData questData)
    {
        if (rewardGridContainer == null || baseInventoryCellPrefab == null || moduleInventoryCellPrefab == null) return;

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
                GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                if (cell != null)
                {
                    // Display credit reward as a special resource type or custom text
                    // For now, we'll need to check if there's a way to display custom text
                    // If BaseInventoryCell supports custom display, use it; otherwise create a text element
                    TMP_Text creditText = cellObj.GetComponentInChildren<TMP_Text>();
                    if (creditText != null)
                    {
                        creditText.text = $"{questData.questFinishReward.creditReward} Credits";
                    }
                }
            }
            
            // Display unlocked buildings
            if (questData.questFinishReward.unlockedBuildings != null && questData.questFinishReward.unlockedBuildings.Length > 0)
            {
                foreach (BuildingData building in questData.questFinishReward.unlockedBuildings)
                {
                    if (building == null) continue;
                    
                    GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                    BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                    if (cell != null)
                    {
                        // Try to set building icon if BaseInventoryCell supports it
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
            
            // Display new units
            if (questData.questFinishReward.newUnits != null && questData.questFinishReward.newUnits.Length > 0)
            {
                foreach (UnitData unit in questData.questFinishReward.newUnits)
                {
                    if (unit == null) continue;
                    
                    GameObject cellObj = Instantiate(baseInventoryCellPrefab, rewardGridContainer.transform);
                    BaseInventoryCell cell = cellObj.GetComponent<BaseInventoryCell>();
                    if (cell != null)
                    {
                        // Try to set unit icon if BaseInventoryCell supports it
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
        foreach (Transform child in rewardGridContainer.transform)
        {
            Destroy(child.gameObject);
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

    private void UpdateButtonVisibility()
    {
        if (_currentQuestId == -1 || questActionButton == null)
        {
            if (questActionButton != null) questActionButton.gameObject.SetActive(false);
            return;
        }

        if (QuestDataManager.Instance == null)
        {
            if (questActionButton != null) questActionButton.gameObject.SetActive(false);
            return;
        }

        QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);
        
        if (questState == QuestState.Available)
        {
            questActionButton.gameObject.SetActive(true);
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
                questActionButton.gameObject.SetActive(true);
                if (questActionButtonText != null)
                {
                    questActionButtonText.text = "완료";
                }
            }
            else
            {
                questActionButton.gameObject.SetActive(false);
            }
        }
        else if (questState == QuestState.Completed)
        {
            questActionButton.gameObject.SetActive(true);
            if (questActionButtonText != null)
            {
                questActionButtonText.text = "완료";
            }
        }
        else
        {
            questActionButton.gameObject.SetActive(false);
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
        else if (questState == QuestState.Completed)
        {
            FinishQuestAndGiveRewards(_currentQuestId);
        }
    }
    
    private void FinishQuestAndGiveRewards(int questId)
    {
        QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
        if (quest == null) return;
        
        BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
        if (inventoryManager == null) return;
        
        if (quest.requiredResources != null && quest.requiredResources.Length > 0)
        {
            foreach (ResourceCost cost in quest.requiredResources)
            {
                if (!inventoryManager.RemoveResource(cost.resourceType, cost.amount))
                {
                    Debug.LogWarning($"QuestDetailPanel: Failed to remove {cost.amount} {cost.resourceType} from base inventory when finishing quest {questId}");
                }
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

            if (quest.questFinishReward.moduleRewards != null && quest.questFinishReward.moduleRewards.Length > 0)
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
                else
                {
                    Debug.LogWarning($"QuestDetailPanel: CreditManager.Instance is null. Cannot award {quest.questFinishReward.creditReward} credits for quest {questId}");
                }
            }
            
            // Unlock buildings
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
            
            BaseInventorySystem inventorySystem = FindFirstObjectByType<BaseInventorySystem>();
            inventorySystem.ForceRefreshInventory();
            
            CoreCustomUIManager coreCustomUIManager = FindFirstObjectByType<CoreCustomUIManager>();
            coreCustomUIManager.RefreshModuleSelectionGrid();
        }
        
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.FinishQuest(questId);
        }
        
        ClearQuestInfo();
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

    private IEnumerator RebuildAllLayouts()
    {
        yield return new WaitForEndOfFrame();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
    }
}