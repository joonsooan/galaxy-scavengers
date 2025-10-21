using UnityEngine;
using UnityEngine.Tilemaps;

public class BuildingSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap buildingTilemap;
    [SerializeField] private GameObject mainStructurePrefab;
    [SerializeField] private TileBase mainStructureTile;
    [SerializeField] private Grid grid;
    [SerializeField] private Transform parentTransform;

    public Tilemap BuildingTilemap {
        get {
            return buildingTilemap;
        }
    }

    public void SpawnBuildings()
    {
        if (buildingTilemap == null || mainStructurePrefab == null || mainStructureTile == null || grid == null) {
            Debug.LogError("BuildingSpawner: Missing references.");
            return;
        }
        
        if (parentTransform == null) {
            parentTransform = this.transform;
        }

        foreach (Vector3Int cellPosition in buildingTilemap.cellBounds.allPositionsWithin) {
            if (buildingTilemap.HasTile(cellPosition) && buildingTilemap.GetTile(cellPosition) == mainStructureTile) {
                Vector3 worldPos = grid.GetCellCenterWorld(cellPosition);
                
                GameObject newBuilding = Instantiate(mainStructurePrefab, worldPos, Quaternion.identity, parentTransform);
                MainStructure mainStructure = newBuilding.GetComponent<MainStructure>();
                
                if (mainStructure != null && ResourceManager.Instance != null)
                {
                    ResourceManager.Instance.RegisterMainStructure(mainStructure);
                    ResourceManager.Instance.AddStorage(mainStructure);
                }
            }
        }
    }
}
