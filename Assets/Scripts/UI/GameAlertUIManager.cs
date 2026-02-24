using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

public enum GameAlertType
{
    MinerNoResource = 0,
    UnitUnderAttack = 1,
    BuildingUnderAttack = 2,
    DroneNoResource = 3,
    StorageFull = 4,
    AetherStorageFull = 5,
    NoiseCaution = 6,
    NoiseWarning = 7,
    NoiseDanger = 8,
    MainEngineRepair = 9,
    MineTypeAllOff = 10,
    MinerIsFull = 11,
    DroneIsNotAssigned = 12,
    ConstructNoResource = 13
}

public class GameAlertUIManager : MonoBehaviour
{
    [SerializeField] private GameObject minetypeallOffCell;
    [SerializeField] private GameObject minerNoResourceCell;
    [SerializeField] private GameObject minerIsFullCell;
    [SerializeField] private GameObject unitUnderAttackCell;
    [SerializeField] private GameObject buildingUnderAttackCell;
    [SerializeField] private GameObject droneIsNotAssignedCell;
    [SerializeField] private GameObject droneNoResourceCell;
    [SerializeField] private GameObject constructNoResourceCell;
    [SerializeField] private GameObject storageFullCell;
    [SerializeField] private GameObject aetherStorageFullCell;
    [SerializeField] private GameObject mainEngineRepairCell;
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
    [SerializeField] private EventReference aetherStorageFullSound;
    [SerializeField] private EventReference mainEngineRepairSound;
    [SerializeField] private EventReference noiseCautionSound;
    [SerializeField] private EventReference noiseWarningSound;
    [SerializeField] private EventReference noiseDangerSound;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [Header("Tooltip Messages")]
    [SerializeField, TextArea(2, 6)] private string tooltipMineTypeAllOff = "채굴 타입이 모두 비활성화되었습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipMinerNoResource = "채굴 유닛이 캘 자원이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipMinerIsFull = "채굴 유닛이 자원을 저장할 공간이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipUnitUnderAttack = "유닛이 공격당하고 있습니다!";
    [SerializeField, TextArea(2, 6)] private string tooltipBuildingUnderAttack = "건물이 공격당하고 있습니다!";
    [SerializeField, TextArea(2, 6)] private string tooltipDroneIsNotAssigned = "가공 유닛이 프로세서에 배정되지 않았습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipDroneNoResource = "가공 유닛이 가공할 자원이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipConstructNoResource = "건설 유닛이 건설할 자원이 없습니다.";
    [SerializeField, TextArea(2, 6)] private string tooltipStorageFull = "모든 저장 공간이 가득 찼습니다!\n저장고를 더 건설해주세요.";
    [SerializeField, TextArea(2, 6)] private string tooltipAetherStorageFull = "모든 에테르 저장고가 가득 찼습니다!\n저장고나 배터리를 더 건설해주세요.";
    [SerializeField, TextArea(2, 6)] private string tooltipMainEngineRepair = "메인 엔진이 고장나\n시드 코어를 발사할 수 없습니다.\n퀘스트를 확인하세요.";
    [SerializeField, TextArea(2, 6)] private string tooltipNoiseCaution = "소음 정도 : 주의\n적의 습격에 대비하세요!";
    [SerializeField, TextArea(2, 6)] private string tooltipNoiseWarning = "소음 정도 : 경고\n적의 습격에 대비하세요!";
    [SerializeField, TextArea(2, 6)] private string tooltipNoiseDanger = "소음 정도 : 위험\n적의 습격에 대비하세요!";
    [SerializeField, TextArea(1, 3)] private string toolTipExtraTextUnit = "(클릭해서 유닛으로 이동)";
    [SerializeField, TextArea(1, 3)] private string toolTipExtraTextBuilding = "(클릭해서 건물로 이동)";

    private GameAlertType? _currentTooltipType;

    private readonly List<Damageable> _mineTypeAllOffSources = new List<Damageable>();
    private readonly List<Damageable> _minerNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _minerIsFullSources = new List<Damageable>();
    private readonly List<Damageable> _unitUnderAttackSources = new List<Damageable>();
    private readonly List<Damageable> _buildingUnderAttackSources = new List<Damageable>();
    private readonly List<Damageable> _droneIsNotAssignedSources = new List<Damageable>();
    private readonly List<Damageable> _droneNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _constructNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _storageFullSources = new List<Damageable>();
    private readonly List<Damageable> _aetherStorageFullSources = new List<Damageable>();
    private readonly List<Damageable> _mainEngineRepairSources = new List<Damageable>();
    private const int MaxSourcesPerType = 5;
    private Coroutine _tooltipRebuildCoroutine;
    private StorageTrackerManager _storageTrackerManager;
    private Coroutine _bindStorageTrackerCoroutine;
    private bool _isStorageFullByTracker;
    private bool _isAetherFullByTracker;

    private int _mineTypeAllOffCount;
    private int _minerNoResourceCount;
    private int _minerIsFullCount;
    private int _unitUnderAttackCount;
    private int _buildingUnderAttackCount;
    private int _droneIsNotAssignedCount;
    private int _droneNoResourceCount;
    private int _constructNoResourceCount;
    private int _storageFullCount;
    private int _aetherStorageFullCount;
    private int _mainEngineRepairCount;
    [SerializeField] private CameraTargetController cameraTargetController;
    private readonly Dictionary<GameAlertType, int> _alertFocusIndices = new Dictionary<GameAlertType, int>();
    private bool _unitUnderAttackAlertActive;
    private bool _buildingUnderAttackAlertActive;

    private bool _isCountdownIgnoringAlerts;

    private void Awake()
    {
        SetAllInactive();
    }

    private void OnEnable()
    {
        LaunchUIController.OnLaunchCountdownStarted += OnLaunchCountdownStarted;
        LaunchUIController.OnLaunchCountdownFinished += OnLaunchCountdownFinished;
        _isStorageFullByTracker = false;
        _isAetherFullByTracker = false;
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
            _storageTrackerManager.OnAetherChanged -= OnTrackedAetherChanged;
        }
        _storageTrackerManager = null;
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
        }
        HideTooltip();
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
        _storageTrackerManager.OnAetherChanged -= OnTrackedAetherChanged;
        _storageTrackerManager.OnAetherChanged += OnTrackedAetherChanged;
        SyncStorageFullAlertWithTracker();
        SyncAetherFullAlertWithTracker();
        _bindStorageTrackerCoroutine = null;
    }

    private void OnTrackedStorageChanged()
    {
        SyncStorageFullAlertWithTracker();
    }

    private void OnTrackedAetherChanged()
    {
        SyncAetherFullAlertWithTracker();
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

    private void SyncAetherFullAlertWithTracker()
    {
        if (_storageTrackerManager == null)
            return;

        int max = _storageTrackerManager.MaxStorableAetherAmount;
        int current = _storageTrackerManager.CurrentAetherAmount;
        bool isAetherFull = max > 0 && current >= max;

        if (isAetherFull == _isAetherFullByTracker)
            return;

        _isAetherFullByTracker = isAetherFull;
        if (isAetherFull)
            RegisterAlert(GameAlertType.AetherStorageFull);
        else
            UnregisterAlert(GameAlertType.AetherStorageFull);
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
            case GameAlertType.MineTypeAllOff:
                _mineTypeAllOffCount++;
                AddSource(_mineTypeAllOffSources, source);
                break;
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
            case GameAlertType.AetherStorageFull:
                _aetherStorageFullCount++;
                AddSource(_aetherStorageFullSources, source);
                break;
            case GameAlertType.MainEngineRepair:
                _mainEngineRepairCount++;
                AddSource(_mainEngineRepairSources, source);
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

    private void OnLaunchCountdownStarted()
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

    private void OnLaunchCountdownFinished()
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
            case GameAlertType.MineTypeAllOff:
                _mineTypeAllOffCount = Mathf.Max(0, _mineTypeAllOffCount - 1);
                RemoveSource(_mineTypeAllOffSources, source);
                break;
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
            case GameAlertType.AetherStorageFull:
                _aetherStorageFullCount = Mathf.Max(0, _aetherStorageFullCount - 1);
                RemoveSource(_aetherStorageFullSources, source);
                break;
            case GameAlertType.MainEngineRepair:
                _mainEngineRepairCount = Mathf.Max(0, _mainEngineRepairCount - 1);
                RemoveSource(_mainEngineRepairSources, source);
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
            case GameAlertType.MineTypeAllOff:
                if (minetypeallOffCell != null) minetypeallOffCell.SetActive(active);
                break;
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
            case GameAlertType.AetherStorageFull:
                if (aetherStorageFullCell != null) aetherStorageFullCell.SetActive(active);
                break;
            case GameAlertType.MainEngineRepair:
                if (mainEngineRepairCell != null) mainEngineRepairCell.SetActive(active);
                break;
        }

        if (!active && _currentTooltipType == type)
            HideTooltip();
    }

    private void UpdateAlertState(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MineTypeAllOff:
                if (minetypeallOffCell != null)
                {
                    bool active = _mineTypeAllOffCount > 0;
                    if (active && !minetypeallOffCell.activeSelf && !minerNoResourceSound.IsNull)
                        RuntimeManager.PlayOneShot(minerNoResourceSound);
                    minetypeallOffCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.MineTypeAllOff)
                        HideTooltip();
                }
                break;
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
            case GameAlertType.AetherStorageFull:
                if (aetherStorageFullCell != null)
                {
                    bool active = _aetherStorageFullCount > 0;
                    if (active && !aetherStorageFullCell.activeSelf && !aetherStorageFullSound.IsNull)
                        RuntimeManager.PlayOneShot(aetherStorageFullSound);
                    aetherStorageFullCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.AetherStorageFull)
                        HideTooltip();
                }
                break;
            case GameAlertType.MainEngineRepair:
                if (mainEngineRepairCell != null)
                {
                    bool active = _mainEngineRepairCount > 0;
                    if (active && !mainEngineRepairCell.activeSelf && !mainEngineRepairSound.IsNull)
                        RuntimeManager.PlayOneShot(mainEngineRepairSound);
                    mainEngineRepairCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.MainEngineRepair)
                        HideTooltip();
                }
                break;
        }
    }

    private void SetAllInactive()
    {
        if (minetypeallOffCell != null) minetypeallOffCell.SetActive(false);
        if (minerNoResourceCell != null) minerNoResourceCell.SetActive(false);
        if (minerIsFullCell != null) minerIsFullCell.SetActive(false);
        if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(false);
        if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(false);
        if (droneIsNotAssignedCell != null) droneIsNotAssignedCell.SetActive(false);
        if (droneNoResourceCell != null) droneNoResourceCell.SetActive(false);
        if (constructNoResourceCell != null) constructNoResourceCell.SetActive(false);
        if (storageFullCell != null) storageFullCell.SetActive(false);
        if (aetherStorageFullCell != null) aetherStorageFullCell.SetActive(false);
        if (mainEngineRepairCell != null) mainEngineRepairCell.SetActive(false);
        if (noiseCautionCell != null) noiseCautionCell.SetActive(false);
        if (noiseWarningCell != null) noiseWarningCell.SetActive(false);
        if (noiseDangerCell != null) noiseDangerCell.SetActive(false);
        _unitUnderAttackAlertActive = false;
        _buildingUnderAttackAlertActive = false;
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
        
        if (type == GameAlertType.StorageFull || type == GameAlertType.AetherStorageFull || type == GameAlertType.MainEngineRepair ||
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
            case GameAlertType.MineTypeAllOff: return tooltipMineTypeAllOff;
            case GameAlertType.MinerNoResource: return tooltipMinerNoResource;
            case GameAlertType.MinerIsFull: return tooltipMinerIsFull;
            case GameAlertType.UnitUnderAttack: return tooltipUnitUnderAttack;
            case GameAlertType.BuildingUnderAttack: return tooltipBuildingUnderAttack;
            case GameAlertType.DroneIsNotAssigned: return tooltipDroneIsNotAssigned;
            case GameAlertType.DroneNoResource: return tooltipDroneNoResource;
            case GameAlertType.ConstructNoResource: return tooltipConstructNoResource;
            case GameAlertType.StorageFull: return tooltipStorageFull;
            case GameAlertType.AetherStorageFull: return tooltipAetherStorageFull;
            case GameAlertType.MainEngineRepair: return tooltipMainEngineRepair;
            case GameAlertType.NoiseCaution: return tooltipNoiseCaution;
            case GameAlertType.NoiseWarning: return tooltipNoiseWarning;
            case GameAlertType.NoiseDanger: return tooltipNoiseDanger;
            default: return string.Empty;
        }
    }
    
    private string GetToolTipExtraText(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MineTypeAllOff: return toolTipExtraTextUnit;
            case GameAlertType.MinerNoResource: return toolTipExtraTextUnit;
            case GameAlertType.MinerIsFull: return toolTipExtraTextUnit;
            case GameAlertType.UnitUnderAttack: return toolTipExtraTextUnit;
            case GameAlertType.DroneIsNotAssigned: return toolTipExtraTextUnit;
            case GameAlertType.DroneNoResource: return toolTipExtraTextUnit;
            case GameAlertType.ConstructNoResource: return toolTipExtraTextUnit;
            case GameAlertType.BuildingUnderAttack: return toolTipExtraTextBuilding;
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
            case GameAlertType.MineTypeAllOff: return _mineTypeAllOffSources;
            case GameAlertType.MinerNoResource: return _minerNoResourceSources;
            case GameAlertType.MinerIsFull: return _minerIsFullSources;
            case GameAlertType.UnitUnderAttack: return _unitUnderAttackSources;
            case GameAlertType.BuildingUnderAttack: return _buildingUnderAttackSources;
            case GameAlertType.DroneIsNotAssigned: return _droneIsNotAssignedSources;
            case GameAlertType.DroneNoResource: return _droneNoResourceSources;
            case GameAlertType.ConstructNoResource: return _constructNoResourceSources;
            case GameAlertType.StorageFull: return _storageFullSources;
            case GameAlertType.AetherStorageFull: return _aetherStorageFullSources;
            case GameAlertType.MainEngineRepair: return _mainEngineRepairSources;
            default: return new List<Damageable>();
        }
    }

    private static string GetDisplayName(Damageable d)
    {
        if (d == null) return string.Empty;
        if (d.GetComponent<Unit_Player>() != null) return "메인 유닛";
        if (d is UnitBase ub && ub.unitData != null) return ub.unitData.unitName;
        BuildingDataHolder b = d.GetComponent<BuildingDataHolder>();
        if (b != null && b.buildingData != null) return b.buildingData.displayName;
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
            case GameAlertType.MineTypeAllOff:
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

