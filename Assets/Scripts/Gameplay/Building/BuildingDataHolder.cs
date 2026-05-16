using UnityEngine;

public class BuildingDataHolder : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField] public BuildingData buildingData;

    public void SetBuildingData(BuildingData data)
    {
        buildingData = data;
    }

    public Damageable GetDamageable()
    {
        return GetComponent<Damageable>();
    }
}

