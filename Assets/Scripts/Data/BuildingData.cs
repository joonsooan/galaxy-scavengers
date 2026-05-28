using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum BuildingType
{
    None = -1,
    Storage,
    Generator,
    DroneHub,
    Smelter,
    Assembler,
    Reactor,
    Battery,
    RepairStation,
    Turret,
    Radar,
    OrbitalLauncher,
    MainStructure,
    PowerReceiver,
    DataExtractor,
    ChargingStation,
    Platform
}

[CreateAssetMenu(fileName = "New Building Data", menuName = "Building/Building Data")]
public class BuildingData : DisplayableData
{
    [Header("Building Info")]
    public GameObject buildingPrefab;
    public TileBase buildingTile;
    public BuildingType buildingType;
    public List<BuildingPiece> recipe;

    [Header("Noise Coefficient")]
    [Tooltip("Noise coefficient value for this building (0-100)")]
    [Range(0f, 100f)]
    public float noiseCoefficient = 0f;

    [System.Serializable]
    public class BuildingPiece
    {
        public BuildingPieceType buildingPieceType;
        public Vector3Int relativePosition;
    }
}
