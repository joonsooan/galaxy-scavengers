using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Drone Hub Data", menuName = "Building/Drone Hub Data")]
public class DroneHubData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string droneHubName;
    [SerializeField] [TextArea(3, 10)] private string droneHubInfo;
    
    [Header("Production Settings")]
    [SerializeField] private List<UnitData> producibleUnits;

    public string DroneHubName => droneHubName;
    public string DroneHubInfo => droneHubInfo;
    public List<UnitData> ProducibleUnits => producibleUnits;
}

