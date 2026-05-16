using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum GameAlertType
{
    MinerNoResource = 0,
    UnitUnderAttack = 1,
    BuildingUnderAttack = 2,
    DroneNoResource = 3,
    StorageFull = 4,
    ElecInsufficient = 5,
    NoiseCaution = 6,
    NoiseWarning = 7,
    NoiseDanger = 8,
    MinerIsFull = 11,
    DroneIsNotAssigned = 12,
    ConstructNoResource = 13
}

public class GameAlertUIManager : MonoBehaviour
{
    [SerializeField] private GameObject minerNoResourceCell;
    [SerializeField] private GameObject minerIsFullCell;
    [SerializeField] private GameObject unitUnderAttackCell;
    [SerializeField] private GameObject buildingUnderAttackCell;
    [SerializeField] private GameObject droneIsNotAssignedCell;
    [SerializeField] private GameObject droneNoResourceCell;
    [SerializeField] private GameObject constructNoResourceCell;
    [SerializeField] private GameObject storageFullCell;
    [SerializeField] private GameObject elecInsufficientCell;
    [SerializeField] private GameObject noiseCautionCell;
    [SerializeField] private GameObject noiseWarningCell;
    [SerializeField] private GameObject noiseDangerCell;

    [Header("Alert Sounds")]
    [SerializeField] private EventReference minerNoResourceSound;
    [SerializeField] private EventReference minerIsFullSound;
    [SerializeField] private EventReference unitUnderAttackSound;
    [SerializeField] private EventReference buildingUnderAttackSound;
    [SerializeField] private EventReference droneIsNotAssignedSound;
    [SerializeField] private EventReference droneNoResourceSound;
    [SerializeField] private EventReference constructNoResourceSound;
    [SerializeField] private EventReference storageFullSound;
    [SerializeField] private EventReference elecInsufficientSound;
    [SerializeField] private EventReference noiseCautionSound;
    [SerializeField] private EventReference noiseWarningSound;
    [SerializeField] private EventReference noiseDangerSound;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [Header("Tooltip Messages")]
    [SerializeField, TextArea(2, 6)] private string tooltipMinerNoResource = "채굴 유닛이 캘 자원이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipMinerIsFull = "채굴 유닛이 자원을 저장할 공간이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipUnitUnderAttack = "유닛이 공격당하고 있습니다!";
    [SerializeField, TextArea(2, 6)] private string tooltipBuildingUnderAttack = "건물이 공격당하고 있습니다!";
    [SerializeField, TextArea(2, 6)] private string tooltipDroneIsNotAssigned = "가공 유닛이 프로세서에 배정되지 않았습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipDroneNoResource = "가공 유닛이 가공할 자원이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipConstructNoResource = "건설 유닛이 건설할 자원이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipStorageFull = "모든 저장 공간이 가득 찼습니다!\n저장고를 더 건설해주세요.";
    [SerializeField, TextArea(2, 6)] private string tooltipElecInsufficient = "전력이 부족합니다!\n발전기·배터리·전력망을 확인해주세요.";
    [SerializeField, TextArea(2, 6)] private string tooltipNoiseCaution = "소음 정도 : 주의\n적의 습격에 대비하세요!";
    [SerializeField, TextArea(2, 6)] private string tooltipNoiseWarning = "소음 정도 : 경고\n적의 습격에 대비하세요!";
    [SerializeField, TextArea(2, 6)] private string tooltipNoiseDanger = "소음 정도 : 위험\n적의 습격에 대비하세요!";
    [SerializeField, TextArea(1, 3)] private string toolTipExtraTextUnit = "(클릭해서 유닛으로 이동)";
    [SerializeField, TextArea(1, 3)] private string toolTipExtraTextBuilding = "(클릭해서 건물로 이동)";

    private GameAlertType? _currentTooltipType;

    private readonly List<Damageable> _minerNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _minerIsFullSources = new List<Damageable>();
    private readonly List<Damageable> _unitUnderAttackSources = new List<Damageable>();
    private readonly List<Damageable> _buildingUnderAttackSources = new List<Damageable>();
    private readonly List<Damageable> _droneIsNotAssignedSources = new List<Damageable>();
    private readonly List<Damageable> _droneNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _constructNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _storageFullSources = new List<Damageable>();
    private readonly List<Damageable> _elecInsufficientSources = new List<Damageable>();
    private const int MaxSourcesPerType = 5;
    private Coroutine _tooltipRebuildCoroutine;
    private StorageTrackerManager _storageTrackerManager;
    private ElectricityConsumptionManager _electricityConsumptionManager;
    private Coroutine _bindStorageTrackerCoroutine;
    private bool _isStorageFullByTracker;
    private bool _isElecInsufficientByTracker;
    private int _minerNoResourceCount;
    private int _minerIsFullCount;
    private int _unitUnderAttackCount;
    private int _buildingUnderAttackCount;
    private int _droneIsNotAssignedCount;
    private int _droneNoResourceCount;
    private int _constructNoResourceCount;
    private int _storageFullCount;
    private int _elecInsufficientCount;
    [SerializeField] private CameraTargetController cameraTargetController;
    private readonly Dictionary<GameAlertType, int> _alertFocusIndices = new Dictionary<GameAlertType, int>();
    private bool _unitUnderAttackAlertActive;
    private bool _buildingUnderAttackAlertActive;

    private bool _isCountdownIgnoringAlerts;

    private void Awake()
    {
        SetAllInactive();
        ApplyLocalizedAlertCellTexts();
    }

    private void OnEnable()
    {
        LaunchUIController.OnLaunchSequenceStarted += OnLaunchSequenceStarted;
        LaunchUIController.OnLaunchSequenceFinished += OnLaunchSequenceFinished;
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        ApplyLocalizedAlertCellTexts();
        _isStorageFullByTracker = false;
        _isElecInsufficientByTracker = false;
        if (_bindStorageTrackerCoroutine != null)
            StopCoroutine(_bindStorageTrackerCoroutine);
        _bindStorageTrackerCoroutine = StartCoroutine(BindStorageTrackerWhenReady());
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged += OnNoiseChanged;
            OnNoiseChanged(NoiseManager.Instance.NoisePercentage);
        }
    }

    private void OnDisable()
    {
        if (_bindStorageTrackerCoroutine != null)
        {
            StopCoroutine(_bindStorageTrackerCoroutine);
            _bindStorageTrackerCoroutine = null;
        }
        if (_storageTrackerManager != null)
        {
            _storageTrackerManager.OnStorageChanged -= OnTrackedStorageChanged;
        }
        _storageTrackerManager = null;
        if (_electricityConsumptionManager != null)
        {
            _electricityConsumptionManager.OnAfterElectricityConsumersResolved -= OnElectricityPowerTickResolved;
            _electricityConsumptionManager = null;
        }
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
        }
        LaunchUIController.OnLaunchSequenceStarted -= OnLaunchSequenceStarted;
        LaunchUIController.OnLaunchSequenceFinished -= OnLaunchSequenceFinished;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        HideTooltip();
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedAlertCellTexts();
        if (_currentTooltipType.HasValue)
            RefreshTooltipText(_currentTooltipType.Value);
    }

    private void OnNoiseChanged(float noisePercentage)
    {
        if (NoiseManager.Instance == null) return;

        NoiseManager.NoiseZone zone = NoiseManager.Instance.GetCurrentNoiseZone();
        if (noiseCautionCell != null)
        {
            bool cautionActive = zone == NoiseManager.NoiseZone.Caution;
            if (cautionActive && !noiseCautionCell.activeSelf && !noiseCautionSound.IsNull)
                RuntimeManager.PlayOneShot(noiseCautionSound);
            noiseCautionCell.SetActive(cautionActive);
        }
        if (noiseWarningCell != null)
        {
            bool warningActive = zone == NoiseManager.NoiseZone.Warning;
            if (warningActive && !noiseWarningCell.activeSelf && !noiseWarningSound.IsNull)
                RuntimeManager.PlayOneShot(noiseWarningSound);
            noiseWarningCell.SetActive(warningActive);
        }
        if (noiseDangerCell != null)
        {
            bool dangerActive = zone == NoiseManager.NoiseZone.Danger;
            if (dangerActive && !noiseDangerCell.activeSelf && !noiseDangerSound.IsNull)
                RuntimeManager.PlayOneShot(noiseDangerSound);
            noiseDangerCell.SetActive(dangerActive);
        }
    }

    private IEnumerator BindStorageTrackerWhenReady()
    {
        while (_storageTrackerManager == null)
        {
            _storageTrackerManager = FindFirstObjectByType<StorageTrackerManager>();
            if (_storageTrackerManager == null)
                yield return null;
        }

        _storageTrackerManager.OnStorageChanged -= OnTrackedStorageChanged;
        _storageTrackerManager.OnStorageChanged += OnTrackedStorageChanged;
        SyncStorageFullAlertWithTracker();

        ElectricityConsumptionManager ecm = null;
        while (ecm == null)
        {
            ecm = ElectricityConsumptionManager.Instance;
            if (ecm == null)
                yield return null;
        }

        _electricityConsumptionManager = ecm;
        _electricityConsumptionManager.OnAfterElectricityConsumersResolved -= OnElectricityPowerTickResolved;
        _electricityConsumptionManager.OnAfterElectricityConsumersResolved += OnElectricityPowerTickResolved;
        SyncElecInsufficientAlertWithTracker();
        _bindStorageTrackerCoroutine = null;
    }

    private void OnElectricityPowerTickResolved()
    {
        SyncElecInsufficientAlertWithTracker();
    }

    private void OnTrackedStorageChanged()
    {
        SyncStorageFullAlertWithTracker();
    }

    private void SyncStorageFullAlertWithTracker()
    {
        if (_storageTrackerManager == null)
            return;

        int max = _storageTrackerManager.MaxStorableResourceAmount;
        int current = _storageTrackerManager.CurrentTotalStoredResourceAmount;
        bool isStorageFull = max > 0 && current >= max;

        if (isStorageFull == _isStorageFullByTracker)
            return;

        _isStorageFullByTracker = isStorageFull;
        if (isStorageFull)
            RegisterAlert(GameAlertType.StorageFull);
        else
            UnregisterAlert(GameAlertType.StorageFull);
    }

    private void SyncElecInsufficientAlertWithTracker()
    {
        ElectricityConsumptionManager ecm = _electricityConsumptionManager != null
            ? _electricityConsumptionManager
            : ElectricityConsumptionManager.Instance;
        if (ecm == null)
            return;

        bool shouldAlert = ecm.GetTotalElectricityAmount() <= 0 && ecm.HasActiveElectricityDemand;

        if (shouldAlert == _isElecInsufficientByTracker)
            return;

        _isElecInsufficientByTracker = shouldAlert;
        if (shouldAlert)
            RegisterAlert(GameAlertType.ElecInsufficient);
        else
            UnregisterAlert(GameAlertType.ElecInsufficient);
    }

    public void RegisterAlert(GameAlertType type)
    {
        RegisterAlert(type, null);
    }

    public void RegisterAlert(GameAlertType type, Damageable source)
    {
        if (_isCountdownIgnoringAlerts && IsCountdownIgnoredAlert(type))
        {
            return;
        }
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                _minerNoResourceCount++;
                AddSource(_minerNoResourceSources, source);
                break;
            case GameAlertType.MinerIsFull:
                _minerIsFullCount++;
                AddSource(_minerIsFullSources, source);
                break;
            case GameAlertType.UnitUnderAttack:
                _unitUnderAttackCount++;
                AddSource(_unitUnderAttackSources, source);
                break;
            case GameAlertType.BuildingUnderAttack:
                _buildingUnderAttackCount++;
                AddSource(_buildingUnderAttackSources, source);
                break;
            case GameAlertType.DroneIsNotAssigned:
                _droneIsNotAssignedCount++;
                AddSource(_droneIsNotAssignedSources, source);
                break;
            case GameAlertType.DroneNoResource:
                _droneNoResourceCount++;
                AddSource(_droneNoResourceSources, source);
                break;
            case GameAlertType.ConstructNoResource:
                _constructNoResourceCount++;
                AddSource(_constructNoResourceSources, source);
                break;
            case GameAlertType.StorageFull:
                _storageFullCount++;
                AddSource(_storageFullSources, source);
                break;
            case GameAlertType.ElecInsufficient:
                _elecInsufficientCount++;
                AddSource(_elecInsufficientSources, source);
                break;
        }

        UpdateAlertState(type);
        if (_currentTooltipType == type)
            RefreshTooltipText(type);
    }

    private static bool IsCountdownIgnoredAlert(GameAlertType type)
    {
        return type == GameAlertType.MinerNoResource ||
               type == GameAlertType.DroneNoResource ||
               type == GameAlertType.DroneIsNotAssigned;
    }

    private void OnLaunchSequenceStarted()
    {
        _isCountdownIgnoringAlerts = true;
        _minerNoResourceCount = 0;
        _minerNoResourceSources.Clear();
        _droneNoResourceCount = 0;
        _droneNoResourceSources.Clear();
        _droneIsNotAssignedCount = 0;
        _droneIsNotAssignedSources.Clear();
        SetAlertActive(GameAlertType.MinerNoResource, false);
        SetAlertActive(GameAlertType.DroneNoResource, false);
        SetAlertActive(GameAlertType.DroneIsNotAssigned, false);
    }

    private void OnLaunchSequenceFinished()
    {
        _isCountdownIgnoringAlerts = false;
    }

    private static void AddSource(List<Damageable> list, Damageable source)
    {
        if (source == null) return;
        list.Add(source);
        while (list.Count > MaxSourcesPerType)
            list.RemoveAt(0);
    }

    public void UnregisterAlert(GameAlertType type)
    {
        UnregisterAlert(type, null);
    }

    public void UnregisterAlert(GameAlertType type, Damageable source)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                _minerNoResourceCount = Mathf.Max(0, _minerNoResourceCount - 1);
                RemoveSource(_minerNoResourceSources, source);
                break;
            case GameAlertType.MinerIsFull:
                _minerIsFullCount = Mathf.Max(0, _minerIsFullCount - 1);
                RemoveSource(_minerIsFullSources, source);
                break;
            case GameAlertType.UnitUnderAttack:
                _unitUnderAttackCount = Mathf.Max(0, _unitUnderAttackCount - 1);
                RemoveSource(_unitUnderAttackSources, source);
                break;
            case GameAlertType.BuildingUnderAttack:
                _buildingUnderAttackCount = Mathf.Max(0, _buildingUnderAttackCount - 1);
                RemoveSource(_buildingUnderAttackSources, source);
                break;
            case GameAlertType.DroneIsNotAssigned:
                _droneIsNotAssignedCount = Mathf.Max(0, _droneIsNotAssignedCount - 1);
                RemoveSource(_droneIsNotAssignedSources, source);
                break;
            case GameAlertType.DroneNoResource:
                _droneNoResourceCount = Mathf.Max(0, _droneNoResourceCount - 1);
                RemoveSource(_droneNoResourceSources, source);
                break;
            case GameAlertType.ConstructNoResource:
                _constructNoResourceCount = Mathf.Max(0, _constructNoResourceCount - 1);
                RemoveSource(_constructNoResourceSources, source);
                break;
            case GameAlertType.StorageFull:
                _storageFullCount = Mathf.Max(0, _storageFullCount - 1);
                RemoveSource(_storageFullSources, source);
                break;
            case GameAlertType.ElecInsufficient:
                _elecInsufficientCount = Mathf.Max(0, _elecInsufficientCount - 1);
                RemoveSource(_elecInsufficientSources, source);
                break;
        }

        UpdateAlertState(type);
        if (_currentTooltipType == type)
            RefreshTooltipText(type);
    }

    private static void RemoveSource(List<Damageable> list, Damageable source)
    {
        if (source == null) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == source)
            {
                list.RemoveAt(i);
                return;
            }
        }
    }

    public void SetAlertActive(GameAlertType type, bool active)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                if (minerNoResourceCell != null) minerNoResourceCell.SetActive(active);
                break;
            case GameAlertType.MinerIsFull:
                if (minerIsFullCell != null) minerIsFullCell.SetActive(active);
                break;
            case GameAlertType.UnitUnderAttack:
                if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(active);
                break;
            case GameAlertType.BuildingUnderAttack:
                if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(active);
                break;
            case GameAlertType.DroneIsNotAssigned:
                if (droneIsNotAssignedCell != null) droneIsNotAssignedCell.SetActive(active);
                break;
            case GameAlertType.DroneNoResource:
                if (droneNoResourceCell != null) droneNoResourceCell.SetActive(active);
                break;
            case GameAlertType.ConstructNoResource:
                if (constructNoResourceCell != null) constructNoResourceCell.SetActive(active);
                break;
            case GameAlertType.StorageFull:
                if (storageFullCell != null) storageFullCell.SetActive(active);
                break;
            case GameAlertType.ElecInsufficient:
                if (elecInsufficientCell != null) elecInsufficientCell.SetActive(active);
                break;
        }

        if (!active && _currentTooltipType == type)
            HideTooltip();
    }

    private void UpdateAlertState(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                if (minerNoResourceCell != null)
                {
                    bool active = _minerNoResourceCount > 0;
                    if (active && !minerNoResourceCell.activeSelf && !minerNoResourceSound.IsNull)
                        RuntimeManager.PlayOneShot(minerNoResourceSound);
                    minerNoResourceCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.MinerNoResource)
                        HideTooltip();
                }
                break;
            case GameAlertType.MinerIsFull:
                if (minerIsFullCell != null)
                {
                    bool active = _minerIsFullCount > 0;
                    EventReference sound = minerIsFullSound.IsNull ? minerNoResourceSound : minerIsFullSound;
                    if (active && !minerIsFullCell.activeSelf && !sound.IsNull)
                        RuntimeManager.PlayOneShot(sound);
                    minerIsFullCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.MinerIsFull)
                        HideTooltip();
                }
                break;
            case GameAlertType.UnitUnderAttack:
                if (unitUnderAttackCell != null)
                {
                    bool active = _unitUnderAttackCount > 0;
                    if (active && !_unitUnderAttackAlertActive && !unitUnderAttackSound.IsNull)
                        RuntimeManager.PlayOneShot(unitUnderAttackSound);
                    unitUnderAttackCell.SetActive(active);
                    _unitUnderAttackAlertActive = active;
                    if (!active && _currentTooltipType == GameAlertType.UnitUnderAttack)
                        HideTooltip();
                }
                break;
            case GameAlertType.BuildingUnderAttack:
                if (buildingUnderAttackCell != null)
                {
                    bool active = _buildingUnderAttackCount > 0;
                    if (active && !_buildingUnderAttackAlertActive && !buildingUnderAttackSound.IsNull)
                        RuntimeManager.PlayOneShot(buildingUnderAttackSound);
                    buildingUnderAttackCell.SetActive(active);
                    _buildingUnderAttackAlertActive = active;
                    if (!active && _currentTooltipType == GameAlertType.BuildingUnderAttack)
                        HideTooltip();
                }
                break;
            case GameAlertType.DroneIsNotAssigned:
                if (droneIsNotAssignedCell != null)
                {
                    bool active = _droneIsNotAssignedCount > 0;
                    EventReference sound = droneIsNotAssignedSound.IsNull ? droneNoResourceSound : droneIsNotAssignedSound;
                    if (active && !droneIsNotAssignedCell.activeSelf && !sound.IsNull)
                        RuntimeManager.PlayOneShot(sound);
                    droneIsNotAssignedCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.DroneIsNotAssigned)
                        HideTooltip();
                }
                break;
            case GameAlertType.DroneNoResource:
                if (droneNoResourceCell != null)
                {
                    bool active = _droneNoResourceCount > 0;
                    if (active && !droneNoResourceCell.activeSelf && !droneNoResourceSound.IsNull)
                        RuntimeManager.PlayOneShot(droneNoResourceSound);
                    droneNoResourceCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.DroneNoResource)
                        HideTooltip();
                }
                break;
            case GameAlertType.ConstructNoResource:
                if (constructNoResourceCell != null)
                {
                    bool active = _constructNoResourceCount > 0;
                    EventReference sound = constructNoResourceSound.IsNull ? droneNoResourceSound : constructNoResourceSound;
                    if (active && !constructNoResourceCell.activeSelf && !sound.IsNull)
                        RuntimeManager.PlayOneShot(sound);
                    constructNoResourceCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.ConstructNoResource)
                        HideTooltip();
                }
                break;
            case GameAlertType.StorageFull:
                if (storageFullCell != null)
                {
                    bool active = _storageFullCount > 0;
                    if (active && !storageFullCell.activeSelf && !storageFullSound.IsNull)
                        RuntimeManager.PlayOneShot(storageFullSound);
                    storageFullCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.StorageFull)
                        HideTooltip();
                }
                break;
            case GameAlertType.ElecInsufficient:
                if (elecInsufficientCell != null)
                {
                    bool active = _elecInsufficientCount > 0;
                    if (active && !elecInsufficientCell.activeSelf && !elecInsufficientSound.IsNull)
                        RuntimeManager.PlayOneShot(elecInsufficientSound);
                    elecInsufficientCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.ElecInsufficient)
                        HideTooltip();
                }
                break;
        }
    }

    private void SetAllInactive()
    {
        if (minerNoResourceCell != null) minerNoResourceCell.SetActive(false);
        if (minerIsFullCell != null) minerIsFullCell.SetActive(false);
        if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(false);
        if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(false);
        if (droneIsNotAssignedCell != null) droneIsNotAssignedCell.SetActive(false);
        if (droneNoResourceCell != null) droneNoResourceCell.SetActive(false);
        if (constructNoResourceCell != null) constructNoResourceCell.SetActive(false);
        if (storageFullCell != null) storageFullCell.SetActive(false);
        if (elecInsufficientCell != null) elecInsufficientCell.SetActive(false);
        if (noiseCautionCell != null) noiseCautionCell.SetActive(false);
        if (noiseWarningCell != null) noiseWarningCell.SetActive(false);
        if (noiseDangerCell != null) noiseDangerCell.SetActive(false);
        _unitUnderAttackAlertActive = false;
        _buildingUnderAttackAlertActive = false;
    }

    private void ApplyLocalizedAlertCellTexts()
    {
        SetAlertCellText(minerNoResourceCell, GameAlertType.MinerNoResource);
        SetAlertCellText(minerIsFullCell, GameAlertType.MinerIsFull);
        SetAlertCellText(unitUnderAttackCell, GameAlertType.UnitUnderAttack);
        SetAlertCellText(buildingUnderAttackCell, GameAlertType.BuildingUnderAttack);
        SetAlertCellText(droneIsNotAssignedCell, GameAlertType.DroneIsNotAssigned);
        SetAlertCellText(droneNoResourceCell, GameAlertType.DroneNoResource);
        SetAlertCellText(constructNoResourceCell, GameAlertType.ConstructNoResource);
        SetAlertCellText(storageFullCell, GameAlertType.StorageFull);
        SetAlertCellText(elecInsufficientCell, GameAlertType.ElecInsufficient);
        SetAlertCellText(noiseCautionCell, GameAlertType.NoiseCaution);
        SetAlertCellText(noiseWarningCell, GameAlertType.NoiseWarning);
        SetAlertCellText(noiseDangerCell, GameAlertType.NoiseDanger);
    }

    private void SetAlertCellText(GameObject cell, GameAlertType type)
    {
        if (cell == null)
            return;

        TMP_Text text = cell.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = GetAlertCellMessage(type);
    }

    private string GetAlertCellMessage(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.minerNoResource", "채굴 유닛 떠도는 중");
            case GameAlertType.MinerIsFull:
                return GameLocalization.GetOrDefault("UI_Common", "alert.minerIsFull", "자원 적재 불가");
            case GameAlertType.UnitUnderAttack:
                return GameLocalization.GetOrDefault("UI_Common", "alert.unitUnderAttack", "유닛이 공격받는 중");
            case GameAlertType.BuildingUnderAttack:
                return GameLocalization.GetOrDefault("UI_Common", "alert.buildingUnderAttack", "건물이 공격받는 중");
            case GameAlertType.DroneIsNotAssigned:
                return GameLocalization.GetOrDefault("UI_Common", "alert.droneIsNotAssigned", "가공 유닛 배정 필요");
            case GameAlertType.DroneNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.droneNoResource", "가공 재료 부족");
            case GameAlertType.ConstructNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.constructNoResource", "건설 재료 부족");
            case GameAlertType.StorageFull:
                return GameLocalization.GetOrDefault("UI_Common", "alert.storageFull", "저장 공간 부족");
            case GameAlertType.ElecInsufficient:
                return GameLocalization.GetOrDefault("UI_Common", "alert.elecInsufficient", "전력 부족");
            case GameAlertType.NoiseCaution:
                return GameLocalization.GetOrDefault("UI_Common", "alert.noiseCaution", "소음 단계 : 주의");
            case GameAlertType.NoiseWarning:
                return GameLocalization.GetOrDefault("UI_Common", "alert.noiseWarning", "소음 단계 : 경고");
            case GameAlertType.NoiseDanger:
                return GameLocalization.GetOrDefault("UI_Common", "alert.noiseDanger", "소음 단계 : 위험");
            default:
                return string.Empty;
        }
    }

    public void ShowTooltip(GameAlertType type)
    {
        if (tooltipPanel == null || tooltipText == null) return;
        _currentTooltipType = type;
        RefreshTooltipText(type);
        tooltipPanel.SetActive(true);
    }

    public void HideTooltip()
    {
        _currentTooltipType = null;
        if (tooltipText != null) tooltipText.text = string.Empty;
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    private void RefreshTooltipText(GameAlertType type)
    {
        if (tooltipText == null) return;
        string header = GetTooltipHeader(type);
        string extraText = GetToolTipExtraText(type);
        List<Damageable> sources = GetSourcesForType(type);
        var names = new List<string>();
        
        foreach (Damageable d in sources)
        {
            if (d == null) continue;
            string name = GetDisplayName(d);
            if (!string.IsNullOrEmpty(name)) names.Add(name);
        }
        
        if (type == GameAlertType.StorageFull || type == GameAlertType.ElecInsufficient ||
            type == GameAlertType.NoiseCaution || type == GameAlertType.NoiseWarning || type == GameAlertType.NoiseDanger)
            tooltipText.text = header;
        else if (names.Count > 0)
            tooltipText.text = header + "\n" + string.Join("\n", names.ConvertAll(n => "- " + n)) + "\n" + extraText;
        else
            tooltipText.text = header;
        
        RebuildTooltipLayout();
    }

    private string GetTooltipHeader(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.minerNoResource", tooltipMinerNoResource);
            case GameAlertType.MinerIsFull:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.minerIsFull", tooltipMinerIsFull);
            case GameAlertType.UnitUnderAttack:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.unitUnderAttack", tooltipUnitUnderAttack);
            case GameAlertType.BuildingUnderAttack:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.buildingUnderAttack", tooltipBuildingUnderAttack);
            case GameAlertType.DroneIsNotAssigned:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.droneIsNotAssigned", tooltipDroneIsNotAssigned);
            case GameAlertType.DroneNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.droneNoResource", tooltipDroneNoResource);
            case GameAlertType.ConstructNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.constructNoResource", tooltipConstructNoResource);
            case GameAlertType.StorageFull:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.storageFull", tooltipStorageFull);
            case GameAlertType.ElecInsufficient:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.elecInsufficient", tooltipElecInsufficient);
            case GameAlertType.NoiseCaution:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.noiseCaution", tooltipNoiseCaution);
            case GameAlertType.NoiseWarning:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.noiseWarning", tooltipNoiseWarning);
            case GameAlertType.NoiseDanger:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.noiseDanger", tooltipNoiseDanger);
            default: return string.Empty;
        }
    }
    
    private string GetToolTipExtraText(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
            case GameAlertType.MinerIsFull:
            case GameAlertType.UnitUnderAttack:
            case GameAlertType.DroneIsNotAssigned:
            case GameAlertType.DroneNoResource:
            case GameAlertType.ConstructNoResource:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.extra.unit", toolTipExtraTextUnit);
            case GameAlertType.BuildingUnderAttack:
                return GameLocalization.GetOrDefault("UI_Common", "alert.tooltip.extra.building", toolTipExtraTextBuilding);
            default: return string.Empty;
        }
    }

    private void RebuildTooltipLayout()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform textRect = tooltipText != null ? tooltipText.rectTransform : null;
        RectTransform panelRect = null;
        if (tooltipPanel != null)
            panelRect = tooltipPanel.GetComponent<RectTransform>();

        if (textRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        if (_tooltipRebuildCoroutine != null)
            StopCoroutine(_tooltipRebuildCoroutine);
        _tooltipRebuildCoroutine = StartCoroutine(DelayedRebuildTooltipLayout());
    }

    private IEnumerator DelayedRebuildTooltipLayout()
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        RectTransform textRect = tooltipText != null ? tooltipText.rectTransform : null;
        RectTransform panelRect = null;
        if (tooltipPanel != null)
            panelRect = tooltipPanel.GetComponent<RectTransform>();

        if (textRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (panelRect != null && panelRect.parent is RectTransform parentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        _tooltipRebuildCoroutine = null;
    }

    private List<Damageable> GetSourcesForType(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource: return _minerNoResourceSources;
            case GameAlertType.MinerIsFull: return _minerIsFullSources;
            case GameAlertType.UnitUnderAttack: return _unitUnderAttackSources;
            case GameAlertType.BuildingUnderAttack: return _buildingUnderAttackSources;
            case GameAlertType.DroneIsNotAssigned: return _droneIsNotAssignedSources;
            case GameAlertType.DroneNoResource: return _droneNoResourceSources;
            case GameAlertType.ConstructNoResource: return _constructNoResourceSources;
            case GameAlertType.StorageFull: return _storageFullSources;
            case GameAlertType.ElecInsufficient: return _elecInsufficientSources;
            default: return new List<Damageable>();
        }
    }

    private static string GetDisplayName(Damageable d)
    {
        if (d == null) return string.Empty;
        if (d.GetComponent<Unit_Player>() != null) return GameLocalization.GetOrDefault("UI_Common", "label.mainUnit", "메인 유닛");
        if (d is UnitBase ub && ub.unitData != null) return ub.unitData.GetDisplayName();
        BuildingDataHolder b = d.GetComponent<BuildingDataHolder>();
        if (b != null && b.buildingData != null) return b.buildingData.GetDisplayName();
        return d.gameObject.name;
    }

    public IReadOnlyList<Damageable> GetAlertSources(GameAlertType type)
    {
        List<Damageable> sources = GetSourcesForType(type);
        if (sources == null || sources.Count == 0) return sources;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] == null)
                sources.RemoveAt(i);
        }
        return sources;
    }

    public bool TryFocusAlert(GameAlertType type)
    {
        if (!IsFocusableAlertType(type))
            return false;
        if (cameraTargetController == null)
            cameraTargetController = FindFirstObjectByType<CameraTargetController>();
        if (cameraTargetController == null)
            return false;
        IReadOnlyList<Damageable> sources = GetAlertSources(type);
        if (sources == null || sources.Count == 0)
            return false;
        int index;
        if (!_alertFocusIndices.TryGetValue(type, out index))
            index = 0;
        if (index >= sources.Count)
            index = 0;
        Damageable target = sources[index];
        int safety = 0;
        while (target == null && safety < sources.Count)
        {
            index = (index + 1) % sources.Count;
            target = sources[index];
            safety++;
        }
        if (target == null)
            return false;
        _alertFocusIndices[type] = (index + 1) % sources.Count;
        cameraTargetController.SetFollowTarget(target.transform);
        TargetBracketEffect.Show(target.transform);
        return true;
    }

    private static bool IsFocusableAlertType(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
            case GameAlertType.MinerIsFull:
            case GameAlertType.UnitUnderAttack:
            case GameAlertType.BuildingUnderAttack:
            case GameAlertType.DroneIsNotAssigned:
            case GameAlertType.DroneNoResource:
            case GameAlertType.ConstructNoResource:
                return true;
            default:
                return false;
        }
    }

}

