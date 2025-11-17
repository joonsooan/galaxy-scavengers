using UnityEngine;

public class BuildingPiece : MonoBehaviour
{
    [HideInInspector] public GadgetType gadgetType;
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