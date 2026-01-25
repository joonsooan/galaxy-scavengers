using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TutorialStepType
{
    Click,
    RightClick,
    WASDInput,
    MouseWheel,
    SpacebarPress,
    NumberKeyPress,
    ResourceMined,
    ResourceBlockRevealed,
    BulletFired,
    BuildingPlaced,
    BuildingCompleted,
    UnitProduced,
    ItemProduced,
    MineableTypesChanged
}


public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private int firstQuestId = 1;
    [SerializeField] private GameObject tutorialUI;
    [SerializeField] private TutorialStepData[] tutorialStepDataList;
    private int _buildingCompletedCount;
    private int _buildingPlacedCount;
    private int _bulletFireCount;
    private int _currentStepIndex = -1;
    private bool _isTutorialActive;
    private bool _isWaitingForCondition;
    private int _itemProducedCount;
    private float _lastMouseWheelValue;
    private int _mouseWheelScrollCount;

    private int _numberKeyPressCount;
    private RectTransform _rect;
    private int _resourceBlockRevealCount;
    private int _mineableTypesChangedCount;
    private int _resourceMinedAmount;
    private int _spacebarPressCount;

    private List<TutorialStepData> _tutorialSteps = new List<TutorialStepData>();
    private TutorialUI _tutorialUI;
    private int _unitProducedCount;

    private float _wasdInputTime;
    public static TutorialManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _rect = tutorialUI.GetComponent<RectTransform>();
        _tutorialUI = tutorialUI.GetComponent<TutorialUI>();
    }

    private void Start()
    {
        if (ShouldStartTutorial()) {
            InitializeTutorialSteps();
            StartCoroutine(WaitForGameInitialization());
        }
    }

    private void OnEnable()
    {
        UnitManager.OnMineableTypesChanged += OnMineableTypesChanged;
    }

    private void OnDisable()
    {
        UnitManager.OnMineableTypesChanged -= OnMineableTypesChanged;
    }

    private bool ShouldStartTutorial()
    {
        if (QuestManager.Instance == null) {
            return false;
        }

        return !QuestManager.Instance.IsQuestCompleted(firstQuestId);
    }

    private IEnumerator WaitForGameInitialization()
    {
        while (GameManager.Instance == null || !GameManager.Instance.IsGameSceneInitialized) {
            yield return null;
        }

        while (LoadingUIManager.Instance != null) {
            LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
            if (loadingScreen != null && loadingScreen.gameObject.activeSelf) {
                yield return null;
            }
            else {
                break;
            }
        }

        yield return new WaitForSeconds(0.5f);

        StartTutorial();
    }

    private void InitializeTutorialSteps()
    {
        _tutorialSteps = new List<TutorialStepData>();

        TutorialStepData[] loadedFromResources = Resources.LoadAll<TutorialStepData>("Tutorial Data");
        if (loadedFromResources != null && loadedFromResources.Length > 0) {
            foreach (TutorialStepData stepData in loadedFromResources) {
                if (stepData != null) {
                    _tutorialSteps.Add(stepData);
                }
            }
        }
        else if (tutorialStepDataList != null && tutorialStepDataList.Length > 0) {
            foreach (TutorialStepData stepData in tutorialStepDataList) {
                if (stepData != null) {
                    _tutorialSteps.Add(stepData);
                }
            }
        }

        if (_tutorialSteps.Count > 0) {
            _tutorialSteps.Sort((a, b) => a.stepIndex.CompareTo(b.stepIndex));
        }
    }

    private void StartTutorial()
    {
        _isTutorialActive = true;
        _currentStepIndex = 0;
        
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.Instance.SetAutoAdvanceTime(false);
        }
        
        DisableAllEnemyUnits();
        
        NextStep();
    }

    private void NextStep()
    {
        if (_currentStepIndex >= _tutorialSteps.Count) {
            EndTutorial();
            return;
        }

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        ResetStepCounters();

        if (_tutorialUI != null) {
            _tutorialUI.ShowTutorialStep(currentStep);
        }

        ProcessStepStartActions(currentStep);

        _isWaitingForCondition = true;
        StartCoroutine(CheckStepCondition(currentStep));

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
    }

    private void ProcessStepStartActions(TutorialStepData step)
    {
        if (step.spawnUnits != null && step.spawnUnits.Length > 0) {
            SpawnUnits(step.spawnUnits);
        }

        if (step.grantResources != null && step.grantResources.Length > 0) {
            GrantResources(step.grantResources);
        }
    }

    private void SpawnUnits(UnitData[] unitsToSpawn)
    {
        if (UnitManager.Instance == null || UnitManager.Instance.unitParent == null) {
            return;
        }

        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        Vector3 spawnPosition = mainStructure != null ? mainStructure.transform.position : Vector3.zero;

        foreach (UnitData unitData in unitsToSpawn) {
            if (unitData == null || unitData.unitPrefab == null) {
                continue;
            }

            Vector3 offSet = new Vector3(0f, -2f, 0f);
            GameObject unitObj = Instantiate(unitData.unitPrefab, spawnPosition + offSet, Quaternion.identity, UnitManager.Instance.unitParent);
            UnitBase unitBase = unitObj.GetComponent<UnitBase>();
            if (unitBase != null) {
                unitBase.unitType = UnitBase.UnitType.Ally;
            }
        }
    }

    private void GrantResources(ResourceCost[] resources)
    {
        if (ResourceManager.Instance == null) {
            return;
        }

        MainStructure mainStructure = null;
        if (ResourceDataManager.Instance != null) {
            mainStructure = ResourceDataManager.Instance.GetMainStructure();
        }

        foreach (ResourceCost resourceCost in resources) {
            if (resourceCost.amount <= 0) {
                continue;
            }

            int remaining = resourceCost.amount;

            if (mainStructure != null) {
                int beforeAmount = mainStructure.GetCurrentResourceAmount(resourceCost.resourceType);
                bool added = mainStructure.TryAddResource(resourceCost.resourceType, remaining);
                int afterAmount = mainStructure.GetCurrentResourceAmount(resourceCost.resourceType);
                int addedAmount = afterAmount - beforeAmount;

                if (added && addedAmount > 0) {
                    remaining -= addedAmount;
                }
            }

            if (remaining > 0) {
                ResourceManager.Instance.AddResource(resourceCost.resourceType, remaining);
            }
        }

        if (mainStructure != null) {
            mainStructure.UpdateStorageUI();
        }
    }

    private void ResetStepCounters()
    {
        _wasdInputTime = 0f;
        _mouseWheelScrollCount = 0;
        _spacebarPressCount = 0;
        _numberKeyPressCount = 0;
        _resourceMinedAmount = 0;
        _bulletFireCount = 0;
        _buildingPlacedCount = 0;
        _buildingCompletedCount = 0;
        _unitProducedCount = 0;
        _itemProducedCount = 0;
        _resourceBlockRevealCount = 0;
        _mineableTypesChangedCount = 0;
        _lastMouseWheelValue = Input.mouseScrollDelta.y;
    }

    private IEnumerator CheckStepCondition(TutorialStepData step)
    {
        while (_isWaitingForCondition) {
            bool conditionMet = false;

            switch (step.stepType) {
            case TutorialStepType.Click:
                if (Input.GetMouseButtonDown(0) && IsClickingGameWorld()) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.RightClick:
                if (Input.GetMouseButtonDown(1) && IsClickingGameWorld()) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.WASDInput:
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)) {
                    _wasdInputTime += Time.deltaTime;
                    if (_tutorialUI != null && step.showProgressBar) {
                        _tutorialUI.UpdateProgress(_wasdInputTime / step.duration);
                    }
                    if (_wasdInputTime >= step.duration) {
                        conditionMet = true;
                    }
                }
                break;

            case TutorialStepType.MouseWheel:
                float currentWheelValue = Input.mouseScrollDelta.y;
                if (Mathf.Abs(currentWheelValue - _lastMouseWheelValue) > 0.01f) {
                    _mouseWheelScrollCount++;
                    if (_tutorialUI != null && step.showProgressBar) {
                        _tutorialUI.UpdateProgress((float)_mouseWheelScrollCount / step.count);
                    }
                    if (_mouseWheelScrollCount >= step.count) {
                        conditionMet = true;
                    }
                }
                _lastMouseWheelValue = currentWheelValue;
                break;

            case TutorialStepType.SpacebarPress:
                if (Input.GetKeyDown(KeyCode.Space)) {
                    _spacebarPressCount++;
                    if (_tutorialUI != null && step.showProgressBar) {
                        _tutorialUI.UpdateProgress((float)_spacebarPressCount / step.count);
                    }
                    if (_spacebarPressCount >= step.count) {
                        conditionMet = true;
                    }
                }
                break;

            case TutorialStepType.NumberKeyPress:
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Alpha3)) {
                    _numberKeyPressCount++;
                    if (_tutorialUI != null && step.showProgressBar) {
                        _tutorialUI.UpdateProgress((float)_numberKeyPressCount / step.count);
                    }
                    if (_numberKeyPressCount >= step.count) {
                        conditionMet = true;
                    }
                }
                break;

            case TutorialStepType.ResourceMined:
                if (_tutorialUI != null && step.showProgressBar) {
                    _tutorialUI.UpdateProgress((float)_resourceMinedAmount / step.count);
                }
                if (_resourceMinedAmount >= step.count) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.ResourceBlockRevealed:
                int targetBlocks = Mathf.Max(1, step.count);
                if (_tutorialUI != null && step.showProgressBar) {
                    float progress = Mathf.Clamp01((float)_resourceBlockRevealCount / targetBlocks);
                    _tutorialUI.UpdateProgress(progress);
                }
                if (_resourceBlockRevealCount >= targetBlocks) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.MineableTypesChanged:
                if (_tutorialUI != null && step.showProgressBar && step.count > 0) {
                    float progress = Mathf.Clamp01((float)_mineableTypesChangedCount / step.count);
                    _tutorialUI.UpdateProgress(progress);
                }
                if (step.count > 0 && _mineableTypesChangedCount >= step.count) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.BulletFired:
                if (_tutorialUI != null && step.showProgressBar) {
                    _tutorialUI.UpdateProgress((float)_bulletFireCount / step.count);
                }
                if (_bulletFireCount >= step.count) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.BuildingPlaced:
                if (_buildingPlacedCount > 0) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.BuildingCompleted:
                if (_buildingCompletedCount > 0) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.UnitProduced:
                if (_tutorialUI != null && step.showProgressBar && step.count > 0) {
                    _tutorialUI.UpdateProgress((float)_unitProducedCount / step.count);
                }
                if (_unitProducedCount >= step.count) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.ItemProduced:
                if (_tutorialUI != null && step.showProgressBar && step.count > 0) {
                    _tutorialUI.UpdateProgress((float)_itemProducedCount / step.count);
                }
                if (_itemProducedCount >= step.count) {
                    conditionMet = true;
                }
                break;
            }

            if (conditionMet) {
                _isWaitingForCondition = false;
                _currentStepIndex++;
                NextStep();
                yield break;
            }

            yield return null;
        }
    }

    private bool IsClickingGameWorld()
    {
        if (EventSystem.current == null) {
            return true;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current) {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results) {
            if (result.gameObject.layer == LayerMask.NameToLayer("UI")) {
                return false;
            }
        }

        return true;
    }

    public void OnResourceMined(ResourceType resourceType, int amount)
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.ResourceMined && currentStep.resourceType == resourceType) {
            _resourceMinedAmount += amount;
        }
    }

    public void OnBulletFired()
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.BulletFired) {
            _bulletFireCount++;
        }
    }

    public void OnResourceBlockRevealed()
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.ResourceBlockRevealed) {
            _resourceBlockRevealCount++;
        }
    }

    private void OnMineableTypesChanged(ResourceType[] newTypes)
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.MineableTypesChanged) {
            _mineableTypesChangedCount++;
        }
    }

    public bool IsTutorialActive()
    {
        return _isTutorialActive;
    }

    public void OnBuildingPlaced(string buildingType)
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.BuildingPlaced && currentStep.buildingType == buildingType) {
            _buildingPlacedCount++;
        }
    }

    public void OnBuildingCompleted(string buildingType)
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.BuildingCompleted &&
            (string.IsNullOrEmpty(currentStep.buildingType) || currentStep.buildingType == buildingType)) {
            _buildingCompletedCount++;
        }
    }

    public void OnUnitProduced(string unitType)
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.UnitProduced && currentStep.unitType == unitType) {
            _unitProducedCount++;
        }
    }

    public void OnItemProduced(string itemType)
    {
        if (!_isTutorialActive || !_isWaitingForCondition) return;

        TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
        if (currentStep.stepType == TutorialStepType.ItemProduced && currentStep.itemType == itemType) {
            _itemProducedCount++;
        }
    }

    private void EndTutorial()
    {
        _isTutorialActive = false;
        _isWaitingForCondition = false;

        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.Instance.SetAutoAdvanceTime(true);
        }
        
        EnableAllEnemyUnits();

        if (_tutorialUI != null) {
            _tutorialUI.HideTutorial();
        }
    }
    
    private void DisableAllEnemyUnits()
    {
        if (UnitManager.Instance == null) return;
        
        List<UnitBase> enemyUnitsCopy = new List<UnitBase>(UnitManager.Instance.EnemyUnits);
        
        foreach (UnitBase enemyUnit in enemyUnitsCopy)
        {
            if (enemyUnit != null && enemyUnit.gameObject != null)
            {
                enemyUnit.gameObject.SetActive(false);
            }
        }
    }
    
    private void EnableAllEnemyUnits()
    {
        if (UnitManager.Instance == null) return;
        
        List<UnitBase> enemyUnitsCopy = new List<UnitBase>(UnitManager.Instance.EnemyUnits);
        
        foreach (UnitBase enemyUnit in enemyUnitsCopy)
        {
            if (enemyUnit != null && enemyUnit.gameObject != null)
            {
                enemyUnit.gameObject.SetActive(true);
            }
        }
    }
}
