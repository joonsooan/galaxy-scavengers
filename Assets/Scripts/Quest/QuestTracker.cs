using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestTracker : MonoBehaviour
{
    private static QuestTracker _instance;
    
    // Tracking state for each active quest
    private readonly Dictionary<int, Dictionary<QuestCheckData, int>> _questProgress = new ();
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        SubscribeToEvents();
        LoadQuestCheckProgress();
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveQuestCheckProgress();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveQuestCheckProgress();
        }
    }
    
    private void OnDestroy()
    {
        SaveQuestCheckProgress();
        UnsubscribeFromEvents();
    }
    
    private void SubscribeToEvents()
    {
        BuildingManager.OnBuildingConstructed += OnBuildingConstructed;
        DroneHub.OnUnitProduced += OnUnitProduced;
        BeaconManager.OnBeaconPlacedForScout += OnBeaconPlacedForScout;
        Unit_Scout.OnScoutEnteredLocation += OnScoutEnteredLocation;
        CoreCustomizationManager.OnModulePlacedOnCore += OnModulePlacedOnCore;
        ResourceTransferManager.OnResourceTransferCompleted += OnResourceTransferCompleted;
        
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged += OnQuestStateChanged;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        BuildingManager.OnBuildingConstructed -= OnBuildingConstructed;
        DroneHub.OnUnitProduced -= OnUnitProduced;
        BeaconManager.OnBeaconPlacedForScout -= OnBeaconPlacedForScout;
        Unit_Scout.OnScoutEnteredLocation -= OnScoutEnteredLocation;
        CoreCustomizationManager.OnModulePlacedOnCore -= OnModulePlacedOnCore;
        ResourceTransferManager.OnResourceTransferCompleted -= OnResourceTransferCompleted;
        
        if (QuestDataManager.Instance != null)
        {
            QuestDataManager.Instance.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }
    
    private void OnQuestStateChanged(int questId)
    {
        if (QuestDataManager.Instance == null) return;
        
        QuestState state = QuestDataManager.Instance.GetQuestState(questId);
        
        // Initialize tracking for Active and Completable quests
        if (state == QuestState.Active || state == QuestState.Completable)
        {
            InitializeQuestTracking(questId);
        }
        else
        {
            // Clear tracking for all other states
            ClearQuestTracking(questId);
        }
    }
    
    private void InitializeQuestTracking(int questId)
    {
        if (QuestDataManager.Instance == null) return;
        
        QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
        if (quest == null || quest.questCheckRequirements == null) return;
        
        if (!_questProgress.ContainsKey(questId))
        {
            _questProgress[questId] = new Dictionary<QuestCheckData, int>();
        }
        
        foreach (QuestCheckData checkData in quest.questCheckRequirements)
        {
            if (!_questProgress[questId].ContainsKey(checkData))
            {
                _questProgress[questId][checkData] = 0;
            }
        }
    }
    
    private void ClearQuestTracking(int questId)
    {
        _questProgress.Remove(questId);
    }
    
    private void OnBuildingConstructed(BuildingData buildingData)
    {
        if (buildingData == null) return;
        
        CheckAllActiveQuests(checkData =>
        {
            if (checkData.checkType == QuestCheckType.BuildingConstructed)
            {
                if (string.IsNullOrEmpty(checkData.targetId) || 
                    buildingData.displayName.Equals(checkData.targetId, StringComparison.OrdinalIgnoreCase) ||
                    buildingData.name.Equals(checkData.targetId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        });
    }
    
    private void OnUnitProduced(UnitData unitData)
    {
        if (unitData == null) return;
        
        CheckAllActiveQuests(checkData =>
        {
            if (checkData.checkType == QuestCheckType.UnitProduced)
            {
                if (string.IsNullOrEmpty(checkData.targetId) || 
                    unitData.unitName.Equals(checkData.targetId, StringComparison.OrdinalIgnoreCase) ||
                    unitData.name.Equals(checkData.targetId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        });
    }
    
    private void OnBeaconPlacedForScout(Beacon beacon)
    {
        if (beacon == null) return;
        
        CheckAllActiveQuests(checkData =>
        {
            return checkData.checkType == QuestCheckType.BeaconPlacedForScout;
        });
    }
    
    private void OnScoutEnteredLocation(Vector3 location)
    {
        CheckAllActiveQuests(checkData =>
        {
            if (checkData.checkType == QuestCheckType.ScoutEnteredLocation)
            {
                float distance = Vector3.Distance(location, checkData.targetLocation);
                return distance <= checkData.locationTolerance;
            }
            return false;
        });
    }
    
    private void OnModulePlacedOnCore(Module module, int slotIndex)
    {
        if (module == null) return;
        
        CheckAllActiveQuests(checkData =>
        {
            if (checkData.checkType == QuestCheckType.ModulePlacedOnCore)
            {
                if (string.IsNullOrEmpty(checkData.targetId))
                {
                    return true; // Any module placement counts
                }
                
                return module.moduleName.Equals(checkData.targetId, StringComparison.OrdinalIgnoreCase) ||
                       module.moduleId.Equals(checkData.targetId, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        });
    }
    
    private void OnResourceTransferCompleted()
    {
        // Check module placement on core when resource transfer completes
        if (CoreCustomizationManager.Instance != null)
        {
            List<Module> activeModules = CoreCustomizationManager.Instance.GetActiveModules();
            if (activeModules.Count > 0)
            {
                // This will be handled by OnModulePlacedOnCore event, but we can also check here
                CheckAllActiveQuests(checkData =>
                {
                    if (checkData.checkType == QuestCheckType.ModulePlacedOnCore && 
                        !checkData.isCompleted)
                    {
                        // Check if any module is placed on core
                        return activeModules.Count > 0;
                    }
                    return false;
                });
            }
        }
    }
    
    private void CheckAllActiveQuests(Func<QuestCheckData, bool> checkMatch)
    {
        if (QuestDataManager.Instance == null) return;
        
        HashSet<int> activeQuestIds = QuestDataManager.Instance.GetActiveQuestIds();
        
        foreach (int questId in activeQuestIds)
        {
            QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
            if (quest == null || quest.questCheckRequirements == null) continue;
            
            foreach (QuestCheckData checkData in quest.questCheckRequirements)
            {
                if (checkMatch(checkData))
                {
                    UpdateQuestProgress(questId, checkData);
                    QuestDataManager.Instance.CheckQuestCompletion(questId);
                }
            }
        }
    }
    
    private void UpdateQuestProgress(int questId, QuestCheckData checkData)
    {
        if (!_questProgress.ContainsKey(questId))
        {
            _questProgress[questId] = new Dictionary<QuestCheckData, int>();
        }
        
        if (!_questProgress[questId].ContainsKey(checkData))
        {
            _questProgress[questId][checkData] = 0;
        }
        
        _questProgress[questId][checkData]++;
        checkData.currentCount = _questProgress[questId][checkData];
        
        if (checkData.IsRequirementMet())
        {
            checkData.isCompleted = true;
        }
        
        SaveQuestCheckProgress();
    }
    
    public int GetQuestProgress(int questId, QuestCheckData checkData)
    {
        if (_questProgress.ContainsKey(questId) && _questProgress[questId].ContainsKey(checkData))
        {
            return _questProgress[questId][checkData];
        }
        return 0;
    }
    
    public void ResetAllQuestProgress()
    {
        _questProgress.Clear();
        
        ClearSavedQuestCheckProgress();
        
        // Reset all quest check data progress
        if (QuestDataManager.Instance != null)
        {
            foreach (QuestData quest in QuestDataManager.Instance.GetAllQuests())
            {
                if (quest == null || quest.questCheckRequirements == null) continue;
                
                foreach (QuestCheckData checkData in quest.questCheckRequirements)
                {
                    checkData.ResetProgress();
                }
            }
        }
        
        Debug.Log("QuestTracker: All quest tracking progress has been reset.");
    }

    public void ResetQuestProgressForQuest(int questId)
    {
        _questProgress.Remove(questId);

        if (QuestDataManager.Instance != null)
        {
            QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
            if (quest != null && quest.questCheckRequirements != null)
            {
                foreach (QuestCheckData checkData in quest.questCheckRequirements)
                {
                    if (checkData == null) continue;
                    checkData.ResetProgress();
                }
            }
        }

        ClearSavedQuestCheckProgressForQuest(questId);
    }
    
    private void SaveQuestCheckProgress()
    {
        if (QuestDataManager.Instance == null) return;
        
        foreach (var questKvp in _questProgress)
        {
            int questId = questKvp.Key;
            QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
            if (quest == null || quest.questCheckRequirements == null) continue;
            
            for (int i = 0; i < quest.questCheckRequirements.Length; i++)
            {
                QuestCheckData checkData = quest.questCheckRequirements[i];
                if (checkData == null) continue;
                
                string progressKey = $"QuestCheckProgress_{questId}_{i}";
                string completedKey = $"QuestCheckCompleted_{questId}_{i}";
                
                if (_questProgress[questId].ContainsKey(checkData))
                {
                    PlayerPrefs.SetInt(progressKey, _questProgress[questId][checkData]);
                    PlayerPrefs.SetInt(completedKey, checkData.isCompleted ? 1 : 0);
                }
            }
        }
        
        PlayerPrefs.Save();
    }
    
    private void LoadQuestCheckProgress()
    {
        if (QuestDataManager.Instance == null) return;
        
        foreach (QuestData quest in QuestDataManager.Instance.GetAllQuests())
        {
            if (quest == null || quest.questCheckRequirements == null) continue;
            
            int questId = quest.questId;
            
            QuestState state = QuestDataManager.Instance.GetQuestState(questId);
            if (state != QuestState.Active && state != QuestState.Completable) continue;
            
            if (!_questProgress.ContainsKey(questId))
            {
                _questProgress[questId] = new Dictionary<QuestCheckData, int>();
            }
            
            for (int i = 0; i < quest.questCheckRequirements.Length; i++)
            {
                QuestCheckData checkData = quest.questCheckRequirements[i];
                if (checkData == null) continue;
                
                string progressKey = $"QuestCheckProgress_{questId}_{i}";
                string completedKey = $"QuestCheckCompleted_{questId}_{i}";
                
                if (PlayerPrefs.HasKey(progressKey))
                {
                    int savedCount = PlayerPrefs.GetInt(progressKey);
                    _questProgress[questId][checkData] = savedCount;
                    checkData.currentCount = savedCount;
                }
                
                if (PlayerPrefs.HasKey(completedKey))
                {
                    int savedCompleted = PlayerPrefs.GetInt(completedKey);
                    checkData.isCompleted = savedCompleted == 1;
                }
            }
        }
    }
    
    private void ClearSavedQuestCheckProgress()
    {
        if (QuestDataManager.Instance == null) return;
        
        foreach (QuestData quest in QuestDataManager.Instance.GetAllQuests())
        {
            if (quest == null || quest.questCheckRequirements == null) continue;
            
            int questId = quest.questId;
            
            for (int i = 0; i < quest.questCheckRequirements.Length; i++)
            {
                string progressKey = $"QuestCheckProgress_{questId}_{i}";
                string completedKey = $"QuestCheckCompleted_{questId}_{i}";
                
                if (PlayerPrefs.HasKey(progressKey))
                {
                    PlayerPrefs.DeleteKey(progressKey);
                }
                
                if (PlayerPrefs.HasKey(completedKey))
                {
                    PlayerPrefs.DeleteKey(completedKey);
                }
            }
        }
        
        PlayerPrefs.Save();
    }

    private void ClearSavedQuestCheckProgressForQuest(int questId)
    {
        if (QuestDataManager.Instance == null) return;

        QuestData quest = QuestDataManager.Instance.GetQuestData(questId);
        if (quest == null || quest.questCheckRequirements == null) return;

        for (int i = 0; i < quest.questCheckRequirements.Length; i++)
        {
            string progressKey = $"QuestCheckProgress_{questId}_{i}";
            string completedKey = $"QuestCheckCompleted_{questId}_{i}";

            if (PlayerPrefs.HasKey(progressKey))
            {
                PlayerPrefs.DeleteKey(progressKey);
            }

            if (PlayerPrefs.HasKey(completedKey))
            {
                PlayerPrefs.DeleteKey(completedKey);
            }
        }

        PlayerPrefs.Save();
    }
}
