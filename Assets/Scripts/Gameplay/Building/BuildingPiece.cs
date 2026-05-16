using UnityEngine;

public class BuildingPiece : MonoBehaviour
{
    [HideInInspector] public BuildingPieceType buildingPieceType;
    [HideInInspector] public Vector3Int cellPosition;
    
    private void OnDestroy()
    {
        if (BuildingManager.Instance != null)
        {
            if (BuildingManager.Instance.GetPieceAt(cellPosition) == this)
            {
                BuildingManager.Instance.ClearBuildingDataAt(cellPosition);
            }
        }
    }
}