using UnityEngine;

public enum GameAlertType
{
    MinerNoResource,
    UnitUnderAttack,
    BuildingUnderAttack,
    DroneNoResource
}

public class GameAlertUIManager : MonoBehaviour
{
    public static GameAlertUIManager Instance { get; private set; }

    [SerializeField] private GameObject minerNoResourceCell;
    [SerializeField] private GameObject unitUnderAttackCell;
    [SerializeField] private GameObject buildingUnderAttackCell;
    [SerializeField] private GameObject droneNoResourceCell;

    private int _minerNoResourceCount;
    private int _unitUnderAttackCount;
    private int _buildingUnderAttackCount;
    private int _droneNoResourceCount;

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
        }
    }

    private void SetAllInactive()
    {
        if (minerNoResourceCell != null) minerNoResourceCell.SetActive(false);
        if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(false);
        if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(false);
        if (droneNoResourceCell != null) droneNoResourceCell.SetActive(false);
    }
}

