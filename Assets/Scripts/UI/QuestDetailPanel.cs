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

    private void Update()
    {
       if (_currentQuestId != -1 && QuestDataManager.Instance != null)
       {
            QuestState questState = QuestDataManager.Instance.GetQuestState(_currentQuestId);
            if (questState == QuestState.Active)
            {
                bool canFinish = QuestDataManager.Instance.CheckQuestCompletion(_currentQuestId);
                if (canFinish && questActionButton != null && !questActionButton.gameObject.activeSelf)
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
                    UpdateButtonVisibility();
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
        if (QuestDataManager.Instance == null) return;
        
        QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
        if (quest == null) return;
        
        if (quest.questFinishReward != null)
        {
            BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            
            if (quest.questFinishReward.resourceRewards != null && quest.questFinishReward.resourceRewards.Length > 0)
            {
                if (inventoryManager != null)
                {
                    foreach (ResourceCost reward in quest.questFinishReward.resourceRewards)
                    {
                        inventoryManager.AddResource(reward.resourceType, reward.amount);
                    }
                }
            }

            if (quest.questFinishReward.moduleRewards != null && quest.questFinishReward.moduleRewards.Length > 0)
            {
                if (inventoryManager != null)
                {
                    foreach (ModuleRecipe moduleRecipe in quest.questFinishReward.moduleRewards)
                    {
                        Module module = new Module(moduleRecipe);
                        inventoryManager.AddModule(module);
                    }
                }
            }
            
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
        
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.FinishQuest(questId);
        }
        
        _currentQuestId = -1;
        gameObject.SetActive(false);
    }

    public void RefreshQuestState(int questId)
    {
        if (_currentQuestId == questId)
        {
            UpdateButtonVisibility();
        }
    }

    private IEnumerator RebuildAllLayouts()
    {
        yield return new WaitForEndOfFrame();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(requiredResourcesGridContainer.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(rewardGridContainer.GetComponent<RectTransform>());
    }
}
