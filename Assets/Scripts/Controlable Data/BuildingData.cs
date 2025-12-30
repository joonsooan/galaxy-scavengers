using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum BuildingType
{
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
    MainStructure
}

[CreateAssetMenu(fileName = "New Building Data", menuName = "Building/Building Data")]
public class BuildingData : DisplayableData
{
    [Header("Building Info")]
    public GameObject buildingPrefab;
    public TileBase buildingTile;
    public BuildingType buildingType;
    public List<BuildingPiece> recipe;

    [System.Serializable]
    public class BuildingPiece
    {
        public BuildingPieceType buildingPieceType;
        public Vector3Int relativePosition;
    }
}