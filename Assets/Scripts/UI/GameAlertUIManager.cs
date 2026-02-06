using UnityEngine;

public enum GameAlertType
{
    MinerNoResource,
    UnitUnderAttack,
    BuildingUnderAttack,
    DroneNoResource,
    StorageFull,
    AetherStorageFull
}

public class GameAlertUIManager : MonoBehaviour
{
    public static GameAlertUIManager Instance { get; private set; }

    [SerializeField] private GameObject minerNoResourceCell;
    [SerializeField] private GameObject unitUnderAttackCell;
    [SerializeField] private GameObject buildingUnderAttackCell;
    [SerializeField] private GameObject droneNoResourceCell;
    [SerializeField] private GameObject storageFullCell;
    [SerializeField] private GameObject aetherStorageFullCell;
    [SerializeField] private GameObject noiseCautionCell;
    [SerializeField] private GameObject noiseWarningCell;
    [SerializeField] private GameObject noiseDangerCell;

    private int _minerNoResourceCount;
    private int _unitUnderAttackCount;
    private int _buildingUnderAttackCount;
    private int _droneNoResourceCount;
    private int _storageFullCount;
    private int _aetherStorageFullCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SetAllInactive();
    }

    private void OnEnable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged += OnNoiseChanged;
            OnNoiseChanged(NoiseManager.Instance.NoisePercentage);
        }
    }

    private void OnDisable()
    {
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.OnNoiseChanged -= OnNoiseChanged;
        }
    }

    private void OnNoiseChanged(float noisePercentage)
    {
        if (NoiseManager.Instance == null) return;

        NoiseManager.NoiseZone zone = NoiseManager.Instance.GetCurrentNoiseZone();
        if (noiseCautionCell != null) noiseCautionCell.SetActive(zone == NoiseManager.NoiseZone.Caution);
        if (noiseWarningCell != null) noiseWarningCell.SetActive(zone == NoiseManager.NoiseZone.Warning);
        if (noiseDangerCell != null) noiseDangerCell.SetActive(zone == NoiseManager.NoiseZone.Danger);
    }

    public void RegisterAlert(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                _minerNoResourceCount++;
                break;
            case GameAlertType.UnitUnderAttack:
                _unitUnderAttackCount++;
                break;
            case GameAlertType.BuildingUnderAttack:
                _buildingUnderAttackCount++;
                break;
            case GameAlertType.DroneNoResource:
                _droneNoResourceCount++;
                break;
            case GameAlertType.StorageFull:
                _storageFullCount++;
                break;
            case GameAlertType.AetherStorageFull:
                _aetherStorageFullCount++;
                break;
        }

        UpdateAlertState(type);
    }

    public void UnregisterAlert(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                _minerNoResourceCount = Mathf.Max(0, _minerNoResourceCount - 1);
                break;
            case GameAlertType.UnitUnderAttack:
                _unitUnderAttackCount = Mathf.Max(0, _unitUnderAttackCount - 1);
                break;
            case GameAlertType.BuildingUnderAttack:
                _buildingUnderAttackCount = Mathf.Max(0, _buildingUnderAttackCount - 1);
                break;
            case GameAlertType.DroneNoResource:
                _droneNoResourceCount = Mathf.Max(0, _droneNoResourceCount - 1);
                break;
            case GameAlertType.StorageFull:
                _storageFullCount = Mathf.Max(0, _storageFullCount - 1);
                break;
            case GameAlertType.AetherStorageFull:
                _aetherStorageFullCount = Mathf.Max(0, _aetherStorageFullCount - 1);
                break;
        }

        UpdateAlertState(type);
    }

    public void SetAlertActive(GameAlertType type, bool active)
    {
        if (Instance == null) return;

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
        }
    }

    private void UpdateAlertState(GameAlertType type)
    {
        switch (type)
        {
            case GameAlertType.MinerNoResource:
                if (minerNoResourceCell != null) minerNoResourceCell.SetActive(_minerNoResourceCount > 0);
                break;
            case GameAlertType.UnitUnderAttack:
                if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(_unitUnderAttackCount > 0);
                break;
            case GameAlertType.BuildingUnderAttack:
                if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(_buildingUnderAttackCount > 0);
                break;
            case GameAlertType.DroneNoResource:
                if (droneNoResourceCell != null) droneNoResourceCell.SetActive(_droneNoResourceCount > 0);
                break;
            case GameAlertType.StorageFull:
                if (storageFullCell != null) storageFullCell.SetActive(_storageFullCount > 0);
                break;
            case GameAlertType.AetherStorageFull:
                if (aetherStorageFullCell != null) aetherStorageFullCell.SetActive(_aetherStorageFullCount > 0);
                break;
        }
    }

    private void SetAllInactive()
    {
        if (minerNoResourceCell != null) minerNoResourceCell.SetActive(false);
        if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(false);
        if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(false);
        if (droneNoResourceCell != null) droneNoResourceCell.SetActive(false);
        if (storageFullCell != null) storageFullCell.SetActive(false);
        if (aetherStorageFullCell != null) aetherStorageFullCell.SetActive(false);
        if (noiseCautionCell != null) noiseCautionCell.SetActive(false);
        if (noiseWarningCell != null) noiseWarningCell.SetActive(false);
        if (noiseDangerCell != null) noiseDangerCell.SetActive(false);
    }
}

