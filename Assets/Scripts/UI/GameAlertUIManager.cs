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
    MainEngineRepair = 9
}

public class GameAlertUIManager : MonoBehaviour
{
    [SerializeField] private GameObject minerNoResourceCell;
    [SerializeField] private GameObject unitUnderAttackCell;
    [SerializeField] private GameObject buildingUnderAttackCell;
    [SerializeField] private GameObject droneNoResourceCell;
    [SerializeField] private GameObject storageFullCell;
    [SerializeField] private GameObject aetherStorageFullCell;
    [SerializeField] private GameObject mainEngineRepairCell;
    [SerializeField] private GameObject noiseCautionCell;
    [SerializeField] private GameObject noiseWarningCell;
    [SerializeField] private GameObject noiseDangerCell;

    [Header("Alert Sounds")]
    [SerializeField] private EventReference minerNoResourceSound;
    [SerializeField] private EventReference unitUnderAttackSound;
    [SerializeField] private EventReference buildingUnderAttackSound;
    [SerializeField] private EventReference droneNoResourceSound;
    [SerializeField] private EventReference storageFullSound;
    [SerializeField] private EventReference aetherStorageFullSound;
    [SerializeField] private EventReference mainEngineRepairSound;
    [SerializeField] private EventReference noiseCautionSound;
    [SerializeField] private EventReference noiseWarningSound;
    [SerializeField] private EventReference noiseDangerSound;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    
    [Header("Alert Shader")]
    [SerializeField] private Material shaderMaterial;
    [SerializeField] private string shaderMaterialResourcePath;
    [SerializeField] private float shaderWaveSpeedOnValue = 1f;

    private const string TooltipMinerNoResource = "채굴 유닛이 캘 자원이 없습니다.";
    private const string TooltipUnitUnderAttack = "유닛이 공격당하고 있습니다!";
    private const string TooltipBuildingUnderAttack = "건물이 공격당하고 있습니다!";
    private const string TooltipDroneNoResource = "가공 유닛이 가공할 자원이 없습니다.";
    private const string TooltipStorageFull = "모든 저장 공간이 가득 찼습니다!\n저장고를 더 건설해주세요.";
    private const string TooltipAetherStorageFull = "모든 에테르 저장고가 가득 찼습니다!\n저장고나 배터리를 더 건설해주세요.";
    private const string TooltipMainEngineRepair = "메인 엔진이 고장나\n시드 코어를 발사할 수 없습니다.\n퀘스트를 확인하세요.";
    private const string ToolTipNoiseCaution = "소음 정도 : 주의\n적의 습격에 대비하세요!";
    private const string ToolTipNoiseWarning = "소음 정도 : 경고\n적의 습격에 대비하세요!";
    private const string ToolTipNoiseDanger = "소음 정도 : 위험\n적의 습격에 대비하세요!";

    private const string ToolTipExtraTextUnit = "(클릭해서 유닛으로 이동)";
    private const string ToolTipExtraTextBuilding = "(클릭해서 건물로 이동)";

    private GameAlertType? _currentTooltipType;

    private readonly List<Damageable> _minerNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _unitUnderAttackSources = new List<Damageable>();
    private readonly List<Damageable> _buildingUnderAttackSources = new List<Damageable>();
    private readonly List<Damageable> _droneNoResourceSources = new List<Damageable>();
    private readonly List<Damageable> _storageFullSources = new List<Damageable>();
    private readonly List<Damageable> _aetherStorageFullSources = new List<Damageable>();
    private readonly List<Damageable> _mainEngineRepairSources = new List<Damageable>();
    private const int MaxSourcesPerType = 5;
    private Coroutine _tooltipRebuildCoroutine;
    private bool _alertShaderSuppressed;
    private float _cachedShaderWaveSpeed = 1f;
    private bool _hasCachedShaderWaveSpeed;
    private StorageTrackerManager _storageTrackerManager;
    private Coroutine _bindStorageTrackerCoroutine;
    private bool _isStorageFullByTracker;
    private bool _isAetherFullByTracker;

    private int _minerNoResourceCount;
    private int _unitUnderAttackCount;
    private int _buildingUnderAttackCount;
    private int _droneNoResourceCount;
    private int _storageFullCount;
    private int _aetherStorageFullCount;
    private int _mainEngineRepairCount;
    [SerializeField] private CameraTargetController cameraTargetController;
    private readonly Dictionary<GameAlertType, int> _alertFocusIndices = new Dictionary<GameAlertType, int>();

    private void Awake()
    {
        if (shaderMaterial == null && !string.IsNullOrEmpty(shaderMaterialResourcePath))
            shaderMaterial = Resources.Load<Material>(shaderMaterialResourcePath);
        if (shaderMaterial == null)
        {
            Image panelImage = GetComponent<Image>();
            if (panelImage != null)
                shaderMaterial = panelImage.material;
        }
        CacheShaderDefaults();
        SetAllInactive();
    }

    private void OnEnable()
    {
        _alertShaderSuppressed = false;
        DisableShaderMaterial();
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
        DisableShaderMaterial();
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
            if (cautionActive && !noiseCautionCell.activeSelf)
                OnNewAlertCellActivated();
            noiseCautionCell.SetActive(cautionActive);
        }
        if (noiseWarningCell != null)
        {
            bool warningActive = zone == NoiseManager.NoiseZone.Warning;
            if (warningActive && !noiseWarningCell.activeSelf && !noiseWarningSound.IsNull)
                RuntimeManager.PlayOneShot(noiseWarningSound);
            if (warningActive && !noiseWarningCell.activeSelf)
                OnNewAlertCellActivated();
            noiseWarningCell.SetActive(warningActive);
        }
        if (noiseDangerCell != null)
        {
            bool dangerActive = zone == NoiseManager.NoiseZone.Danger;
            if (dangerActive && !noiseDangerCell.activeSelf && !noiseDangerSound.IsNull)
                RuntimeManager.PlayOneShot(noiseDangerSound);
            if (dangerActive && !noiseDangerCell.activeSelf)
                OnNewAlertCellActivated();
            noiseDangerCell.SetActive(dangerActive);
        }
        RefreshAlertShaderState();
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
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                _minerNoResourceCount++;
                AddSource(_minerNoResourceSources, source);
                break;
            case GameAlertType.UnitUnderAttack:
                _unitUnderAttackCount++;
                AddSource(_unitUnderAttackSources, source);
                break;
            case GameAlertType.BuildingUnderAttack:
                _buildingUnderAttackCount++;
                AddSource(_buildingUnderAttackSources, source);
                break;
            case GameAlertType.DroneNoResource:
                _droneNoResourceCount++;
                AddSource(_droneNoResourceSources, source);
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
            case GameAlertType.UnitUnderAttack:
                _unitUnderAttackCount = Mathf.Max(0, _unitUnderAttackCount - 1);
                RemoveSource(_unitUnderAttackSources, source);
                break;
            case GameAlertType.BuildingUnderAttack:
                _buildingUnderAttackCount = Mathf.Max(0, _buildingUnderAttackCount - 1);
                RemoveSource(_buildingUnderAttackSources, source);
                break;
            case GameAlertType.DroneNoResource:
                _droneNoResourceCount = Mathf.Max(0, _droneNoResourceCount - 1);
                RemoveSource(_droneNoResourceSources, source);
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
        GameObject targetCell = GetAlertCell(type);
        bool wasActive = targetCell != null && targetCell.activeSelf;

        switch (type)
        {
            case GameAlertType.MinerNoResource:
                if (minerNoResourceCell != null) minerNoResourceCell.SetActive(active);
                break;
            case GameAlertType.UnitUnderAttack:
                if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(active);
                break;
            case GameAlertType.BuildingUnderAttack:
                if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(active);
                break;
            case GameAlertType.DroneNoResource:
                if (droneNoResourceCell != null) droneNoResourceCell.SetActive(active);
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
        if (active && !wasActive)
            OnNewAlertCellActivated();
        RefreshAlertShaderState();

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
                    if (active && !minerNoResourceCell.activeSelf)
                        OnNewAlertCellActivated();
                    minerNoResourceCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.MinerNoResource)
                        HideTooltip();
                }
                break;
            case GameAlertType.UnitUnderAttack:
                if (unitUnderAttackCell != null)
                {
                    bool active = _unitUnderAttackCount > 0;
                    if (active && !unitUnderAttackCell.activeSelf && !unitUnderAttackSound.IsNull)
                        RuntimeManager.PlayOneShot(unitUnderAttackSound);
                    if (active && !unitUnderAttackCell.activeSelf)
                        OnNewAlertCellActivated();
                    unitUnderAttackCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.UnitUnderAttack)
                        HideTooltip();
                }
                break;
            case GameAlertType.BuildingUnderAttack:
                if (buildingUnderAttackCell != null)
                {
                    bool active = _buildingUnderAttackCount > 0;
                    if (active && !buildingUnderAttackCell.activeSelf && !buildingUnderAttackSound.IsNull)
                        RuntimeManager.PlayOneShot(buildingUnderAttackSound);
                    if (active && !buildingUnderAttackCell.activeSelf)
                        OnNewAlertCellActivated();
                    buildingUnderAttackCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.BuildingUnderAttack)
                        HideTooltip();
                }
                break;
            case GameAlertType.DroneNoResource:
                if (droneNoResourceCell != null)
                {
                    bool active = _droneNoResourceCount > 0;
                    if (active && !droneNoResourceCell.activeSelf && !droneNoResourceSound.IsNull)
                        RuntimeManager.PlayOneShot(droneNoResourceSound);
                    if (active && !droneNoResourceCell.activeSelf)
                        OnNewAlertCellActivated();
                    droneNoResourceCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.DroneNoResource)
                        HideTooltip();
                }
                break;
            case GameAlertType.StorageFull:
                if (storageFullCell != null)
                {
                    bool active = _storageFullCount > 0;
                    if (active && !storageFullCell.activeSelf && !storageFullSound.IsNull)
                        RuntimeManager.PlayOneShot(storageFullSound);
                    if (active && !storageFullCell.activeSelf)
                        OnNewAlertCellActivated();
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
                    if (active && !aetherStorageFullCell.activeSelf)
                        OnNewAlertCellActivated();
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
                    if (active && !mainEngineRepairCell.activeSelf)
                        OnNewAlertCellActivated();
                    mainEngineRepairCell.SetActive(active);
                    if (!active && _currentTooltipType == GameAlertType.MainEngineRepair)
                        HideTooltip();
                }
                break;
        }
        RefreshAlertShaderState();
    }

    private void SetAllInactive()
    {
        if (minerNoResourceCell != null) minerNoResourceCell.SetActive(false);
        if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(false);
        if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(false);
        if (droneNoResourceCell != null) droneNoResourceCell.SetActive(false);
        if (storageFullCell != null) storageFullCell.SetActive(false);
        if (aetherStorageFullCell != null) aetherStorageFullCell.SetActive(false);
        if (mainEngineRepairCell != null) mainEngineRepairCell.SetActive(false);
        if (noiseCautionCell != null) noiseCautionCell.SetActive(false);
        if (noiseWarningCell != null) noiseWarningCell.SetActive(false);
        if (noiseDangerCell != null) noiseDangerCell.SetActive(false);
        DisableShaderMaterial();
    }

    public void ShowTooltip(GameAlertType type)
    {
        if (tooltipPanel == null || tooltipText == null) return;
        ConsumeAlertShaderHighlight();
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
            case GameAlertType.MinerNoResource: return TooltipMinerNoResource;
            case GameAlertType.UnitUnderAttack: return TooltipUnitUnderAttack;
            case GameAlertType.BuildingUnderAttack: return TooltipBuildingUnderAttack;
            case GameAlertType.DroneNoResource: return TooltipDroneNoResource;
            case GameAlertType.StorageFull: return TooltipStorageFull;
            case GameAlertType.AetherStorageFull: return TooltipAetherStorageFull;
            case GameAlertType.MainEngineRepair: return TooltipMainEngineRepair;
            case GameAlertType.NoiseCaution: return ToolTipNoiseCaution;
            case GameAlertType.NoiseWarning: return ToolTipNoiseWarning;
            case GameAlertType.NoiseDanger: return ToolTipNoiseDanger;
            default: return string.Empty;
        }
    }
    
    private string GetToolTipExtraText(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource: return ToolTipExtraTextUnit;
            case GameAlertType.UnitUnderAttack: return ToolTipExtraTextUnit;
            case GameAlertType.DroneNoResource: return ToolTipExtraTextUnit;
            case GameAlertType.BuildingUnderAttack: return ToolTipExtraTextBuilding;
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
            case GameAlertType.UnitUnderAttack: return _unitUnderAttackSources;
            case GameAlertType.BuildingUnderAttack: return _buildingUnderAttackSources;
            case GameAlertType.DroneNoResource: return _droneNoResourceSources;
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
            case GameAlertType.MinerNoResource:
            case GameAlertType.UnitUnderAttack:
            case GameAlertType.BuildingUnderAttack:
            case GameAlertType.DroneNoResource:
                return true;
            default:
                return false;
        }
    }

    private void ConsumeAlertShaderHighlight()
    {
        if (_alertShaderSuppressed)
            return;
        _alertShaderSuppressed = true;
        DisableShaderMaterial();
    }

    private void OnNewAlertCellActivated()
    {
        _alertShaderSuppressed = false;
        EnableShaderMaterial();
    }

    private void RefreshAlertShaderState()
    {
        if (!HasAnyActiveAlertCell())
        {
            DisableShaderMaterial();
            return;
        }
        if (!_alertShaderSuppressed)
            EnableShaderMaterial();
    }

    private bool HasAnyActiveAlertCell()
    {
        return (minerNoResourceCell != null && minerNoResourceCell.activeSelf) ||
               (unitUnderAttackCell != null && unitUnderAttackCell.activeSelf) ||
               (buildingUnderAttackCell != null && buildingUnderAttackCell.activeSelf) ||
               (droneNoResourceCell != null && droneNoResourceCell.activeSelf) ||
               (storageFullCell != null && storageFullCell.activeSelf) ||
               (aetherStorageFullCell != null && aetherStorageFullCell.activeSelf) ||
               (mainEngineRepairCell != null && mainEngineRepairCell.activeSelf) ||
               (noiseCautionCell != null && noiseCautionCell.activeSelf) ||
               (noiseWarningCell != null && noiseWarningCell.activeSelf) ||
               (noiseDangerCell != null && noiseDangerCell.activeSelf);
    }

    private GameObject GetAlertCell(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource: return minerNoResourceCell;
            case GameAlertType.UnitUnderAttack: return unitUnderAttackCell;
            case GameAlertType.BuildingUnderAttack: return buildingUnderAttackCell;
            case GameAlertType.DroneNoResource: return droneNoResourceCell;
            case GameAlertType.StorageFull: return storageFullCell;
            case GameAlertType.AetherStorageFull: return aetherStorageFullCell;
            case GameAlertType.MainEngineRepair: return mainEngineRepairCell;
            default: return null;
        }
    }

    private void EnableShaderMaterial()
    {
        if (shaderMaterial == null)
            return;
        if (shaderMaterial.HasProperty("_Enabled"))
        {
            shaderMaterial.SetFloat("_Enabled", 1f);
        }
        else if (shaderMaterial.HasProperty("_Intensity"))
        {
            shaderMaterial.SetFloat("_Intensity", 1f);
        }
        else if (shaderMaterial.HasProperty("_WaveSpeed"))
        {
            float activeWaveSpeed = _hasCachedShaderWaveSpeed && _cachedShaderWaveSpeed > 0f
                ? _cachedShaderWaveSpeed
                : shaderWaveSpeedOnValue;
            shaderMaterial.SetFloat("_WaveSpeed", activeWaveSpeed);
        }
    }

    private void DisableShaderMaterial()
    {
        if (shaderMaterial == null)
            return;
        if (shaderMaterial.HasProperty("_Enabled"))
        {
            shaderMaterial.SetFloat("_Enabled", 0f);
        }
        else if (shaderMaterial.HasProperty("_Intensity"))
        {
            shaderMaterial.SetFloat("_Intensity", 0f);
        }
        else if (shaderMaterial.HasProperty("_WaveSpeed"))
        {
            shaderMaterial.SetFloat("_WaveSpeed", 0f);
        }
    }

    private void CacheShaderDefaults()
    {
        if (shaderMaterial == null)
            return;
        if (shaderMaterial.HasProperty("_WaveSpeed"))
        {
            _cachedShaderWaveSpeed = shaderMaterial.GetFloat("_WaveSpeed");
            _hasCachedShaderWaveSpeed = true;
        }
    }
}

