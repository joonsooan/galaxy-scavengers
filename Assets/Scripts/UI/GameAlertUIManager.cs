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

    private void SetAllInactive()
    {
        if (minerNoResourceCell != null) minerNoResourceCell.SetActive(false);
        if (unitUnderAttackCell != null) unitUnderAttackCell.SetActive(false);
        if (buildingUnderAttackCell != null) buildingUnderAttackCell.SetActive(false);
        if (droneNoResourceCell != null) droneNoResourceCell.SetActive(false);
    }
}

