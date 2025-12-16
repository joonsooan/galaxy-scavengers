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

    public Tilemap BuildingTilemap => buildingTilemap;

    public void SpawnBuildings()
    {
        if (buildingTilemap == null || mainStructurePrefab == null || mainStructureTile == null || grid == null) {
            Debug.LogError("BuildingSpawner: Missing references.");
            return;
        }
        
        if (parentTransform == null) {
            parentTransform = transform;
        }

        foreach (Vector3Int cellPosition in buildingTilemap.cellBounds.allPositionsWithin) {
            if (buildingTilemap.HasTile(cellPosition) && buildingTilemap.GetTile(cellPosition) == mainStructureTile) {
                Vector3 worldPos = grid.GetCellCenterWorld(cellPosition);
                
                // Calculate the anchor position for a 3x3 building (bottom-left corner)
                // The tile position is the center, so we need to offset to get the bottom-left
                Vector3Int anchorCell = cellPosition - new Vector3Int(1, 1, 0);
                
                GameObject newBuilding = Instantiate(mainStructurePrefab, worldPos, Quaternion.identity, parentTransform);
                MainStructure mainStructure = newBuilding.GetComponent<MainStructure>();
                
                if (mainStructure != null && ResourceManager.Instance != null)
                {
                    ResourceManager.Instance.RegisterMainStructure(mainStructure);
                    ResourceManager.Instance.AddStorage(mainStructure);
                }
                
                // Register the Main Structure as a 3x3 building in BuildingManager
                if (BuildingManager.Instance != null)
                {
                    BuildingManager.Instance.RegisterMainStructure(anchorCell, new Vector2Int(3, 3));
                }
                
                // Remove the main structure tile from the tilemap (keep GameObject, remove tile)
                // Remove all tiles in the 3x3 grid
                for (int x = 0; x < 3; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        Vector3Int cellToRemove = anchorCell + new Vector3Int(x, y, 0);
                        if (buildingTilemap.HasTile(cellToRemove) && buildingTilemap.GetTile(cellToRemove) == mainStructureTile)
                        {
                            buildingTilemap.SetTile(cellToRemove, null);
                        }
                    }
                }
            }
        }
        
        // Refresh tilemap after removing tiles
        if (buildingTilemap != null)
        {
            buildingTilemap.RefreshAllTiles();
        }
    }
}
