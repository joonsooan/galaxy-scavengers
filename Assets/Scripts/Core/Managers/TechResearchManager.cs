using System;
using System.Collections.Generic;
using UnityEngine;

public enum TechResearchState
{
    Locked,
    Available,
    InProgress,
    Completed
}

public class TechResearchManager : MonoBehaviour
{
    public static TechResearchManager Instance { get; private set; }

    [SerializeField] private TechResearchCatalog catalog;
    [SerializeField] private float researchTickInterval = 1f;

    private readonly Dictionary<int, TechResearchState> _techStates = new Dictionary<int, TechResearchState>();
    private int _currentResearchIndex = -1;
    private int _currentProgress;
    private float _tickTimer;

    public static event Action<int> OnResearchStarted;
    public static event Action<int> OnResearchCompleted;
    public static event Action OnResearchProgressChanged;
    public static event Action OnResearchStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeTechStates();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (catalog == null)
        {
            return;
        }

        IReadOnlyList<TechData> techs = catalog.Techs;
        if (techs == null)
        {
            return;
        }

        for (int i = 0; i < techs.Count; i++)
        {
            TechData tech = techs[i];
            if (tech == null || !tech.isUnlockedByDefault)
            {
                continue;
            }

            ApplyTechUnlockEffects(tech);
            UnlockSuccessors(tech.techIndex);
        }
    }

    private void InitializeTechStates()
    {
        _techStates.Clear();

        if (catalog == null)
        {
            return;
        }

        IReadOnlyList<TechData> techs = catalog.Techs;
        if (techs == null)
        {
            return;
        }

        for (int i = 0; i < techs.Count; i++)
        {
            TechData tech = techs[i];
            if (tech == null)
            {
                continue;
            }

            if (tech.isUnlockedByDefault)
            {
                _techStates[tech.techIndex] = TechResearchState.Completed;
            }
            else
            {
                bool hasPrerequisites = tech.prerequisiteTechIndices != null && tech.prerequisiteTechIndices.Length > 0;
                _techStates[tech.techIndex] = hasPrerequisites ? TechResearchState.Locked : TechResearchState.Available;
            }
        }
    }

    private void Update()
    {
        if (_currentResearchIndex < 0)
        {
            return;
        }

        _tickTimer += Time.deltaTime;

        if (_tickTimer >= researchTickInterval)
        {
            _tickTimer -= researchTickInterval;
            _currentProgress++;
            OnResearchProgressChanged?.Invoke();

            if (_currentProgress >= GetMaxProgress())
            {
                CompleteResearch();
            }
        }
    }

    public TechData GetTechData(int techIndex)
    {
        if (catalog == null)
        {
            return null;
        }

        return catalog.GetTechByIndex(techIndex);
    }

    public TechResearchState GetTechState(int techIndex)
    {
        _techStates.TryGetValue(techIndex, out TechResearchState state);
        return state;
    }

    public bool StartResearch(int techIndex)
    {
        TechResearchState state = GetTechState(techIndex);
        if (state != TechResearchState.Available)
        {
            return false;
        }

        TechData techData = GetTechData(techIndex);
        if (techData == null)
        {
            return false;
        }

        if (ResourceDataManager.Instance == null)
        {
            return false;
        }

        if (!ResourceDataManager.Instance.SpendResources(techData.researchCosts))
        {
            return false;
        }

        _currentResearchIndex = techIndex;
        _currentProgress = 0;
        _techStates[techIndex] = TechResearchState.InProgress;

        OnResearchStarted?.Invoke(techIndex);
        OnResearchStateChanged?.Invoke();

        return true;
    }

    public void CancelResearch()
    {
        if (_currentResearchIndex < 0)
        {
            return;
        }

        TechData techData = GetTechData(_currentResearchIndex);
        if (techData != null && techData.researchDuration > 0 && ResourceDataManager.Instance != null)
        {
            float remainingRatio = 1f - Mathf.Clamp01((float)_currentProgress / techData.researchDuration);
            RefundResearchCosts(techData.researchCosts, remainingRatio);
        }

        _techStates[_currentResearchIndex] = TechResearchState.Available;
        _currentResearchIndex = -1;
        _currentProgress = 0;
        _tickTimer = 0f;

        OnResearchStateChanged?.Invoke();
    }

    private void RefundResearchCosts(ResourceCost[] costs, float ratio)
    {
        if (costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null)
            {
                continue;
            }

            int refundAmount = Mathf.FloorToInt(cost.amount * ratio);
            if (refundAmount > 0)
            {
                ResourceDataManager.Instance.AddResource(cost.resourceType, refundAmount);
            }
        }
    }

    public TechData GetCurrentResearch()
    {
        if (_currentResearchIndex >= 0)
        {
            return GetTechData(_currentResearchIndex);
        }

        return null;
    }

    public int GetCurrentProgress()
    {
        return _currentProgress;
    }

    public int GetMaxProgress()
    {
        if (_currentResearchIndex < 0)
        {
            return 0;
        }

        TechData techData = GetTechData(_currentResearchIndex);
        if (techData == null)
        {
            return 0;
        }

        return techData.researchDuration;
    }

    public bool IsResearchInProgress()
    {
        return _currentResearchIndex >= 0;
    }

    private void CompleteResearch()
    {
        _techStates[_currentResearchIndex] = TechResearchState.Completed;

        TechData completedTech = GetTechData(_currentResearchIndex);
        if (completedTech != null)
        {
            ApplyTechUnlockEffects(completedTech);
        }

        UnlockSuccessors(_currentResearchIndex);

        int completedIndex = _currentResearchIndex;
        _currentResearchIndex = -1;
        _currentProgress = 0;
        _tickTimer = 0f;

        OnResearchCompleted?.Invoke(completedIndex);
        OnResearchStateChanged?.Invoke();
    }

    private void UnlockSuccessors(int techIndex)
    {
        TechData techData = GetTechData(techIndex);
        if (techData == null)
        {
            return;
        }

        if (techData.successorTechIndices == null)
        {
            return;
        }

        for (int i = 0; i < techData.successorTechIndices.Length; i++)
        {
            CheckAndUnlock(techData.successorTechIndices[i]);
        }
    }

    private void ApplyTechUnlockEffects(TechData tech)
    {
        if (tech.unlocksBuildings != null)
        {
            for (int i = 0; i < tech.unlocksBuildings.Length; i++)
            {
                if (tech.unlocksBuildings[i] != null && BuildingUnlockManager.Instance != null)
                {
                    BuildingUnlockManager.Instance.UnlockBuilding(tech.unlocksBuildings[i]);
                }
            }
        }

        if (tech.unlocksUnits != null)
        {
            for (int i = 0; i < tech.unlocksUnits.Length; i++)
            {
                if (tech.unlocksUnits[i] != null && UnitUnlockManager.Instance != null)
                {
                    UnitUnlockManager.Instance.UnlockUnit(tech.unlocksUnits[i]);
                }
            }
        }

        if (tech.unlocksResources != null)
        {
            for (int i = 0; i < tech.unlocksResources.Length; i++)
            {
                if (ResourceUnlockManager.Instance != null)
                {
                    ResourceUnlockManager.Instance.UnlockResource(tech.unlocksResources[i]);
                }
            }
        }

        if (tech.grantStatTypes != null && tech.grantStatValues != null)
        {
            int statCount = Mathf.Min(tech.grantStatTypes.Length, tech.grantStatValues.Length);
            for (int i = 0; i < statCount; i++)
            {
                if (ModuleEffectManager.Instance != null)
                {
                    ModuleEffectManager.Instance.GrantTechBonus(tech.grantStatTypes[i], tech.grantStatValues[i]);
                }
            }
        }
    }

    private void CheckAndUnlock(int techIndex)
    {
        TechData techData = GetTechData(techIndex);
        if (techData == null)
        {
            return;
        }

        if (techData.prerequisiteTechIndices == null || techData.prerequisiteTechIndices.Length == 0)
        {
            _techStates[techIndex] = TechResearchState.Available;
            OnResearchStateChanged?.Invoke();
            return;
        }

        for (int i = 0; i < techData.prerequisiteTechIndices.Length; i++)
        {
            int prereqIndex = techData.prerequisiteTechIndices[i];
            TechResearchState prereqState = GetTechState(prereqIndex);
            if (prereqState != TechResearchState.Completed)
            {
                return;
            }
        }

        _techStates[techIndex] = TechResearchState.Available;
        OnResearchStateChanged?.Invoke();
    }
}
