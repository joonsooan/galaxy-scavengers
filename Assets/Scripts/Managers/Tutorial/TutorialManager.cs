using System;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
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

[Serializable]
public struct HighlightableUI
{
    public string id;
    public GameObject uiObject;
    public Material highlightMaterial;
}


public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private int firstQuestId = 1;
    [SerializeField] private GameObject tutorialUI;
    [SerializeField] private TutorialStepData[] tutorialStepDataList;

    [Header("UI Panels")]
    [SerializeField] private GameObject resourcePanel;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject debuffPanel;
    [SerializeField] private GameObject timeSlider;
    [SerializeField] private GameObject gameSpeed;
    [SerializeField] private GameObject noisePanel;
    [SerializeField] private GameObject unitPopulationPanel;
    [SerializeField] private GameObject alertPanel;
    [SerializeField] private GameObject mainControlPanel;
    [SerializeField] private GameObject questPanel;
    [SerializeField] private GameObject launchButton;

    [Header("Highlight Settings")]
    [SerializeField] private List<HighlightableUI> highlightableList = new List<HighlightableUI>();
    [Header("Arrow UI Settings")]
    [SerializeField] private List<GameObject> arrowUIObjects = new List<GameObject>();
    private readonly List<GameObject> _activeHighlights = new List<GameObject>();
    private readonly List<GameObject> _currentHighlightTargets = new List<GameObject>();
    private readonly Dictionary<string, HighlightableUI> _highlightLookup = new Dictionary<string, HighlightableUI>();
    private readonly Dictionary<string, GameObject> _arrowUILookup = new Dictionary<string, GameObject>();
    private readonly Dictionary<GameObject, Material> _highlightMaterials = new Dictionary<GameObject, Material>();
    private readonly Dictionary<GameObject, Material> _originalMaterials = new Dictionary<GameObject, Material>();
    private readonly List<UnitBase> _spawnedEnemyUnits = new List<UnitBase>();
    private readonly Dictionary<TutorialUIPanel, GameObject> _uiPanels = new Dictionary<TutorialUIPanel, GameObject>();
    private int _buildingCompletedCount;
    private int _buildingPlacedCount;
    private int _bulletFireCount;
    private int _currentStepIndex = -1;
    private bool _isTutorialActive;
    private bool _isWaitingForCondition;
    private int _itemProducedCount;
    private float _lastMouseWheelValue;
    private bool _lastPausedState;
    private int _mineableTypesChangedCount;
    private int _mouseWheelScrollCount;

    private int _numberKeyPressCount;
    private RectTransform _rect;
    private int _resourceBlockRevealCount;
    private int _resourceMinedAmount;
    private int _rightClickCount;
    private int _spacebarPressCount;

    private List<TutorialStepData> _tutorialSteps = new List<TutorialStepData>();
    private TutorialUI _tutorialUI;
    private int _unitProducedCount;
    private GameObject _currentArrowUI;

    private float _wasdInputTime;
    public static TutorialManager Instance { get; private set; }
    
    public static event Action OnTutorialEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (HighlightableUI item in highlightableList) {
            if (!string.IsNullOrEmpty(item.id))
                _highlightLookup[item.id] = item;
        }

        foreach (GameObject arrowUI in arrowUIObjects) {
            if (arrowUI != null) {
                TutorialArrowUI arrowComponent = arrowUI.GetComponent<TutorialArrowUI>();
                if (arrowComponent != null && !string.IsNullOrEmpty(arrowComponent.ArrowID)) {
                    _arrowUILookup[arrowComponent.ArrowID] = arrowUI;
                    arrowUI.SetActive(false);
                }
            }
        }

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

        yield return CoroutineCache.GetWaitForSeconds(0.5f);

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

        if (DayNightCycleManager.Instance != null) {
            DayNightCycleManager.Instance.SetAutoAdvanceTime(false);
        }

        DisableAllEnemyUnits();
        BuildUIPanelDictionary();
        HideAllUIPanels();

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
        EnableUIPanelsForStep(currentStep);
        EnableMaterialHighlights(currentStep);
        ShowArrowUI(currentStep);

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
        _rightClickCount = 0;
        _lastMouseWheelValue = Input.mouseScrollDelta.y;
        if (GameManager.Instance != null) {
            _lastPausedState = GameManager.Instance.IsPaused;
        }
    }

    private IEnumerator CheckStepCondition(TutorialStepData step)
    {
        while (_isWaitingForCondition) {
            bool conditionMet = false;

            switch (step.stepType) {
            case TutorialStepType.Click:
                if (Input.GetMouseButtonUp(0) && IsClickingGameWorld()) {
                    conditionMet = true;
                }
                break;

            case TutorialStepType.RightClick:
                if (Input.GetMouseButtonUp(1) && IsClickingGameWorld()) {
                    _rightClickCount++;
                    if (_tutorialUI != null && step.showProgressBar) {
                        _tutorialUI.UpdateProgress((float)_rightClickCount / step.count);
                    }
                    if (_rightClickCount >= step.count) {
                        conditionMet = true;
                    }
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
                bool spacebarPressed = Input.GetKeyDown(KeyCode.Space);
                bool pauseButtonClicked = false;

                if (GameManager.Instance != null) {
                    bool currentPausedState = GameManager.Instance.IsPaused;
                    if (currentPausedState != _lastPausedState) {
                        pauseButtonClicked = true;
                        _lastPausedState = currentPausedState;
                    }
                }

                if (spacebarPressed || pauseButtonClicked) {
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
                PlayCompletionSound(step);
                DisableCurrentHighlights();
                HideArrowUI();
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

    public TutorialStepData GetCurrentTutorialStep()
    {
        if (_isTutorialActive && _currentStepIndex >= 0 && _currentStepIndex < _tutorialSteps.Count)
        {
            return _tutorialSteps[_currentStepIndex];
        }
        return null;
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

        if (DayNightCycleManager.Instance != null) {
            DayNightCycleManager.Instance.SetAutoAdvanceTime(true);
        }

        EnableAllEnemyUnits();
        ShowAllUIPanels();
        HideArrowUI();

        if (_tutorialUI != null) {
            _tutorialUI.HideTutorial();
        }

        if (QuestManager.Instance != null) {
            QuestManager.Instance.CompleteQuest(firstQuestId);
        }

        if (CoreRepairManager.Instance != null) {
            CoreRepairManager.Instance.InitializeLanding();
        }
        
        OnTutorialEnded?.Invoke();
    }

    public void SkipAllTutorials()
    {
        _isTutorialActive = false;
        _isWaitingForCondition = false;
        StopAllCoroutines();

        if (DayNightCycleManager.Instance != null) {
            DayNightCycleManager.Instance.SetAutoAdvanceTime(true);
        }

        EnableAllEnemyUnits();
        ShowAllUIPanels();
        HideArrowUI();

        if (_tutorialUI != null) {
            _tutorialUI.HideTutorial();
        }

        if (QuestManager.Instance != null) {
            QuestManager.Instance.CompleteQuest(firstQuestId);
        }
        
        if (CoreRepairManager.Instance != null) {
            CoreRepairManager.Instance.InitializeLanding();
        }
        
        OnTutorialEnded?.Invoke();
    }

    public void OnQuestProgressReset()
    {
        if (_isTutorialActive) {
            _isTutorialActive = false;
            _isWaitingForCondition = false;
            StopAllCoroutines();

            if (DayNightCycleManager.Instance != null) {
                DayNightCycleManager.Instance.SetAutoAdvanceTime(true);
            }

            EnableAllEnemyUnits();
            ShowAllUIPanels();
            HideArrowUI();

            if (_tutorialUI != null) {
                _tutorialUI.HideTutorial();
            }
        }

        if (ShouldStartTutorial()) {
            InitializeTutorialSteps();
            StartCoroutine(WaitForGameInitialization());
        }
    }

    public void RegisterSpawnedEnemy(EnemyUnitBase enemyUnit)
    {
        if (enemyUnit == null) return;

        if (!_spawnedEnemyUnits.Contains(enemyUnit)) {
            _spawnedEnemyUnits.Add(enemyUnit);
        }

        if (_isTutorialActive) {
            enemyUnit.gameObject.SetActive(false);
        }
    }

    private void DisableAllEnemyUnits()
    {
        foreach (UnitBase enemyUnit in _spawnedEnemyUnits) {
            if (enemyUnit != null && enemyUnit.gameObject != null) {
                enemyUnit.gameObject.SetActive(false);
            }
        }
    }

    private void EnableAllEnemyUnits()
    {
        foreach (UnitBase enemyUnit in _spawnedEnemyUnits) {
            if (enemyUnit != null && enemyUnit.gameObject != null) {
                enemyUnit.gameObject.SetActive(true);
            }
        }
    }

    private void BuildUIPanelDictionary()
    {
        _uiPanels.Clear();

        _uiPanels[TutorialUIPanel.ResourcePanel] = resourcePanel;
        _uiPanels[TutorialUIPanel.StatsPanel] = statsPanel;
        _uiPanels[TutorialUIPanel.DebuffPanel] = debuffPanel;
        _uiPanels[TutorialUIPanel.TimeSlider] = timeSlider;
        _uiPanels[TutorialUIPanel.GameSpeed] = gameSpeed;
        _uiPanels[TutorialUIPanel.NoisePanel] = noisePanel;
        _uiPanels[TutorialUIPanel.UnitPopulationPanel] = unitPopulationPanel;
        _uiPanels[TutorialUIPanel.AlertPanel] = alertPanel;
        _uiPanels[TutorialUIPanel.MainControlPanel] = mainControlPanel;
        _uiPanels[TutorialUIPanel.LaunchButton] = launchButton;
        _uiPanels[TutorialUIPanel.QuestPanel] = questPanel;
    }

    private void HideAllUIPanels()
    {
        foreach (GameObject panel in _uiPanels.Values) {
            if (panel != null) {
                panel.SetActive(false);
            }
        }
    }

    private void ShowAllUIPanels()
    {
        foreach (GameObject panel in _uiPanels.Values) {
            if (panel != null) {
                panel.SetActive(true);
            }
        }
    }

    private void EnableUIPanelsForStep(TutorialStepData step)
    {
        if (step == null || step.enableUIPanels == null) {
            return;
        }

        foreach (TutorialUIPanel panelType in step.enableUIPanels) {
            if (_uiPanels.TryGetValue(panelType, out GameObject panel) && panel != null) {
                panel.SetActive(true);
            }
        }
    }

    private void PlayCompletionSound(TutorialStepData step)
    {
        if (step == null || step.completionSound.IsNull) {
            return;
        }

        RuntimeManager.PlayOneShot(step.completionSound);
    }

    public void RegisterRuntimeUI(string id, GameObject uiObject, Material highlightMaterial)
    {
        if (string.IsNullOrEmpty(id) || uiObject == null)
        {
            return;
        }

        HighlightableUI newEntry = new HighlightableUI {
            id = id,
            uiObject = uiObject,
            highlightMaterial = highlightMaterial
        };

        _highlightLookup[id] = newEntry;

        if (_isTutorialActive && _isWaitingForCondition && _currentStepIndex >= 0 && _currentStepIndex < _tutorialSteps.Count)
        {
            TutorialStepData currentStep = _tutorialSteps[_currentStepIndex];
            if (currentStep != null && currentStep.enableMaterialHighlight && currentStep.highlightTargetIDs != null)
            {
                if (currentStep.highlightTargetIDs.Contains(id))
                {
                    ApplyHighlightToUI(uiObject, highlightMaterial);
                }
            }
        }
    }

    private void ApplyHighlightToUI(GameObject uiObject, Material highlightMaterial)
    {
        if (uiObject == null || highlightMaterial == null) return;

        Image targetImage = FindButtonImage(uiObject);
        if (targetImage != null)
        {
            GameObject buttonObject = targetImage.gameObject;
            if (!_originalMaterials.ContainsKey(buttonObject))
            {
                _originalMaterials[buttonObject] = targetImage.material;
            }

            targetImage.material = highlightMaterial;
            
            if (!_activeHighlights.Contains(buttonObject))
            {
                _activeHighlights.Add(buttonObject);
            }
        }
    }

    private Image FindButtonImage(GameObject parentObject)
    {
        Button[] buttons = parentObject.GetComponentsInChildren<Button>(true);
        if (buttons != null && buttons.Length > 0)
        {
            Image image = buttons[0].GetComponent<Image>();
            if (image != null)
            {
                return image;
            }
        }
        return null;
    }

    private void EnableMaterialHighlights(TutorialStepData step)
    {
        if (step == null || !step.enableMaterialHighlight || step.highlightTargetIDs == null)
        {
            return;
        }

        DisableCurrentHighlights();

        foreach (string id in step.highlightTargetIDs)
        {
            if (_highlightLookup.TryGetValue(id, out HighlightableUI entry))
            {
                if (entry.uiObject == null || entry.highlightMaterial == null)
                {
                    continue;
                }

                Image targetImage = FindButtonImage(entry.uiObject);
                if (targetImage != null)
                {
                    GameObject buttonObject = targetImage.gameObject;
                    if (!_originalMaterials.ContainsKey(buttonObject))
                    {
                        _originalMaterials[buttonObject] = targetImage.material;
                    }

                    targetImage.material = entry.highlightMaterial;
                    if (!_activeHighlights.Contains(buttonObject))
                    {
                        _activeHighlights.Add(buttonObject);
                    }
                }
            }
        }
    }

    private void DisableCurrentHighlights()
    {
        foreach (GameObject target in _activeHighlights) {
            if (target == null) continue;

            Image image = target.GetComponent<Image>();
            if (image != null && _originalMaterials.TryGetValue(target, out Material original)) {
                image.material = original;
            }
        }

        _activeHighlights.Clear();
    }

    public void DisableHighlightForTarget(GameObject target)
    {
        if (target == null) return;

        Image image = target.GetComponent<Image>();
        if (image != null) {
            image.material = null;
        }

        if (_currentHighlightTargets.Contains(target)) {
            _currentHighlightTargets.Remove(target);
        }
    }

    private void ShowArrowUI(TutorialStepData step)
    {
        HideArrowUI();

        if (step == null || !step.showArrowUI || string.IsNullOrEmpty(step.arrowID))
        {
            return;
        }

        if (_arrowUILookup.TryGetValue(step.arrowID, out GameObject arrowUI))
        {
            _currentArrowUI = arrowUI;
            if (_currentArrowUI != null)
            {
                _currentArrowUI.SetActive(true);
            }
        }
    }

    private void HideArrowUI()
    {
        if (_currentArrowUI != null)
        {
            _currentArrowUI.SetActive(false);
            _currentArrowUI = null;
        }
    }

}
