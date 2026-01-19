using UnityEngine;
using UnityEngine.Tilemaps;

public enum BuildingPieceType
{
    None,
    MetalPanel,
    ChargeCoil,
    BioFilter,
    Cooler,
    ModularPanel,
    ElectroMagnet,
    BioCultivator,
    CrystalLink,
    AetherCondenser
}

[System.Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}

[CreateAssetMenu(fileName = "New BuildingPiece", menuName = "BuildingPiece/BuildingPiece Data")]
public class BuildingPieceData : DisplayableData
{
    [Header("BuildingPiece Info")]
    public GameObject buildingPiecePrefab;
    public BuildingPieceType buildingPieceType;
    public TileBase buildingPieceTile;
    public ResourceCost[] costs;
}