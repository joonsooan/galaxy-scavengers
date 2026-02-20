using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CoreRepairManager : MonoBehaviour
{
    public static CoreRepairManager Instance { get; private set; }

    [Header("Core Part Data")]
    [SerializeField] private List<CorePartData> corePartDataList = new List<CorePartData>();

    [Header("Landing Settings")]
    [SerializeField] private bool alwaysDamageEngine = true;
    [SerializeField] private int randomDamagedPartsCount = 2;

    [Header("Debuff UI")]
    [SerializeField] private Color debuffEffectColor = new Color(1f, 0.4f, 0.4f);

    private readonly Dictionary<CorePart, bool> _partRepairStatus = new Dictionary<CorePart, bool>();
    private readonly Dictionary<CorePart, CorePartData> _partDataDict = new Dictionary<CorePart, CorePartData>();
    private readonly Dictionary<CorePart, int> _partQuestIds = new Dictionary<CorePart, int>();
    private int _nextQuestId = 10000;

    public event Action<CorePart> OnPartRepaired;
    public event Action OnRepairStatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializePartData();
    }

    private void InitializePartData()
    {
        foreach (CorePart part in Enum.GetValues(typeof(CorePart)))
        {
            _partRepairStatus[part] = true;
        }

        foreach (CorePartData data in corePartDataList)
        {
            if (data != null)
            {
                _partDataDict[data.partType] = data;
            }
        }
    }

    public void ResetCoreRepairQuests()
    {
        if (QuestDataManager.Instance != null)
        {
            List<QuestData> allCoreRepairQuests = QuestDataManager.Instance.GetAllQuests()
                .Where(q => q != null && q.questType == QuestType.CoreRepairQuest)
                .ToList();
            
            foreach (QuestData quest in allCoreRepairQuests)
            {
                QuestDataManager.Instance.UnregisterRuntimeQuest(quest.questId);
            }
        }
        _partQuestIds.Clear();
    }

    public void InitializeLanding()
    {
        ResetCoreRepairQuests();
        _nextQuestId = 10000;

        foreach (CorePart part in Enum.GetValues(typeof(CorePart)))
        {
            _partRepairStatus[part] = true;
        }

        List<CorePart> partsToDamage = new List<CorePart>();

        if (alwaysDamageEngine)
        {
            partsToDamage.Add(CorePart.Engine);
        }

        List<CorePart> otherParts = Enum.GetValues(typeof(CorePart))
            .Cast<CorePart>()
            .Where(p => p != CorePart.Engine)
            .ToList();

        for (int i = 0; i < randomDamagedPartsCount && otherParts.Count > 0; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, otherParts.Count);
            partsToDamage.Add(otherParts[randomIndex]);
            otherParts.RemoveAt(randomIndex);
        }

        foreach (CorePart part in partsToDamage)
        {
            _partRepairStatus[part] = false;
            CreateRepairQuest(part);
        }

        ApplyDebuffs();
        OnRepairStatusChanged?.Invoke();
    }

    private void CreateRepairQuest(CorePart part)
    {
        CorePartData partData = GetPartData(part);
        if (partData == null) return;

        QuestData questData = ScriptableObject.CreateInstance<QuestData>();
        questData.questId = _nextQuestId++;
        questData.questType = QuestType.CoreRepairQuest;
        questData.questName = $"[{partData.partName}] 수리";
        questData.questInfo = partData.partDescription;
        questData.requiredResources = partData.requiredResources != null ? 
            (ResourceCost[])partData.requiredResources.Clone() : new ResourceCost[0];
        questData.questFinishReward = partData.repairReward;
        questData.previousQuestIds = new int[0];

        _partQuestIds[part] = questData.questId;

        if (QuestDataManager.Instance != null)
        {
            QuestData existingQuest = QuestDataManager.Instance.GetQuestData(questData.questId);
            bool questExists = existingQuest != null;
            
            QuestDataManager.Instance.RegisterRuntimeQuest(questData);
            
            if (questExists && existingQuest.questType == QuestType.CoreRepairQuest)
            {
                QuestDataManager.Instance.ResetCoreRepairQuestState(questData.questId);
            }
            else
            {
                QuestDataManager.Instance.StartQuest(questData.questId);
            }
        }
    }

    public bool IsPartRepaired(CorePart part)
    {
        return _partRepairStatus.ContainsKey(part) && _partRepairStatus[part];
    }

    public CorePartData GetPartData(CorePart part)
    {
        return _partDataDict.ContainsKey(part) ? _partDataDict[part] : null;
    }

    public List<CorePart> GetDamagedParts()
    {
        return _partRepairStatus
            .Where(kvp => !kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
    }
    
    public CorePart GetCorePartFromQuestId(int questId)
    {
        foreach (KeyValuePair<CorePart, int> kvp in _partQuestIds)
        {
            if (kvp.Value == questId)
            {
                return kvp.Key;
            }
        }
        return CorePart.Engine;
    }

    public bool TryRepairPart(CorePart part)
    {
        return TryRepairPart(part, true);
    }
    
    public bool TryRepairPart(CorePart part, bool checkResources)
    {
        if (IsPartRepaired(part)) return false;

        CorePartData partData = GetPartData(part);
        if (partData == null) return false;

        if (checkResources)
        {
            BaseInventoryManager inventoryManager = FindFirstObjectByType<BaseInventoryManager>();
            if (inventoryManager == null) return false;

            if (partData.requiredResources != null && partData.requiredResources.Length > 0)
            {
                foreach (ResourceCost cost in partData.requiredResources)
                {
                    int availableAmount = inventoryManager.GetResourceAmount(cost.resourceType);
                    if (availableAmount < cost.amount)
                    {
                        return false;
                    }
                }

                foreach (ResourceCost cost in partData.requiredResources)
                {
                    inventoryManager.RemoveResource(cost.resourceType, cost.amount);
                }
            }
        }

        _partRepairStatus[part] = true;
        
        if (_partQuestIds.ContainsKey(part) && QuestDataManager.Instance != null)
        {
            int questId = _partQuestIds[part];
            QuestDataManager.Instance.CompleteQuest(questId);
            QuestDataManager.Instance.FinishQuest(questId);
        }

        ApplyDebuffs();
        OnPartRepaired?.Invoke(part);
        OnRepairStatusChanged?.Invoke();
        return true;
    }

    private void ApplyDebuffs()
    {
        ApplyStorageDebuff();
        ApplyPopulationDebuff();
        ApplyProductionDebuff();
        ApplyBarrierNoise();
    }

    public void ApplyDebuffsImmediately()
    {
        ApplyDebuffs();
    }

    private void ApplyStorageDebuff()
    {
        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        if (mainStructure == null) return;

        InventorySystem inventorySystem = mainStructure.GetComponent<InventorySystem>();
        if (inventorySystem == null) return;

        bool isStorageDamaged = !IsPartRepaired(CorePart.Storage);
        CorePartData storageData = GetPartData(CorePart.Storage);

        if (isStorageDamaged && storageData != null)
        {
            float reductionFactor = 1f - storageData.debuffValue;
            int totalCells = inventorySystem.GetTotalCellCount();
            int maxUsableCells = Mathf.Max(1, Mathf.RoundToInt(totalCells * reductionFactor));
            inventorySystem.SetMaxUsableCells(maxUsableCells);
        }
        else
        {
            int totalCells = inventorySystem.GetTotalCellCount();
            inventorySystem.SetMaxUsableCells(totalCells);
        }
    }

    private void ApplyPopulationDebuff()
    {
        if (!IsPartRepaired(CorePart.Controller))
        {
            CorePartData controllerData = GetPartData(CorePart.Controller);
            if (controllerData != null && UnitManager.Instance != null)
            {
            }
        }
    }

    private void ApplyProductionDebuff()
    {
        if (!IsPartRepaired(CorePart.Repeater))
        {
            CorePartData repeaterData = GetPartData(CorePart.Repeater);
            if (repeaterData != null)
            {
            }
        }
    }

    private void ApplyBarrierNoise()
    {
        if (NoiseManager.Instance == null) return;

        if (!IsPartRepaired(CorePart.Barrier))
        {
            CorePartData barrierData = GetPartData(CorePart.Barrier);
            if (barrierData != null)
            {
                NoiseManager.Instance.SetBarrierNoiseCoefficient(barrierData.debuffValue * 100f);
            }
        }
        else
        {
            NoiseManager.Instance.SetBarrierNoiseCoefficient(0f);
        }
    }

    public string GetDebuffDescription(CorePart part)
    {
        if (IsPartRepaired(part)) return string.Empty;

        CorePartData partData = GetPartData(part);
        string partLabel = partData != null && !string.IsNullOrEmpty(partData.partName) ? partData.partName : part.ToString();
        string debuffEffect;

        if (part == CorePart.Engine)
        {
            debuffEffect = $"시드 코어 발사를 위해 수리 필수";
        }
        else
        {
            float debuffPercent = partData != null ? Mathf.RoundToInt(partData.debuffValue * 100f) : 0f;
            debuffEffect = part switch
            {
                CorePart.Barrier => $"소음 정도 {debuffPercent}% 증가",
                CorePart.Controller => $"최대 유닛 제한 {debuffPercent}% 감소",
                CorePart.Repeater => $"건물 생산 속도, 유닛 작업 속도 {debuffPercent}% 감소",
                CorePart.Storage => $"시드 코어 저장 공간 {debuffPercent}% 감소",
                _ => string.Empty
            };
        }

        if (string.IsNullOrEmpty(debuffEffect)) return string.Empty;

        string coloredEffect = $"<color=#{ColorUtility.ToHtmlStringRGB(debuffEffectColor)}>{debuffEffect}</color>";
        string partDesc = partData != null && !string.IsNullOrEmpty(partData.partDescription) ? partData.partDescription : string.Empty;
        return string.IsNullOrEmpty(partDesc) ? coloredEffect : $"{partData.partName}\n\n{partDesc}\n\n{coloredEffect}";
    }
}
